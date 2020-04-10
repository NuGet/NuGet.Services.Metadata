using System;
using System.Collections.Generic;
using NuGetGallery;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// The result of applying popularity transfers to download data.
    /// </summary>
    public class DownloadTransferResult
    {
        public DownloadTransferResult(
            Dictionary<string, long> downloadChanges,
            SortedDictionary<string, SortedSet<string>> popularityTransfers)
        {
            DownloadChanges = downloadChanges ?? throw new ArgumentNullException(nameof(downloadChanges));
            LatestPopularityTransfers = popularityTransfers ?? throw new ArgumentNullException(nameof(popularityTransfers));
        }

        public Dictionary<string, long> DownloadChanges { get; }

        /// <summary>
        /// The latest popularity transfers data from the gallery database.
        /// </summary>
        public SortedDictionary<string, SortedSet<string>> LatestPopularityTransfers { get; }
    }
}
