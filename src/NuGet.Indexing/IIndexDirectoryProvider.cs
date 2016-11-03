using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lucene.Net.Store;
using Lucene.Net.Store.Azure;
using Microsoft.WindowsAzure.Storage;
using NuGet.Services.Configuration;

namespace NuGet.Indexing
{
    public interface IIndexDirectoryProvider
    {
        Directory GetDirectory();
        string GetIndexContainerName();
        AzureDirectorySynchronizer GetSynchronizer();

        Task Reload();
    }
}
