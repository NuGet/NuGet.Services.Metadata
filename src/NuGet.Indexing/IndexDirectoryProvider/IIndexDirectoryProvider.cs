using System.Threading.Tasks;
using Lucene.Net.Store;

namespace NuGet.Indexing.IndexDirectoryProvider
{
    public interface IIndexDirectoryProvider
    {
        Directory GetDirectory();
        string GetIndexContainerName();
        AzureDirectorySynchronizer GetSynchronizer();

        /// <summary>
        /// Reloads the index directory.
        /// </summary>
        /// <returns>Returns true if the index directory has changed.</returns>
        Task<bool> Reload();
    }
}
