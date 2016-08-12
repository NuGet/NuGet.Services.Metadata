using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog.Registration;

namespace CollectorSample.RegistrationPoc
{
    public static class RegistrationMaker2
    {
        public static async Task Process(RegistrationKey registrationKey, IDictionary<string, JObject> newItems, StorageFactory storageFactory, Uri contentBaseAddress, int partitionSize, int packageCountThreshold, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("RegistrationMaker2.Process: registrationKey = {0} newItems: {1}", registrationKey, newItems.Count);

            var registration = new RegistrationPersistence2(storageFactory, registrationKey, partitionSize, packageCountThreshold, contentBaseAddress);

            var existing = await registration.Load(cancellationToken);

            Trace.TraceInformation("RegistrationMaker2.Process: existing = {0}", existing.Count);

            var delta = PromoteRegistrationKey(newItems);

            Trace.TraceInformation("RegistrationMaker2.Process: delta = {0}", delta.Count);

            var resulting = Apply(existing, delta);

            Trace.TraceInformation("RegistrationMaker2.Process: resulting = {0}", resulting.Count);

            await registration.Save(resulting, cancellationToken);
        }

        private static IDictionary<RegistrationEntryKey, RegistrationCatalogEntry2> PromoteRegistrationKey(IDictionary<string, JObject> newItems)
        {
            var promoted = new Dictionary<RegistrationEntryKey, RegistrationCatalogEntry2>();
            foreach (var newItem in newItems)
            {
                var promotedEntry = RegistrationCatalogEntry2.Promote(newItem.Key, newItem.Value, isExistingItem: false);

                promoted[promotedEntry.Key] = promotedEntry.Value;
            }

            return promoted;
        }

        private static IDictionary<RegistrationEntryKey, RegistrationCatalogEntry2> Apply(
            IDictionary<RegistrationEntryKey, RegistrationCatalogEntry2> existing,
            IDictionary<RegistrationEntryKey, RegistrationCatalogEntry2> delta)
        {
            IDictionary<RegistrationEntryKey, RegistrationCatalogEntry2> resulting = new Dictionary<RegistrationEntryKey, RegistrationCatalogEntry2>();

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