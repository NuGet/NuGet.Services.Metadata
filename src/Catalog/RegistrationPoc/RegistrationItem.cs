using System;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace CollectorSample.RegistrationPoc
{
    public class RegistrationItem
    {
        public string Id { get; }
        public string Version { get; }
        public Uri ContentBaseAddress { get; }
        public bool IsExistingItem { get; }
        public Uri RegistrationBaseAddress { get; }
        public JObject Subject { get; }
        public Uri RegistrationUri { get; }

        public Uri RegistrationVersionUri
        {
            get { return new Uri($"{RegistrationBaseAddress}{Id}/{Version}.json".ToLowerInvariant()); }
        }

        public RegistrationItem(string id, string version, Uri registrationUri, JObject subject, Uri registrationBaseAddress, bool isExistingItem, Uri contentBaseAddress)
        {
            Id = id;
            Version = version;
            RegistrationUri = registrationUri;
            Subject = subject;
            RegistrationBaseAddress = registrationBaseAddress;
            IsExistingItem = isExistingItem;
            ContentBaseAddress = contentBaseAddress;
        }

        public JTokenStorageContent CreateContent(Guid commitId, DateTime commitTimeStamp)
        {
            var packageJsonLdContext = Utils.GetResource("context.Package.json");
            var packageContext = JObject.Parse(packageJsonLdContext);

            packageContext.Add(PropertyNames.SchemaId, RegistrationVersionUri.ToString().ToLowerInvariant());
            packageContext.Add(PropertyNames.SchemaType, new JArray(
                "Package",
                "catalog:Permalink"));

            packageContext.Add(PropertyNames.CatalogEntry, RegistrationUri);
            packageContext.Add(PropertyNames.Listed, Subject["listed"]);
            packageContext.Add(PropertyNames.Published, Subject["published"]);
            packageContext.Add(PropertyNames.PackageContent, $"{ContentBaseAddress}packages/{Id}.{Version}.nupkg".ToLowerInvariant());
            packageContext.Add(PropertyNames.Registration, $"{RegistrationBaseAddress}{Id}/index.json".ToLowerInvariant());

            return new JTokenStorageContent(packageContext, "application/json", "no-store");
        }
    }
}