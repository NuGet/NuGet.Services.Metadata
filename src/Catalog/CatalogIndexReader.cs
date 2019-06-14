// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Services.Metadata.Catalog
{
    public class CatalogIndexReader
    {
        protected readonly Uri _indexUri;
        protected readonly CollectorHttpClient _httpClient;
        protected readonly ITelemetryService _telemetryService;

        public CatalogIndexReader(Uri indexUri, CollectorHttpClient httpClient, ITelemetryService telemetryService)
        {
            _indexUri = indexUri;
            _httpClient = httpClient;
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
        }

        public async Task<IEnumerable<CatalogIndexEntry>> GetEntries()
        {
            var pages = await GetIndexPagesAsync();
            return await GetEntriesAsync(pages.Select(p => p.Value));
        }

        protected async Task<SortedList<DateTime, Uri>> GetIndexPagesAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            JObject index = await _httpClient.GetJObjectAsync(_indexUri);
            _telemetryService.TrackCatalogIndexReadDuration(stopwatch.Elapsed, _indexUri);

            var pages = new SortedList<DateTime, Uri>();

            foreach (var item in index["items"])
            {
                pages.Add(
                    DateTime.Parse(item["commitTimeStamp"].ToString()),
                    new Uri(item["@id"].ToString()));
            }

            return pages;
        }

        private async Task<ConcurrentBag<CatalogIndexEntry>> GetEntriesAsync(IEnumerable<Uri> pageUris)
        {
            var pageUriBag = new ConcurrentBag<Uri>(pageUris);
            var entries = new ConcurrentBag<CatalogIndexEntry>();
            var interner = new StringInterner();

            var tasks = Enumerable
                .Range(0, ServicePointManager.DefaultConnectionLimit)
                .Select(i => ProcessPageUris(pageUriBag, entries, interner))
                .ToList();

            await Task.WhenAll(tasks);

            return entries;
        }

        private async Task ProcessPageUris(ConcurrentBag<Uri> pageUriBag, ConcurrentBag<CatalogIndexEntry> entries, StringInterner interner)
        {
            await Task.Yield();
            Uri pageUri;
            while (pageUriBag.TryTake(out pageUri))
            {
                var json = await _httpClient.GetJObjectAsync(pageUri);

                foreach (var item in json["items"])
                {
                    entries.Add(ParseItem(item, interner));
                }
            }
        }

        protected CatalogIndexEntry ParseItem(JToken item, StringInterner interner)
        {
            // This string is unique.
            var id = item["@id"].ToString();

            // These strings should be shared.
            var type = interner.Intern(item["@type"].ToString());
            var commitId = interner.Intern(item["commitId"].ToString());
            var nugetId = interner.Intern(item["nuget:id"].ToString());
            var nugetVersion = interner.Intern(item["nuget:version"].ToString());
            var packageIdentity = new PackageIdentity(nugetId, NuGetVersion.Parse(nugetVersion));

            // No string is directly operated on here.
            var commitTimeStamp = item["commitTimeStamp"].ToObject<DateTime>();

            return new CatalogIndexEntry(
                new Uri(id),
                type,
                commitId,
                commitTimeStamp,
                packageIdentity);
        }
    }
}