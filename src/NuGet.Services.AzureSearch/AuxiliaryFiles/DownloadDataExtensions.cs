// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public static class DownloadDataExtensions
    {
        private const double PopularityTransfer = 0.99;
        private const double PopularityKept = 1 - PopularityTransfer;

        /// <summary>
        /// Override the download data used to score package popularity
        /// </summary>
        /// <param name="originalData">TODO</param>
        /// <param name="replacedPackages">TODO: Old to list of new</param>
        /// <param name="logger">TODO</param>
        /// <returns>TODO</returns>
        public static DownloadData ApplyDownloadOverrides(
            this DownloadData originalData,
            //IReadOnlyDictionary<string, long> downloadOverrides,
            IReadOnlyDictionary<string, List<string>> replacedPackages,
            ILogger logger)
        {
            if (originalData == null)
            {
                throw new ArgumentNullException(nameof(originalData));
            }

            if (replacedPackages == null)
            {
                throw new ArgumentNullException(nameof(replacedPackages));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            // Create a copy of the original data and apply overrides as we copy.
            var result = new DownloadData();
            var downloadTransfers = CalculateDownloadTransfers(originalData, replacedPackages);

            foreach (var downloadData in originalData)
            {
                var packageId = downloadData.Key;

                if (replacedPackages.ContainsKey(packageId))
                {
                    ApplyDownloadOverride(
                        originalData,
                        result,
                        packageId,
                        (long)(downloadData.Value.Total * PopularityKept),
                        logger);
                    continue;
                }

                if (downloadTransfers.TryGetValue(packageId, out var downloadTransfer))
                {
                    var downloadOverrides = downloadData.Value.Total + downloadTransfer;

                    ApplyDownloadOverride(
                        originalData,
                        result,
                        packageId,
                        downloadOverrides,
                        logger);
                    continue;
                }

                foreach (var versionData in downloadData.Value)
                {
                    result.SetDownloadCount(downloadData.Key, versionData.Key, versionData.Value);
                }
            }

            return result;
        }

        private static Dictionary<string, long> CalculateDownloadTransfers(
            this DownloadData originalData,
            IReadOnlyDictionary<string, List<string>> replacedPackages)
        {
             return replacedPackages
                .Where(replacedPackage => originalData.ContainsKey(replacedPackage.Key))
                .SelectMany(replacedPackage =>
                {
                    var replacements = replacedPackage.Value;
                    var replacedPackageDownloads = originalData[replacedPackage.Key];

                    var downloadTransfer = (long)(replacedPackageDownloads.Total * PopularityTransfer / replacements.Count);

                    return replacements.Select(packageId => new
                    {
                        PackageId = packageId,
                        DownloadTransfer = downloadTransfer
                    });
                })
                .GroupBy(x => x.PackageId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(packageReplacement => packageReplacement.DownloadTransfer).Sum());
        }

        private static void ApplyDownloadOverride(
            DownloadData originalData,
            DownloadData overriddenData,
            string packageId,
            long downloadOverride,
            ILogger logger)
        {
            logger.LogInformation(
                "Overriding downloads of package {PackageId} from {Downloads} to {DownloadsOverride}",
                packageId,
                originalData.GetDownloadCount(packageId),
                downloadOverride);

            var versions = originalData[packageId].Keys;

            overriddenData.SetDownloadCount(
                packageId,
                versions.First(),
                downloadOverride);
        }
    }
}
