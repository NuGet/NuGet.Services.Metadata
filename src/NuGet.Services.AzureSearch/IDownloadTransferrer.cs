using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.AzureSearch.AuxiliaryFiles;

namespace NuGet.Services.AzureSearch
{
    public interface IDownloadTransferrer
    {
        Task<DownloadTransferResult> GetTransferChangesAsync(DownloadData downloads);

        // TODO: Assumption that all download changes are applied to downloads
        // Add test??
        Task<DownloadTransferResult> GetTransferChangesAsync(
            DownloadData downloads,
            SortedDictionary<string, long> downloadChanges,
            SortedDictionary<string, SortedSet<string>> oldTransfers);
    }
}
