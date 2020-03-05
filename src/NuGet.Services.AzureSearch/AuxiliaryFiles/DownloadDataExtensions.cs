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
        /// <param name="popularityTransfers">TODO: Old to list of new</param>
        /// <param name="logger">TODO</param>
        /// <returns>TODO</returns>
        public static DownloadData ApplyPopularityTransfers(
            this DownloadData originalData,
            //IReadOnlyDictionary<string, long> downloadOverrides,
            IReadOnlyDictionary<string, List<string>> popularityTransfers,
            ILogger logger)
        {
            if (originalData == null)
            {
                throw new ArgumentNullException(nameof(originalData));
            }

            if (popularityTransfers == null)
            {
                throw new ArgumentNullException(nameof(popularityTransfers));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            // Create a copy of the original data and apply overrides as we copy.
            var result = new DownloadData();
            var downloadTransfers = CalculateDownloadTransfers(originalData, popularityTransfers);

            foreach (var downloadData in originalData)
            {
                var packageId = downloadData.Key;

                // If this package's popularity has been transferred to other packages,
                // remove the necessary download counts.
                if (popularityTransfers.ContainsKey(packageId))
                {
                    ApplyDownloadOverride(
                        originalData,
                        result,
                        packageId,
                        (long)(downloadData.Value.Total * PopularityKept),
                        logger);
                    continue;
                }

                // Accept transferred downloads, if any.
                if (downloadTransfers.TryGetValue(packageId, out var downloadTransfer))
                {
                    var downloadOverride = downloadData.Value.Total + downloadTransfer;

                    ApplyDownloadOverride(
                        originalData,
                        result,
                        packageId,
                        downloadOverride,
                        logger);
                    continue;
                }

                // Otherwise, simply copy over the original download data.
                foreach (var versionData in downloadData.Value)
                {
                    result.SetDownloadCount(downloadData.Key, versionData.Key, versionData.Value);
                }
            }

            return result;
        }

        private static Dictionary<string, long> CalculateDownloadTransfers(
            this DownloadData originalData,
            IReadOnlyDictionary<string, List<string>> downloadTransfers)
        {
            var result = new Dictionary<string, long>();

            foreach (var transfer in downloadTransfers)
            {
                var fromPackageId = transfer.Key;
                var toPackageIds = transfer.Value;

                if (!originalData.ContainsKey(fromPackageId))
                {
                    continue;
                }

                var downloadTransfer = (long)(originalData[fromPackageId].Total * PopularityTransfer / toPackageIds.Count);

                foreach (var toPackageId in toPackageIds)
                {
                    if (!result.ContainsKey(toPackageId))
                    {
                        result[toPackageId] = 0;
                    }

                    result[toPackageId] += downloadTransfer;
                }
            }

            return result;
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
