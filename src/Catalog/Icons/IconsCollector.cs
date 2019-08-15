// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Metadata.Catalog.Icons
{
    public class IconsCollector : CommitCollector
    {
        public IconsCollector(
            Uri index,
            ITelemetryService telemetryService,
            Func<HttpMessageHandler> httpHandlerFactory)
            : base(index, telemetryService, httpHandlerFactory)
        {
        }

        protected override Task<IEnumerable<CatalogCommitItemBatch>> CreateBatchesAsync(
            IEnumerable<CatalogCommitItem> catalogItems)
        {
            var maxCommitTimestamp = catalogItems.Max(x => x.CommitTimeStamp);

            return Task.FromResult<IEnumerable<CatalogCommitItemBatch>>(new[]
            {
                new CatalogCommitItemBatch(
                    catalogItems,
                    key: null,
                    commitTimestamp: maxCommitTimestamp),
            });
        }

        protected override Task<bool> OnProcessBatchAsync(CollectorHttpClient client, IEnumerable<CatalogCommitItem> items, JToken context, DateTime commitTimeStamp, bool isLastBatch, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }
    }
}
