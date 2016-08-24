// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
            
            var registrationContext = new JObject();

            registrationContext.Add(PropertyNames.SchemaId, PageUri.ToString().ToLowerInvariant());
            registrationContext.Add(PropertyNames.SchemaType, "catalog:CatalogPage");

            registrationContext.Add(PropertyNames.CommitId, commitId);
            registrationContext.Add(PropertyNames.CommitTimeStamp, commitTimeStamp);

            var registrationItemsContext = new JArray();
            foreach (var registrationVersion in Items.Values)
            {
                var packageContentUrl = $"{contentBaseAddress.ToString().TrimEnd('/')}/{registrationVersion.PackagePath}".ToLowerInvariant();

                var registrationVersionContext = new JObject();
                registrationVersionContext.Add(PropertyNames.SchemaId, $"{registrationBaseAddress}{id}/{registrationVersion.Version}.json".ToLowerInvariant());
                registrationVersionContext.Add(PropertyNames.SchemaType, "Package");

                registrationVersionContext.Add(PropertyNames.CommitId, commitId);
                registrationVersionContext.Add(PropertyNames.CommitTimeStamp, commitTimeStamp);

                // Copy catalog entry
                var catalogEntry = registrationVersion.Subject.FilterClone(
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
                        "version"
                    });

                // Update catalog entry
                catalogEntry[PropertyNames.SchemaType] = "PackageDetails";
                catalogEntry[PropertyNames.PackageContent] = packageContentUrl;

                // Ensure id and version are present
                if (catalogEntry[PropertyNames.Id] == null) catalogEntry[PropertyNames.Id] = id;
                if (catalogEntry[PropertyNames.Version] == null) catalogEntry[PropertyNames.Version] = registrationVersion.Version;

                // Ensure default values are present
                // TODO: Check if this is needed for the client or not. If not, remove the following checks and assignments as they are ballast...
                if (catalogEntry["iconUrl"] == null) catalogEntry["iconUrl"] = string.Empty;
                if (catalogEntry["licenseUrl"] == null) catalogEntry["licenseUrl"] = string.Empty;
                if (catalogEntry["projectUrl"] == null) catalogEntry["projectUrl"] = string.Empty;
                if (catalogEntry["minClientVersion"] == null) catalogEntry["minClientVersion"] = string.Empty;
                if (catalogEntry["language"] == null) catalogEntry["language"] = string.Empty;
                if (catalogEntry["summary"] == null) catalogEntry["summary"] = string.Empty;
                if (catalogEntry["title"] == null) catalogEntry["title"] = string.Empty;
                if (catalogEntry["tags"] == null) catalogEntry["tags"] = new JArray(string.Empty);
                if (catalogEntry["requireLicenseAcceptance"] == null) catalogEntry["requireLicenseAcceptance"] = false;

                // Loop dependency groups and make sure they all have a registration URL property
                var dependencyGroupsEntry = catalogEntry["dependencyGroups"] as JArray;
                if (dependencyGroupsEntry != null)
                {
                    foreach (var dependencyGroupEntry in dependencyGroupsEntry)
                    {
                        var dependenciesEntry = dependencyGroupEntry["dependencies"] as JArray;
                        if (dependenciesEntry != null)
                        {
                            foreach (var dependencyEntry in dependenciesEntry)
                            {
                                var dependencyId = dependencyEntry[PropertyNames.Id];
                                if (dependencyId != null)
                                {
                                    dependencyEntry[PropertyNames.Registration] =
                                        $"{registrationBaseAddress}{dependencyId}/index.json".ToLowerInvariant();
                                }
                            }
                        }
                    }
                }

                registrationVersionContext.Add(PropertyNames.CatalogEntry, catalogEntry);

                registrationVersionContext.Add(PropertyNames.PackageContent, packageContentUrl);
                registrationVersionContext.Add(PropertyNames.Registration, $"{registrationBaseAddress}{registrationVersion.Id}/index.json".ToLowerInvariant());

                registrationItemsContext.Add(registrationVersionContext);
            }
                
            registrationContext.Add(PropertyNames.Count, registrationItemsContext.Count);
            registrationContext.Add(PropertyNames.Items, registrationItemsContext);

            registrationContext.Add(PropertyNames.SchemaContext, JsonLdContext.Registration[PropertyNames.SchemaContext]);

            return new JTokenStorageContent(registrationContext, ContentTypes.ApplicationJson, "no-store");
        }
    }
}