// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using NuGet.Services.Configuration;
using FrameworkLogger = Microsoft.Extensions.Logging.ILogger;

namespace NuGet.Indexing
{
    public class StorageLoader : ILoader
    {
        private readonly FrameworkLogger _logger;

        private readonly ISettingsProvider _settings;

        private string _dataContainerName;
        private CloudStorageAccount _storageAccount;

        public static async Task<StorageLoader> Create(ISettingsProvider settings, FrameworkLogger logger)
        {
            var storageLoader = new StorageLoader(settings, logger);
            await storageLoader.Reload();
            return storageLoader;
        }

        private StorageLoader(ISettingsProvider settings, FrameworkLogger logger)
        {
            _settings = settings;
            _logger = logger;
        }

        public JsonReader GetReader(string name)
        {
            try
            {
                _logger.LogInformation("StorageLoader.GetReader: {ReaderTarget}", name);

                var client = _storageAccount.CreateCloudBlobClient();
                var container = client.GetContainerReference(_dataContainerName);
                var blob = container.GetBlockBlobReference(name);
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
            // Refresh the data container and the primary storage account.
            _dataContainerName = await _settings.GetOrDefault(IndexingSettings.DataContainer, IndexingSettings.DataContainerDefault);
            _storageAccount = CloudStorageAccount.Parse(await _settings.GetOrThrow<string>(IndexingSettings.StoragePrimary));

            _logger.LogInformation("StorageLoader data container: {DataContainerName}", _dataContainerName);
        }
    }
}
