// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.RawJsonRegistration.Model
{
    public class RegistrationRoot
    {
        public string Id { get; }
        public Uri Uri { get; }
        public List<RegistrationPage> Pages { get; }

        public RegistrationRoot(string id, Uri uri, List<RegistrationPage> pages)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            if (pages.Count == 0)
            {
                throw new ArgumentException("Creating an empty index is not allowed.", nameof(pages));
            }

            Id = id;
            Uri = uri;
            Pages = pages;
        }

        public JTokenStorageContent CreateContent(int partitionSize, Guid commitId, DateTime commitTimeStamp)
        {
            var registrationJsonLdContext = Utils.GetResource("context.Registration.json");

            var registrationContext = JObject.Parse(registrationJsonLdContext);

            registrationContext.Add(PropertyNames.SchemaId, Uri.ToString().ToLowerInvariant());
            registrationContext.Add(PropertyNames.SchemaType, new JArray(
                "catalog:CatalogRoot",
                "PackageRegistration",
                "catalog:Permalink"));

            registrationContext.Add(PropertyNames.CommitId, commitId);
            registrationContext.Add(PropertyNames.CommitTimeStamp, commitTimeStamp);

            if (Pages.Count == 1)
            {
                // Only one page? Then our first page is the index root...
                var page = Pages.First();

                registrationContext.Add(PropertyNames.Count, 1);
                registrationContext.Add(PropertyNames.Items, page.CreateContent(partitionSize, commitId, commitTimeStamp).Content);
                
                registrationContext.Add(PropertyNames.Lower, page.Lower.Version);
                registrationContext.Add(PropertyNames.Upper, page.Upper.Version);
            }
            else
            {
                // Multiple pages? Build an index pointing to pages...
                var pagesContext = new JArray();
                foreach (var page in Pages)
                {
                    var pageContext = new JObject();
                    pageContext.Add(PropertyNames.SchemaId, page.PageUri.ToString().ToLowerInvariant()); // TODO verify correctness
                    pageContext.Add(PropertyNames.SchemaType, "catalog:CatalogPage");

                    pageContext.Add(PropertyNames.CommitId, commitId);
                    pageContext.Add(PropertyNames.CommitTimeStamp, commitTimeStamp);

                    pageContext.Add(PropertyNames.Count, page.Items.Count);
                    pageContext.Add(PropertyNames.Lower, page.Lower.Version);
                    pageContext.Add(PropertyNames.Upper, page.Upper.Version);

                    pagesContext.Add(pageContext);
                }

                registrationContext.Add(PropertyNames.Count, pagesContext.Count);
                registrationContext.Add(PropertyNames.Items, pagesContext);
            }
            
            return new JTokenStorageContent(registrationContext, ContentTypes.ApplicationJson, "no-store");
        }
    }
}