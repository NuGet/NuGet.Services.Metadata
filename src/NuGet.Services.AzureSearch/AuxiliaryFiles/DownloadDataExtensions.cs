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
        public static DownloadData ApplyDownloadOverrides(
            this DownloadData originalData,
            IReadOnlyDictionary<string, long> downloadOverrides,
            ILogger logger)
        {
            if (originalData == null)
            {
                throw new ArgumentNullException(nameof(originalData));
            }

            if (downloadOverrides == null)
            {
                throw new ArgumentNullException(nameof(downloadOverrides));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            var downloadOverrides2 = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            if (originalData.TryGetValue("WindowsAzure.Storage", out var oldDownloads))
            {
                downloadOverrides2["WindowsAzure.Storage"] = (int)(oldDownloads.Total * 0.01);
                downloadOverrides2["Azure.Storage.Blobs"] = (int)(oldDownloads.Total * 0.99);
            }
            else
            {
                logger.LogInformation("Couldn't find WindowsAzure.Storage downloads");
            }

            foreach (var x in downloadOverrides2)
            {
                logger.LogInformation("Download override: {Key} {Value}", x.Key, x.Value);
            }

            // Create a copy of the original data and apply overrides as we copy.
            var result = new DownloadData();

            foreach (var downloadData in originalData)
            {
                var packageId = downloadData.Key;

                if (ShouldOverrideDownloads(packageId))
                {
                    logger.LogInformation(
                        "Overriding downloads of package {PackageId} from {Downloads} to {DownloadsOverride}",
                        packageId,
                        originalData.GetDownloadCount(packageId),
                        downloadOverrides2[packageId]);

                    var versions = downloadData.Value.Keys;

                    result.SetDownloadCount(
                        packageId,
                        versions.First(),
                        downloadOverrides2[packageId]);
                }
                else
                {
                    foreach (var versionData in downloadData.Value)
                    {
                        result.SetDownloadCount(downloadData.Key, versionData.Key, versionData.Value);
                    }
                }
            }

            bool ShouldOverrideDownloads(string packageId)
            {
                if (!downloadOverrides2.TryGetValue(packageId, out var downloadOverride))
                {
                    return false;
                }

                // Apply the downloads override only if the package has fewer total downloads.
                // In effect, this removes a package's manual boost once its total downloads exceed the override.
                //if (originalData[packageId].Total >= downloadOverride)
                //{
                //    logger.LogInformation(
                //        "Skipping download override for package {PackageId} as its downloads of {Downloads} are " +
                //        "greater than its override of {DownloadsOverride}",
                //        packageId,
                //        originalData[packageId].Total,
                //        downloadOverride);
                //    return false;
                //}

                return true;
            }

            return result;
        }
    }
}
