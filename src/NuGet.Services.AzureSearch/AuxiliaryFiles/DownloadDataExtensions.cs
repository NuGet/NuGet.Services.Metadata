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
        public static DownloadData WithDownloadChanges(
            this DownloadData originalData,
            IReadOnlyDictionary<string, long> changes)
        {
            if (originalData == null)
            {
                throw new ArgumentNullException(nameof(originalData));
            }

            if (changes == null)
            {
                throw new ArgumentNullException(nameof(changes));
            }

            // Create a copy of the original data and apply overrides as we copy.
            var result = new DownloadData();

            foreach (var downloadData in originalData)
            {
                var packageId = downloadData.Key;

                if (changes.TryGetValue(packageId, out var newDownloads))
                {
                    var versions = downloadData.Value.Keys;

                    result.SetDownloadCount(
                        packageId,
                        versions.First(),
                        newDownloads);
                }
                else
                {
                    foreach (var versionData in downloadData.Value)
                    {
                        result.SetDownloadCount(downloadData.Key, versionData.Key, versionData.Value);
                    }
                }
            }

            return result;
        }
    }
}
