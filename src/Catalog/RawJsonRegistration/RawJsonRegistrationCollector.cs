// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog.Registration;

namespace NuGet.Services.Metadata.Catalog.RawJsonRegistration
{
    public class RawJsonRegistrationCollector
        : TypedSortingCollector
    {
        private readonly StorageFactory _storageFactory;

        public Uri ContentBaseAddress { get; set; }
        public IPackagePathProvider PackagePathProvider { get; set; }
        public int PartitionSize { get; set; }
        public int PackageCountThreshold { get; set; }

        public RawJsonRegistrationCollector(Uri index, StorageFactory storageFactory, Func<HttpMessageHandler> handlerFunc = null)
            : base(index, new Uri[] { Schema.DataTypes.PackageDetails, Schema.DataTypes.PackageDelete }, handlerFunc)
        {
            _storageFactory = storageFactory;

            ContentBaseAddress = new Uri("http://tempuri.org");
            PackagePathProvider = new PackagesFolderPackagePathProvider();

            PartitionSize = 64;
            PackageCountThreshold = 128;
        }

        protected override Task<IEnumerable<CatalogItemBatch>> CreateBatches(IEnumerable<CatalogItem> catalogItems)
        {
            // Grouping batches by commit is slow if it contains
            // the same package registration id over and over again.
            // This happens when, for example, a package publish "wave"
            // occurs.
            //
            // If one package registration id is part of 20 batches,
            // we'll have to process all registration leafs 20 times.
            // It would be better to process these leafs only once.
            //
            // So let's batch by package registration id here,
            // ensuring we never write a commit timestamp to the cursor
            // that is higher than the last item currently processed.
            //
            // So, group by id, then make sure the batch key is the
            // *lowest*  timestamp of all commits in that batch.
            // This ensures that on retries, we will retry
            // from the correct location (even though we may have
            // a little rework).

            var batches = catalogItems
                .GroupBy(item => GetKey(item.Value))
                .Select(group => new CatalogItemBatch(
                    group.Min(item => item.CommitTimeStamp),
                    group));

            return Task.FromResult(batches);
        }

        private string GetKey(JToken item)
        {
            return item["nuget:id"].ToString();
        }

        protected override Task ProcessTypedBatch(
            KeyValuePair<string, IDictionary<string, JObject>> sortedGraphs, 
            CancellationToken cancellationToken)
        {
            return RawJsonRegistrationMaker.Process(
                new RegistrationKey(sortedGraphs.Key),
                sortedGraphs.Value,
                _storageFactory,
                ContentBaseAddress,
                PackagePathProvider,
                PartitionSize,
                PackageCountThreshold,
                cancellationToken);
        }
    }
}