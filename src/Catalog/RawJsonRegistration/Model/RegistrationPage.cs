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
        private static readonly JArray DefaultTagsArrayValue = new JArray(string.Empty);

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
                ? new Uri($"{registrationBaseAddress}page/{Lower.Version}/{Upper.Version}.json".ToLowerInvariant())
                : new Uri($"{registrationBaseAddress}index.json".ToLowerInvariant());

            Items = items;
        }

        public JTokenStorageContent CreateContent(int partitionSize, Guid commitId, DateTime commitTimeStamp)
        {
            var id = Lower.Id;
            var registrationBaseAddress = Lower.RegistrationBaseAddress;
            var contentBaseAddress = Lower.ContentBaseAddress;
            
            var registrationContext = new JObject();

            registrationContext.Add(PropertyNames.SchemaId, PageUri.ToAbsoluteString());
            registrationContext.Add(PropertyNames.SchemaType, "catalog:CatalogPage");

            registrationContext.Add(PropertyNames.CommitId, commitId);
            registrationContext.Add(PropertyNames.CommitTimeStamp, commitTimeStamp);

            var registrationItemsContext = new JArray();
            foreach (var registrationVersion in Items.Values)
            {
                var packageContentUrl = new Uri($"{contentBaseAddress.ToString().TrimEnd('/')}/{registrationVersion.PackagePath}");

                var registrationVersionContext = new JObject();
                registrationVersionContext.Add(PropertyNames.SchemaId, new Uri($"{registrationBaseAddress}{id}/{registrationVersion.Version}.json").ToAbsoluteString());
                registrationVersionContext.Add(PropertyNames.SchemaType, "Package");

                registrationVersionContext.Add(PropertyNames.CommitId, commitId);
                registrationVersionContext.Add(PropertyNames.CommitTimeStamp, commitTimeStamp);

                // Create catalog entry
                var catalogEntry = new JObject();
                catalogEntry[PropertyNames.SchemaType] = "PackageDetails";
                catalogEntry[PropertyNames.PackageContent] = packageContentUrl.ToAbsoluteString();

                // Copy properties from original entry
                catalogEntry.CopyPropertyFrom(registrationVersion.Subject, "@id");
                catalogEntry.CopyPropertyFrom(registrationVersion.Subject, "authors");
                catalogEntry.CopyPropertyFrom(registrationVersion.Subject, "dependencyGroups");
                catalogEntry.CopyPropertyFrom(registrationVersion.Subject, "description");
                catalogEntry.CopyPropertyFrom(registrationVersion.Subject, "iconUrl", fallbackValue: string.Empty);
                catalogEntry.CopyPropertyFrom(registrationVersion.Subject, "id");
                catalogEntry.CopyPropertyFrom(registrationVersion.Subject, "language", fallbackValue: string.Empty);
                catalogEntry.CopyPropertyFrom(registrationVersion.Subject, "licenseUrl", fallbackValue: string.Empty);
                catalogEntry.CopyPropertyFrom(registrationVersion.Subject, "listed");
                catalogEntry.CopyPropertyFrom(registrationVersion.Subject, "minClientVersion", fallbackValue: string.Empty);
                catalogEntry.CopyPropertyFrom(registrationVersion.Subject, "projectUrl", fallbackValue: string.Empty);
                catalogEntry.CopyPropertyFrom(registrationVersion.Subject, "published");
                catalogEntry.CopyPropertyFrom(registrationVersion.Subject, "requireLicenseAcceptance", fallbackValue: false);
                catalogEntry.CopyPropertyFrom(registrationVersion.Subject, "summary", fallbackValue: string.Empty);
                catalogEntry.CopyPropertyFrom(registrationVersion.Subject, "tags", fallbackValue: DefaultTagsArrayValue);
                catalogEntry.CopyPropertyFrom(registrationVersion.Subject, "title", fallbackValue: string.Empty);
                catalogEntry.CopyPropertyFrom(registrationVersion.Subject, "version");
                
                // Ensure id and version are present
                if (catalogEntry[PropertyNames.Id] == null) catalogEntry[PropertyNames.Id] = id;
                if (catalogEntry[PropertyNames.Version] == null) catalogEntry[PropertyNames.Version] = registrationVersion.Version;

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
                                        new Uri($"{registrationBaseAddress}{dependencyId}/index.json").ToAbsoluteString();
                                }
                            }
                        }
                    }
                }

                registrationVersionContext.Add(PropertyNames.CatalogEntry, catalogEntry);

                registrationVersionContext.Add(PropertyNames.PackageContent, packageContentUrl.ToAbsoluteString());
                registrationVersionContext.Add(PropertyNames.Registration, new Uri($"{registrationBaseAddress}{registrationVersion.Id}/index.json").ToAbsoluteString());

                registrationItemsContext.Add(registrationVersionContext);
            }
                
            registrationContext.Add(PropertyNames.Count, registrationItemsContext.Count);
            registrationContext.Add(PropertyNames.Items, registrationItemsContext);

            registrationContext.Add(PropertyNames.SchemaContext, JsonLdContext.Registration[PropertyNames.SchemaContext]);

            return new JTokenStorageContent(registrationContext, ContentTypes.ApplicationJson, "no-store");
        }
    }
}