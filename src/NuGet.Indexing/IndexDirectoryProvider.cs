using System.Threading.Tasks;
using Lucene.Net.Store;
using Lucene.Net.Store.Azure;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using NuGet.Services.Configuration;
using FrameworkLogger = Microsoft.Extensions.Logging.ILogger;

namespace NuGet.Indexing
{
    public class IndexDirectoryProvider : IIndexDirectoryProvider
    {
        private readonly FrameworkLogger _logger;

        private readonly ISettingsProvider _settings;

        private Directory _directory;
        private string _indexContainerName;
        private AzureDirectorySynchronizer _synchronizer;

        public static async Task<IndexDirectoryProvider> Create(ISettingsProvider settings, FrameworkLogger logger)
        {
            var indexSynchronizer = new IndexDirectoryProvider(settings, logger);
            await indexSynchronizer.Reload();
            return indexSynchronizer;
        }

        protected IndexDirectoryProvider(ISettingsProvider settings, FrameworkLogger logger)
        {
            _logger = logger;
            _settings = settings;
        }

        public Directory GetDirectory()
        {
            return _directory;
        }

        public string GetIndexContainerName()
        {
            return _indexContainerName;
        }

        public AzureDirectorySynchronizer GetSynchronizer()
        {
            return _synchronizer;
        }

        public async Task Reload()
        {
            // Refresh the primary storage account.
            var storageAccount = CloudStorageAccount.Parse(await _settings.GetOrThrow<string>("Storage.Primary"));

            // If we don't have a directory or the index container has changed, create a new AzureDirectorySynchronizer.
            // Otherwise, don't refresh the AzureDirectorySynchronizer because we don't want to reload the index unless necessary.
            var newIndexContainerName = await _settings.GetOrDefault("Search.IndexContainer", "ng-search-index");
            if (_directory == null || newIndexContainerName != _indexContainerName)
            {
                _indexContainerName = newIndexContainerName;
                _logger.LogInformation("StorageLoader index container: {IndexContainerName}", _indexContainerName);

                var sourceDirectory = new AzureDirectory(storageAccount, _indexContainerName);
                _directory = new RAMDirectory(sourceDirectory); // Copy the directory from Azure storage to RAM.

                _synchronizer = new AzureDirectorySynchronizer(sourceDirectory, _directory);
            }
        }
    }
}
