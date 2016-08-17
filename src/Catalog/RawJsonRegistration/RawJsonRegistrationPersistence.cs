// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Json;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog.RawJsonRegistration.Model;
using NuGet.Services.Metadata.Catalog.Registration;

namespace NuGet.Services.Metadata.Catalog.RawJsonRegistration
{
    public class RawJsonRegistrationPersistence
        : IRawJsonRegistrationPersistence
    {
        private readonly int _packageCountThreshold;
        private readonly int _partitionSize;
        private readonly RecordingStorage _storage;
        private readonly Uri _registrationBaseAddress;
        private readonly Uri _contentBaseAddress;
        private readonly IPackagePathProvider _packagePathProvider;

        public RawJsonRegistrationPersistence(StorageFactory storageFactory, RegistrationKey registrationKey, int partitionSize, int packageCountThreshold, Uri contentBaseAddress, IPackagePathProvider packagePathProvider)
        {
            _storage = new RecordingStorage(storageFactory.Create(registrationKey.ToString()));
            _packageCountThreshold = packageCountThreshold;
            _partitionSize = partitionSize;
            _registrationBaseAddress = storageFactory.BaseAddress;
            _contentBaseAddress = contentBaseAddress;
            _packagePathProvider = packagePathProvider;
        }

        public Task<IDictionary<RegistrationEntryKey, RawJsonRegistrationCatalogEntry>> Load(CancellationToken cancellationToken)
        {
            return Load(_storage, cancellationToken);
        }

        public async Task Save(IDictionary<RegistrationEntryKey, RawJsonRegistrationCatalogEntry> registration, CancellationToken cancellationToken)
        {
            await Save(_storage, _registrationBaseAddress, registration, _partitionSize, _packageCountThreshold, _contentBaseAddress, _packagePathProvider, cancellationToken);

            await Cleanup(_storage, cancellationToken);
        }
        
        private static async Task<IDictionary<RegistrationEntryKey, RawJsonRegistrationCatalogEntry>> Load(IStorage storage, CancellationToken cancellationToken)
        {
            using (var reader = new RawJsonRegistrationReader(storage))
            {
                Trace.TraceInformation("RegistrationPersistence2.Load: resourceUri = {0}", reader.RootUri);

                var items = await reader.Load(cancellationToken);

                Trace.TraceInformation("RegistrationPersistence2.Load: resources = {0}", items.Count);

                return items;
            }
        }

        private static async Task Save(IStorage storage, Uri registrationBaseAddress, IDictionary<RegistrationEntryKey, RawJsonRegistrationCatalogEntry> registration, int partitionSize, int packageCountThreshold, Uri contentBaseAddress, IPackagePathProvider packagePathProvider, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("RegistrationPersistence2.Save");

            var items = registration.Values
                .Where(v => v != null)
                .ToList();

            if (items.Count == 0)
            {
                return;
            }

            using (var writer = new RawJsonRegistrationWriter(storage, partitionSize, packageCountThreshold))
            {
                foreach (var item in items)
                {
                    writer.Add(
                        new RegistrationItem(
                            item.Id,
                            item.Version,
                            item.Subject, 
                            new Uri(item.RegistrationUri),
                            registrationBaseAddress,
                            contentBaseAddress,
                            packagePathProvider.GetPackagePath(item.Id, item.Version), item.IsExistingItem));
                }

                await writer.Commit(items.First().Id, DateTime.UtcNow, cancellationToken);
            }
        }

        private static async Task Cleanup(RecordingStorage storage, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("RegistrationPersistence2.Cleanup");

            IList<Task> tasks = new List<Task>();
            foreach (var loaded in storage.Loaded)
            {
                if (!storage.Saved.Contains(loaded))
                {
                    tasks.Add(storage.Delete(loaded, cancellationToken));
                }
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks.ToArray());
            }
        }
    }
}