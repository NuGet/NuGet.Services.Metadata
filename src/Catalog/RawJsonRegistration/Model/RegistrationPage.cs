using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Json;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.RawJsonRegistration.Model
{
    public class RegistrationPage
    {
        public Uri PageUri { get; }
        public Dictionary<string, RegistrationItem> Items { get; }
        public RegistrationItem Lower { get; }
        public RegistrationItem Upper { get; }

        public RegistrationPage(Dictionary<string, RegistrationItem> items, bool partitioned)
        {
            if (items.Count == 0)
            {
                throw new ArgumentException("Creating an empty page is not allowed.", nameof(items));
            }

            Lower = items.First().Value;
            Upper = items.Last().Value;

            var registrationBaseAddress = Lower.RegistrationBaseAddress;

            PageUri = partitioned
                ? new Uri($"{registrationBaseAddress}page/{Lower.Version}/{Upper.Version}.json")
                : new Uri($"{registrationBaseAddress}index.json");

            Items = items;
        }

        public JTokenStorageContent CreateContent(int partitionSize, Guid commitId, DateTime commitTimeStamp)
        {
            var id = Lower.Id;
            var registrationBaseAddress = Lower.RegistrationBaseAddress;
            var contentBaseAddress = Lower.ContentBaseAddress;
            
            var registrationJsonLdContext = Utils.GetResource("context.Registration.json");

            var registrationContext = JObject.Parse(registrationJsonLdContext);

            registrationContext.Add(PropertyNames.SchemaId, PageUri.ToString().ToLowerInvariant());
            registrationContext.Add(PropertyNames.SchemaType, "catalog:CatalogPage");

            registrationContext.Add(PropertyNames.CommitId, commitId);
            registrationContext.Add(PropertyNames.CommitTimeStamp, commitTimeStamp);

            var registrationItemsContext = new JArray();
            foreach (var registrationVersion in Items.Values)
            {
                var registrationVersionContext = new JObject();
                registrationVersionContext.Add(PropertyNames.SchemaId, $"{registrationBaseAddress}{id}/index.json".ToLowerInvariant()); // TODO verify correctness
                registrationVersionContext.Add(PropertyNames.SchemaType, "Package");

                registrationVersionContext.Add(PropertyNames.CommitId, commitId);
                registrationVersionContext.Add(PropertyNames.CommitTimeStamp, commitTimeStamp);

                registrationVersionContext.Add(PropertyNames.CatalogEntry, registrationVersion.Subject.FilterClone(
                    new[]
                    {
                        "@id",
                        "@type",
                        "authors",
                        "dependencyGroups",
                        "dependencyGroups[*].*",
                        "description",
                        "iconUrl",
                        "id",
                        "language",
                        "licenseUrl",
                        "listed",
                        "minClientVersion",
                        "projectUrl",
                        "published",
                        "requireLicenseAcceptance",
                        "summary",
                        "tags",
                        "title",
                        "version",
                        "verbatimVersion"
                    }));

                registrationVersionContext.Add("packageContent", $"{contentBaseAddress.ToString().TrimEnd('/')}/{registrationVersion.PackagePath}".ToLowerInvariant()); // TODO
                registrationVersionContext.Add("registration", $"{registrationBaseAddress}{registrationVersion.Id}/index.json".ToLowerInvariant()); // TODO

                registrationItemsContext.Add(registrationVersionContext);
            }
                
            registrationContext.Add("count", registrationItemsContext.Count);
            registrationContext.Add("items", registrationItemsContext);

            return new JTokenStorageContent(registrationContext, ContentTypes.ApplicationJson, "no-store");
        }
    }
}