// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog.RawJsonRegistration.Model;
using NuGet.Versioning;

namespace NuGet.Services.Metadata.Catalog.RawJsonRegistration
{
    public class RawJsonRegistrationWriter
        : IDisposable
    {
        private readonly List<RegistrationItem> _batch;
        private bool _open;

        public RawJsonRegistrationWriter(IStorage storage, int partitionSize, int packageCountThreshold)
        {
            Storage = storage;
            PartitionSize = partitionSize;
            PackageCountThreshold = packageCountThreshold;
            RootUri = Storage.ResolveUri("index.json");

            _batch = new List<RegistrationItem>();
            _open = true;
        }

        public void Dispose()
        {
            _batch.Clear();
            _open = false;
        }

        public IStorage Storage { get; }
        public int PartitionSize { get; }
        public int PackageCountThreshold { get; }
        public Uri RootUri { get; }

        public void Add(RegistrationItem item)
        {
            if (!_open)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            _batch.Add(item);
        }

        public virtual async Task<IEnumerable<Uri>> Commit(string id, DateTime commitTimeStamp, CancellationToken cancellationToken)
        {
            if (!_open)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            if (_batch.Count == 0)
            {
                return Enumerable.Empty<Uri>();
            }

            // The commitId is only used for tracing and trouble shooting
            var commitId = Guid.NewGuid();

            // Save individual items
            var itemEntries = await SaveItems(commitId, commitTimeStamp, cancellationToken);

            // Save pages
            var pageEntries = await SavePages(commitId, commitTimeStamp, itemEntries, PackageCountThreshold, PartitionSize, cancellationToken);

            // Save index root
            await SaveRoot(id, commitId, commitTimeStamp, pageEntries, PartitionSize, cancellationToken);

            // Clear
            _batch.Clear();

            return itemEntries.Keys.Select(s => new Uri(s));
        }

        private async Task<IDictionary<string, RegistrationItem>> SaveItems(Guid commitId, DateTime commitTimeStamp, CancellationToken cancellationToken)
        {
            var items = new ConcurrentDictionary<string, RegistrationItem>();

            var batchIndex = 0;

            var saveTasks = new List<Task>();
            foreach (var item in _batch)
            {
                ResourceSaveOperation saveOperationForItem = null;

                try
                {
                    saveOperationForItem = CreateSaveOperationForItem(Storage, item, commitId, commitTimeStamp, cancellationToken);
                    if (saveOperationForItem.SaveTask != null)
                    {
                        saveTasks.Add(saveOperationForItem.SaveTask);
                    }
                    
                    if (!items.TryAdd(saveOperationForItem.ResourceUri.AbsoluteUri, item))
                    {
                        throw new Exception("Duplicate page: " + saveOperationForItem.ResourceUri.AbsoluteUri);
                    }

                    batchIndex++;
                }
                catch (Exception ex)
                {
                    var message = saveOperationForItem?.ResourceUri == null
                        ? string.Format("Batch index: {0}", batchIndex)
                        : string.Format("Batch index: {0}; resourceUri: {1}", batchIndex, saveOperationForItem.ResourceUri);

                    throw new Exception(message, ex);
                }
            }

            await Task.WhenAll(saveTasks);

            return items;
        }

        private async Task<IDictionary<string, RegistrationPage>> SavePages(Guid commitId, DateTime commitTimeStamp, IDictionary<string, RegistrationItem> itemEntries, int packageCountThreshold, int partitionSize, CancellationToken cancellationToken)
        {
            var pages = new ConcurrentDictionary<string, RegistrationPage>();

            var batchIndex = 0;

            if (itemEntries.Count < packageCountThreshold)
            {
                // When we are within the range of one page, create the page but do not store it.
                // The registration index will simply embed the page in this case.
                var registrationPage = new RegistrationPage(
                    ConvertToVersionOrderedDictionary(itemEntries), partitioned: false);

                if (!pages.TryAdd(registrationPage.PageUri.AbsoluteUri, registrationPage))
                {
                    throw new Exception("Could not add page: " + registrationPage.PageUri.AbsoluteUri);
                }
            }
            else
            {
                // When we need multiple pages, create them all and store them.
                // The registration index will contain references to the various pages.
                var saveTasks = new List<Task>();
                foreach (var pageOfItemEntries in itemEntries.Paged(partitionSize))
                {
                    var registrationPage = new RegistrationPage(
                        ConvertToVersionOrderedDictionary(pageOfItemEntries), partitioned: true);

                    ResourceSaveOperation saveOperationForItem = null;

                    try
                    {
                        saveOperationForItem = CreateSaveOperationForPage(Storage, registrationPage, partitionSize, commitId, commitTimeStamp, cancellationToken);
                        if (saveOperationForItem.SaveTask != null)
                        {
                            saveTasks.Add(saveOperationForItem.SaveTask);
                        }

                        if (!pages.TryAdd(saveOperationForItem.ResourceUri.AbsoluteUri, registrationPage))
                        {
                            throw new Exception("Duplicate page: " + saveOperationForItem.ResourceUri.AbsoluteUri);
                        }

                        batchIndex++;
                    }
                    catch (Exception ex)
                    {
                        var message = saveOperationForItem?.ResourceUri == null
                            ? string.Format("Batch index: {0}", batchIndex)
                            : string.Format("Batch index: {0}; resourceUri: {1}", batchIndex, saveOperationForItem.ResourceUri);

                        throw new Exception(message, ex);
                    }
                }

                await Task.WhenAll(saveTasks);
            }
            
            return pages;
        }

        private async Task SaveRoot(string id, Guid commitId, DateTime commitTimeStamp, IDictionary<string, RegistrationPage> pageEntries, int partitionSize, CancellationToken cancellationToken)
        {
            var entries = pageEntries.Values.ToList();
            var registrationRoot = new RegistrationRoot(id, RootUri, entries);
            await Storage.Save(RootUri, registrationRoot.CreateContent(partitionSize, commitId, commitTimeStamp), cancellationToken);
        }

        private ResourceSaveOperation CreateSaveOperationForItem(IStorage storage, RegistrationItem item, Guid commitId, DateTime commitTimeStamp, CancellationToken cancellationToken)
        {
            var saveOperation = new ResourceSaveOperation();
            saveOperation.ResourceUri = item.RegistrationVersionUri;

            if (!item.IsExistingItem && item.Subject != null)
            {
                saveOperation.SaveTask = storage.Save(
                    item.RegistrationVersionUri, 
                    item.CreateContent(commitId, commitTimeStamp), cancellationToken);
            }
            else
            {
                Trace.WriteLine(string.Format("Resource {0} already exists. Skipping.", item.RegistrationVersionUri), "Debug");
            }

            return saveOperation;
        }

        private ResourceSaveOperation CreateSaveOperationForPage(IStorage storage, RegistrationPage page, int partitionSize, Guid commitId, DateTime commitTimeStamp, CancellationToken cancellationToken)
        {
            var saveOperation = new ResourceSaveOperation();
            saveOperation.ResourceUri = page.PageUri;
            saveOperation.SaveTask = storage.Save(saveOperation.ResourceUri, page.CreateContent(partitionSize, commitId, commitTimeStamp), cancellationToken);

            return saveOperation;
        }

        private static Dictionary<string, RegistrationItem> ConvertToVersionOrderedDictionary(IEnumerable<KeyValuePair<string, RegistrationItem>> itemEntries)
        {
            var mappedPageOfItemEntries = new Dictionary<string, RegistrationItem>();

            foreach (var pageOfItemEntry in itemEntries.OrderBy(entry => NuGetVersion.Parse(entry.Value.Version)))
            {
                mappedPageOfItemEntries.Add(pageOfItemEntry.Key, pageOfItemEntry.Value);
            }

            return mappedPageOfItemEntries;
        }
    }
}