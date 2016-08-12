using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Json;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog.Registration;

namespace CollectorSample.RegistrationPoc
{
    public class RegistrationPersistence2
        : IRegistrationPersistence2
    {
        private readonly int _packageCountThreshold;
        private readonly int _partitionSize;
        private readonly RecordingStorage _storage;
        private readonly Uri _registrationBaseAddress;
        private readonly Uri _contentBaseAddress;

        public RegistrationPersistence2(StorageFactory storageFactory, RegistrationKey registrationKey, int partitionSize, int packageCountThreshold, Uri contentBaseAddress)
        {
            _storage = new RecordingStorage(storageFactory.Create(registrationKey.ToString()));
            _packageCountThreshold = packageCountThreshold;
            _partitionSize = partitionSize;
            _registrationBaseAddress = storageFactory.BaseAddress;
            _contentBaseAddress = contentBaseAddress;
        }

        public Task<IDictionary<RegistrationEntryKey, RegistrationCatalogEntry2>> Load(CancellationToken cancellationToken)
        {
            return Load(_storage, cancellationToken);
        }

        public async Task Save(IDictionary<RegistrationEntryKey, RegistrationCatalogEntry2> registration, CancellationToken cancellationToken)
        {
            await Save(_storage, _registrationBaseAddress, registration, _partitionSize, _packageCountThreshold, _contentBaseAddress, cancellationToken);

            await Cleanup(_storage, cancellationToken);
        }
        
        private static async Task<IDictionary<RegistrationEntryKey, RegistrationCatalogEntry2>> Load(IStorage storage, CancellationToken cancellationToken)
        {
            using (var reader = new RegistrationReader(storage))
            {
                Trace.TraceInformation("RegistrationPersistence2.Load: resourceUri = {0}", reader.RootUri);

                var items = await reader.Load(cancellationToken);

                Trace.TraceInformation("RegistrationPersistence2.Load: resources = {0}", items.Count);

                return items;
            }
        }

        private static async Task Save(IStorage storage, Uri registrationBaseAddress, IDictionary<RegistrationEntryKey, RegistrationCatalogEntry2> registration, int partitionSize, int packageCountThreshold, Uri contentBaseAddress, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("RegistrationPersistence2.Save");

            var items = registration.Values
                .Where(v => v != null)
                .ToList();

            if (items.Count == 0)
            {
                return;
            }

            using (var writer = new RegistrationWriter(storage, partitionSize, packageCountThreshold))
            {
                foreach (var item in items)
                {
                    writer.Add(
                        new RegistrationItem(
                            item.Id,
                            item.Version,
                            new Uri(item.RegistrationUri),
                            item.Subject, 
                            registrationBaseAddress, 
                            item.IsExistingItem, 
                            contentBaseAddress));
                }

                await writer.Commit(items.First().Id, DateTime.UtcNow, cancellationToken);
            }
        }

        private static async Task SaveSmallRegistration(IStorage storage, Uri registrationBaseAddress, IList<RegistrationCatalogEntry2> items, int partitionSize, Uri contentBaseAddress, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("RegistrationPersistence2.SaveSmallRegistration");

            if (items.Count == 0)
            {
                return;
            }

            var commitId = Guid.NewGuid();
            var commitTimeStamp = DateTime.UtcNow.ToString("O"); // TODO

          
            // Save index
            var registrationJsonLdContext = Utils.GetResource("context.Registration.json");

            var registrationContext = JObject.Parse(registrationJsonLdContext);

            registrationContext.Add("@id", $"{registrationBaseAddress}{items.First().Id}/index.json".ToLowerInvariant()); // TODO
            registrationContext.Add("@type", new JArray(
                "catalog:CatalogRoot",
                "PackageRegistration",
                "catalog:Permalink"));

            registrationContext.Add("commitId", commitId);
            registrationContext.Add("commitTimeStamp", commitTimeStamp);

            var partitionsContext = new JArray();
            foreach (var partition in items.Paged(partitionSize))
            {
                var partitionContext = new JObject();
                partitionContext.Add("@id", $"{registrationBaseAddress}{items.First().Id}/index.json#page/{partition.First().Version}/{partition.Last().Version}".ToLowerInvariant()); // TODO
                partitionContext.Add("@type", "catalog:CatalogPage");

                partitionContext.Add("commitId", commitId);
                partitionContext.Add("commitTimeStamp", commitTimeStamp);
                
                var partitionItemsContext = new JArray();
                foreach (var registrationVersion in partition)
                {
                    var registrationVersionContext = new JObject();
                    registrationVersionContext.Add("@id", $"{registrationBaseAddress}{registrationVersion.Id}/index.json".ToLowerInvariant()); // TODO
                    registrationVersionContext.Add("@type", "Package");

                    registrationVersionContext.Add("commitId", commitId);
                    registrationVersionContext.Add("commitTimeStamp", commitTimeStamp);

                    registrationVersionContext.Add("catalogEntry", registrationVersion.Subject.FilterClone(
                        new []
                        {
                             "[*].catalogEntry.@id",
                             "[*].catalogEntry.@type",
                             "[*].catalogEntry.authors",
                             "[*].catalogEntry.dependencyGroups",
                             "[*].catalogEntry.dependencyGroups[*].*",
                             "[*].catalogEntry.description",
                             "[*].catalogEntry.iconUrl",
                             "[*].catalogEntry.id",
                             "[*].catalogEntry.language",
                             "[*].catalogEntry.licenseUrl",
                             "[*].catalogEntry.listed",
                             "[*].catalogEntry.minClientVersion",
                             "[*].catalogEntry.projectUrl",
                             "[*].catalogEntry.published",
                             "[*].catalogEntry.requireLicenseAcceptance",
                             "[*].catalogEntry.summary",
                             "[*].catalogEntry.tags",
                             "[*].catalogEntry.title",
                             "[*].catalogEntry.version"
                        }));

                    registrationVersionContext.Add("packageContent", $"{contentBaseAddress}packages/{registrationVersion.Id}.{registrationVersion.Version}.nupkg".ToLowerInvariant()); // TODO
                    registrationVersionContext.Add("registration", $"{registrationBaseAddress}{registrationVersion.Id}/index.json".ToLowerInvariant()); // TODO

                    partitionItemsContext.Add(registrationVersionContext);
                }

                partitionContext.Add("count", partitionItemsContext.Count);
                partitionContext.Add("items", partitionItemsContext);
                partitionContext.Add("lower", partition.First().Version);
                partitionContext.Add("upper", partition.Last().Version);
                partitionContext.Add("parent", $"{registrationBaseAddress}{items.First().Id}/index.json".ToLowerInvariant());

                partitionsContext.Add(partitionContext);
            }

            registrationContext.Add("count", partitionsContext.Count);
            registrationContext.Add("items", partitionsContext);

            // Save index
            var content = new JTokenStorageContent(registrationContext, "application/json", "no-store");
            await storage.Save(new Uri($"{registrationBaseAddress}{items.First().Id}/index.json"), content, cancellationToken);
        }

        //private static async Task SaveLargeRegistration(IStorage storage, Uri registrationBaseAddress, IList<RegistrationCatalogEntry2> items, int partitionSize, Uri contentBaseAddress, CancellationToken cancellationToken)
        //{
        //    Trace.TraceInformation("RegistrationPersistence2.SaveLargeRegistration: registrationBaseAddress = {0} items: {1}", registrationBaseAddress, items.Count);

        //    IList<Uri> cleanUpList = new List<Uri>();

        //    await SaveRegistration(storage, registrationBaseAddress, items, cleanUpList, null, partitionSize, contentBaseAddress, cancellationToken);
        //}

        //private static async Task SaveRegistration(IStorage storage, Uri registrationBaseAddress, IList<RegistrationCatalogEntry2> items, IList<Uri> cleanUpList, SingleGraphPersistence graphPersistence, int partitionSize, Uri contentBaseAddress, CancellationToken cancellationToken)
        //{
        //    Trace.TraceInformation("RegistrationPersistence2.SaveRegistration: registrationBaseAddress = {0} items: {1}", registrationBaseAddress, items.Count);

        //    using (RegistrationMakerCatalogWriter writer = new RegistrationMakerCatalogWriter(storage, partitionSize, cleanUpList, graphPersistence))
        //    {
        //        foreach (var item in items)
        //        {
        //            writer.Add(new RegistrationMakerCatalogItem(new Uri(item.ResourceUri), item.Graph, registrationBaseAddress, item.IsExistingItem, contentBaseAddress));
        //        }
        //        await writer.Commit(DateTime.UtcNow, null, cancellationToken);
        //    }
        //}

        private static async Task Cleanup(RecordingStorage storage, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("RegistrationPersistence2.Cleanup");

            IList<Task> tasks = new List<Task>();
            foreach (Uri loaded in storage.Loaded)
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