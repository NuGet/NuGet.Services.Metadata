// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using JsonLD.Util;
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

        private object _cachedIndexPagesLock = new object();
        private Task<SortedList<DateTime, Uri>> _cachedIndexPages = null;
        protected async Task<SortedList<DateTime, Uri>> GetIndexPagesAsync()
        {
            return new SortedList<DateTime, Uri>(await GetCachedIndexPagesAsync());
        }

        private Task<SortedList<DateTime, Uri>> GetCachedIndexPagesAsync()
        {
            lock (_cachedIndexPagesLock)
            {
                return _cachedIndexPages = _cachedIndexPages ?? GetIndexPagesInternalAsync();
            }
        }

        private async Task<SortedList<DateTime, Uri>> GetIndexPagesInternalAsync()
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
            while (pageUriBag.TryTake(out var pageUri))
            {
                var newEntries = await GetEntriesAsync(pageUri, interner);
                foreach (var newEntry in newEntries.Values.SelectMany(e => e))
                {
                    entries.Add(newEntry);
                }
            }
        }

        private IDictionary<Uri, Task<SortedList<DateTime, List<CatalogIndexEntry>>>> _cachedEntries = new Dictionary<Uri, Task<SortedList<DateTime, List<CatalogIndexEntry>>>>();
        protected async Task<SortedList<DateTime, List<CatalogIndexEntry>>> GetEntriesAsync(Uri pageUri, StringInterner interner)
        {
            return new SortedList<DateTime, List<CatalogIndexEntry>>(await GetCachedEntriesAsync(pageUri, interner));
        }

        protected Task<SortedList<DateTime, List<CatalogIndexEntry>>> GetCachedEntriesAsync(Uri pageUri, StringInterner interner)
        {
            lock (_cachedEntries)
            {
                return _cachedEntries.TryGetValue(pageUri, out var cachedEntries) 
                    ? cachedEntries 
                    : _cachedEntries[pageUri] = GetEntriesInternalAsync(pageUri, interner);
            }
        }

        private async Task<SortedList<DateTime, List<CatalogIndexEntry>>> GetEntriesInternalAsync(Uri pageUri, StringInterner interner)
        {
            var entries = new SortedList<DateTime, List<CatalogIndexEntry>>();
            var json = await _httpClient.GetJObjectAsync(pageUri);

            foreach (var item in json["items"])
            {
                var entry = ParseItem(item, interner);
                if (entries.TryGetValue(entry.CommitTimeStamp, out var existingEntries))
                {
                    existingEntries.Add(entry);
                }
                else
                {
                    entries.Add(entry.CommitTimeStamp, new List<CatalogIndexEntry> { entry });
                }
            }

            return entries;
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