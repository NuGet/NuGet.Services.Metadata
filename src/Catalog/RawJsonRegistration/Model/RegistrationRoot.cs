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
            var registrationContext = new JObject();

            registrationContext.Add(PropertyNames.SchemaId, Uri.ToAbsoluteString());
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

                // Partition items
                JArray items = new JArray();
                foreach (var partition in page.Items.Paged(partitionSize))
                {
                    var partitionedPage = new RegistrationPage(
                        partition.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                        partitioningType: PartitioningType.Partition);

                    var partitionContent = partitionedPage.CreateContent(commitId, commitTimeStamp).Content;
                    if (partitionContent[PropertyNames.SchemaContext] != null)
                    {
                        ((JObject)partitionContent).Property(PropertyNames.SchemaContext).Remove(); // drop "@context"
                    }

                    partitionContent[PropertyNames.SchemaId] = new Uri($"{partitionedPage.Lower.RegistrationBaseAddress}{partitionedPage.Lower.Id}/index.json#page/{partitionedPage.Lower.Version}/{partitionedPage.Upper.Version}").ToAbsoluteString();
                    partitionContent[PropertyNames.SchemaType] = "catalog:CatalogPage";
                    partitionContent[PropertyNames.Parent] = new Uri($"{partitionedPage.Lower.RegistrationBaseAddress}{partitionedPage.Lower.Id}/index.json").ToAbsoluteString();
                    partitionContent[PropertyNames.Lower] = partitionedPage.Lower.Version;
                    partitionContent[PropertyNames.Upper] = partitionedPage.Upper.Version;

                    items.Add(partitionContent);
                }

                //var itemsContent = page.CreateContent(commitId, commitTimeStamp).Content;
                //if (itemsContent[PropertyNames.SchemaContext] != null) {
                //    ((JObject)itemsContent).Property(PropertyNames.SchemaContext).Remove(); // drop "@context"
                //}

                //itemsContent[PropertyNames.SchemaId] = new Uri($"{page.Lower.RegistrationBaseAddress}{page.Lower.Id}/index.json#page/{page.Lower.Version}/{page.Upper.Version}").ToAbsoluteString();
                //itemsContent[PropertyNames.SchemaType] = "catalog:CatalogPage";
                //itemsContent[PropertyNames.Parent] = new Uri($"{page.Lower.RegistrationBaseAddress}{page.Lower.Id}/index.json").ToAbsoluteString();
                //itemsContent[PropertyNames.Lower] = page.Lower.Version;
                //itemsContent[PropertyNames.Upper] = page.Upper.Version;

                registrationContext.Add(PropertyNames.Count, items.Count);
                registrationContext.Add(PropertyNames.Items, items);
            }
            else
            {
                // Multiple pages? Build an index pointing to pages...
                var pagesContext = new JArray();
                foreach (var page in Pages)
                {
                    var pageContext = new JObject();
                    pageContext.Add(PropertyNames.SchemaId, page.PageUri.ToAbsoluteString());
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

            registrationContext.Add(PropertyNames.SchemaContext, JsonLdContext.Registration[PropertyNames.SchemaContext]);

            return new JTokenStorageContent(registrationContext, ContentTypes.ApplicationJson, "no-store");
        }
    }
}