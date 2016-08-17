using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Registration;

namespace NuGet.Services.Metadata.Catalog.RawJsonRegistration
{
    public class RawJsonRegistrationCatalogEntry
    {
        public RawJsonRegistrationCatalogEntry(string id, string version, string registrationUri, JObject subject, bool isExistingItem)
        {
            Id = id;
            Version = version;
            RegistrationUri = registrationUri;
            Subject = subject;
            IsExistingItem = isExistingItem;
        }

        public string RegistrationUri { get; private set; }
        public JObject Subject { get; private set; }
        public bool IsExistingItem { get; private set; }

        public string Id { get; private set; }
        public string Version { get; private set; }

        public static KeyValuePair<RegistrationEntryKey, RawJsonRegistrationCatalogEntry> Promote(string registrationUri, JObject subject, bool isExistingItem)
        {
            var id = subject[PropertyNames.Id].ToString().ToLowerInvariant();
            var version = subject[PropertyNames.Version].ToString().ToLowerInvariant();
            var type = subject[PropertyNames.SchemaType] is JArray
                ? subject[PropertyNames.SchemaType].Select(t => Schema.Prefixes.NuGet + t.ToString().Replace("nuget:", string.Empty))
                : new [] { Schema.Prefixes.NuGet + subject[PropertyNames.SchemaType].ToString().Replace("nuget:", string.Empty) };

            var registrationEntryKey = new RegistrationEntryKey(
                new RegistrationKey(id), version);
            
            var registrationCatalogEntry = IsDelete(type)
                ? null
                : new RawJsonRegistrationCatalogEntry(id, version, registrationUri, subject, isExistingItem);

            return new KeyValuePair<RegistrationEntryKey, RawJsonRegistrationCatalogEntry>(registrationEntryKey, registrationCatalogEntry);
        }

        static bool IsDelete(IEnumerable<string> type)
        {
            return type.Any(t => t == Schema.DataTypes.CatalogDelete.ToString()
                              || t == Schema.DataTypes.PackageDelete.ToString());
        }
    }
}