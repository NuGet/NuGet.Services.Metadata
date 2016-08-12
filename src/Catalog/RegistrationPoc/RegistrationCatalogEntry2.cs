using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Registration;

namespace CollectorSample.RegistrationPoc
{
    public class RegistrationCatalogEntry2
    {
        public RegistrationCatalogEntry2(string id, string version, string registrationUri, JObject subject, bool isExistingItem)
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

        public static KeyValuePair<RegistrationEntryKey, RegistrationCatalogEntry2> Promote(string registrationUri, JObject subject, bool isExistingItem)
        {
            var id = subject["id"].ToString().ToLowerInvariant();
            var version = subject["version"].ToString().ToLowerInvariant();
            var type = Schema.Prefixes.NuGet + subject["@type"].ToString().Replace("nuget:", string.Empty);

            var registrationEntryKey = new RegistrationEntryKey(
                new RegistrationKey(id), version);
            
            var registrationCatalogEntry = IsDelete(type)
                ? null
                : new RegistrationCatalogEntry2(id, version, registrationUri, subject, isExistingItem);

            return new KeyValuePair<RegistrationEntryKey, RegistrationCatalogEntry2>(registrationEntryKey, registrationCatalogEntry);
        }

        static bool IsDelete(string type)
        {
            return type == Schema.DataTypes.CatalogDelete.ToString()
                || type == Schema.DataTypes.PackageDelete.ToString();
        }
    }
}