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
                            new Uri(item.RegistrationUri),
                            item.Subject, 
                            registrationBaseAddress,
                            item.IsExistingItem,
                            contentBaseAddress,
                            packagePathProvider.GetPackagePath(item.Id, item.Version)));
                }

                await writer.Commit(items.First().Id, DateTime.UtcNow, cancellationToken);
            }
        }

        private static async Task SaveSmallRegistration(IStorage storage, Uri registrationBaseAddress, IList<RawJsonRegistrationCatalogEntry> items, int partitionSize, Uri contentBaseAddress, CancellationToken cancellationToken)
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
            var content = new JTokenStorageContent(registrationContext, ContentTypes.ApplicationJson, "no-store");
            await storage.Save(new Uri($"{registrationBaseAddress}{items.First().Id}/index.json"), content, cancellationToken);
        }
        
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