// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.RawJsonRegistration.Model
{
    public class RegistrationItem
    {
        public string Id { get; }
        public string Version { get; }
        public JObject Subject { get; }
        public Uri RegistrationUri { get; }
        public Uri RegistrationBaseAddress { get; }
        public Uri ContentBaseAddress { get; }
        public string PackagePath { get; }
        public bool IsExistingItem { get; }
        public Uri RegistrationVersionUri { get; }

        public RegistrationItem(string id, string version, JObject subject, Uri registrationUri, Uri registrationBaseAddress, Uri contentBaseAddress, string packagePath, bool isExistingItem)
        {
            Id = id;
            Version = version;
            Subject = subject;
            RegistrationUri = registrationUri;
            RegistrationBaseAddress = registrationBaseAddress;
            ContentBaseAddress = contentBaseAddress;
            PackagePath = packagePath;
            IsExistingItem = isExistingItem;

            RegistrationVersionUri = new Uri($"{RegistrationBaseAddress}{Id}/{Version}.json".ToLowerInvariant());
        }

        public JTokenStorageContent CreateContent(Guid commitId, DateTime commitTimeStamp)
        {
            var packageContext = new JObject();

            packageContext.Add(PropertyNames.SchemaId, RegistrationVersionUri.ToString().ToLowerInvariant());
            packageContext.Add(PropertyNames.SchemaType, new JArray(
                "Package",
                Schema.DataTypes.Permalink));

            packageContext.Add(PropertyNames.CatalogEntry, RegistrationUri);
            packageContext.Add(PropertyNames.Listed, Subject[PropertyNames.Listed]);
            packageContext.Add(PropertyNames.Published, Subject[PropertyNames.Published]);
            packageContext.Add(PropertyNames.PackageContent, $"{ContentBaseAddress.ToString().TrimEnd('/')}/{PackagePath}".ToLowerInvariant());
            packageContext.Add(PropertyNames.Registration, $"{RegistrationBaseAddress}{Id}/index.json".ToLowerInvariant());

            packageContext.Add(PropertyNames.SchemaContext, JsonLdContext.Package[PropertyNames.SchemaContext]);

            return new JTokenStorageContent(packageContext, ContentTypes.ApplicationJson, "no-store");
        }
    }
}