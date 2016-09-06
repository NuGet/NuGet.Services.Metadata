// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using FrameworkLogger = Microsoft.Extensions.Logging.ILogger;

namespace NuGet.Indexing
{
    public class StorageLoader : ILoader
    {
        private readonly string _containerName;
        private readonly FrameworkLogger _logger;

        private CloudStorageAccount _storageAccount;
        private AzureDirectorySynchronizer _synchronizer;
        private Func<Task<Tuple<CloudStorageAccount, AzureDirectorySynchronizer>>> _createCloud;

        public static async Task<StorageLoader> CreateStorageLoader(Func<Task<Tuple<CloudStorageAccount, AzureDirectorySynchronizer>>> createCloud, string containerName, FrameworkLogger logger)
        {
            var storageLoader = new StorageLoader(createCloud, containerName, logger);
            await storageLoader.Reload();
            return storageLoader;
        }

        private StorageLoader(Func<Task<Tuple<CloudStorageAccount, AzureDirectorySynchronizer>>> createCloud, string containerName, FrameworkLogger logger)
        {
            logger.LogInformation("StorageLoader container: {ContainerName}", containerName);

            _createCloud = createCloud;
            _containerName = containerName;

            _logger = logger;
        }

        public JsonReader GetReader(string name)
        {
            try
            {
                _logger.LogInformation("StorageLoader.GetReader: {ReaderTarget}", name);

                CloudBlobClient client = _storageAccount.CreateCloudBlobClient();
                CloudBlobContainer container = client.GetContainerReference(_containerName);
                CloudBlockBlob blob = container.GetBlockBlobReference(name);
                return new JsonTextReader(new StreamReader(blob.OpenRead()));
            }
            catch (Exception e)
            {
                _logger.LogError($"Exception {e.Message} attempting to load {name}", e);
                throw;
            }
        }

        public async Task Reload()
        {
            var cloudTuple = await _createCloud();
            _storageAccount = cloudTuple.Item1;
            _synchronizer = cloudTuple.Item2;
        }

        public AzureDirectorySynchronizer GetSynchronizer()
        {
            return _synchronizer;
        }
    }
}
