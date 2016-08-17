using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog.Registration;

namespace NuGet.Services.Metadata.Catalog.RawJsonRegistration
{
    public static class RawJsonRegistrationMaker
    {
        public static async Task Process(RegistrationKey registrationKey, IDictionary<string, JObject> newItems, StorageFactory storageFactory, Uri contentBaseAddress, IPackagePathProvider packagePathProvider, int partitionSize, int packageCountThreshold, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("RegistrationMaker2.Process: registrationKey = {0} newItems: {1}", registrationKey, newItems.Count);

            var registration = new RawJsonRegistrationPersistence(storageFactory, registrationKey, partitionSize, packageCountThreshold, contentBaseAddress, packagePathProvider);

            var existing = await registration.Load(cancellationToken);

            Trace.TraceInformation("RegistrationMaker2.Process: existing = {0}", existing.Count);

            var delta = PromoteRegistrationKey(newItems);

            Trace.TraceInformation("RegistrationMaker2.Process: delta = {0}", delta.Count);

            var resulting = Apply(existing, delta);

            Trace.TraceInformation("RegistrationMaker2.Process: resulting = {0}", resulting.Count);

            await registration.Save(resulting, cancellationToken);
        }

        private static IDictionary<RegistrationEntryKey, RawJsonRegistrationCatalogEntry> PromoteRegistrationKey(IDictionary<string, JObject> newItems)
        {
            var promoted = new Dictionary<RegistrationEntryKey, RawJsonRegistrationCatalogEntry>();
            foreach (var newItem in newItems)
            {
                var promotedEntry = RawJsonRegistrationCatalogEntry.Promote(newItem.Key, newItem.Value, isExistingItem: false);

                promoted[promotedEntry.Key] = promotedEntry.Value;
            }

            return promoted;
        }

        private static IDictionary<RegistrationEntryKey, RawJsonRegistrationCatalogEntry> Apply(
            IDictionary<RegistrationEntryKey, RawJsonRegistrationCatalogEntry> existing,
            IDictionary<RegistrationEntryKey, RawJsonRegistrationCatalogEntry> delta)
        {
            IDictionary<RegistrationEntryKey, RawJsonRegistrationCatalogEntry> resulting = new Dictionary<RegistrationEntryKey, RawJsonRegistrationCatalogEntry>();

            foreach (var item in existing)
            {
                if (delta.ContainsKey(item.Key))
                {
                    resulting.Add(item.Key, delta[item.Key]);
                    delta.Remove(item.Key);
                }
                else
                {
                    resulting.Add(item);
                }
            }

            foreach (var item in delta)
            {
                resulting.Add(item);
            }

            return resulting;
        }
    }
}