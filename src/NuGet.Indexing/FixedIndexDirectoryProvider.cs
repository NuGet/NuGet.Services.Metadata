using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lucene.Net.Store;

namespace NuGet.Indexing
{
    /// <summary>
    /// Stores a directory and an index container name but does not reload them or provide a synchronizer.
    /// </summary>
    public class FixedIndexDirectoryProvider : IIndexDirectoryProvider
    {
        private readonly Directory _directory;
        private readonly string _indexContainerName;

        public FixedIndexDirectoryProvider(Directory directory, string indexContainerName)
        {
            _directory = directory;
            _indexContainerName = indexContainerName;
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
            return null;
        }

        public Task Reload()
        {
            return Task.FromResult(false);
        }
    }
}
