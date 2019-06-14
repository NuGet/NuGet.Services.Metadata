// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Monitoring.Utility
{
    public class MonitoringCatalogIndexReader : CatalogIndexReader
    {
        private readonly IGalleryDatabaseQueryService _databaseService;
        private readonly StorageFactory _auditingStorageFactory;
        private readonly ILogger<MonitoringCatalogIndexReader> _logger;

        public MonitoringCatalogIndexReader(
            Uri indexUri, 
            CollectorHttpClient httpClient, 
            IGalleryDatabaseQueryService galleryDatabase,
            StorageFactory auditingStorageFactory,
            ITelemetryService telemetryService,
            ILogger<MonitoringCatalogIndexReader> logger) 
            : base(indexUri, httpClient, telemetryService)
        {
            _databaseService = galleryDatabase ?? throw new ArgumentNullException(nameof(galleryDatabase));
            _auditingStorageFactory = auditingStorageFactory ?? throw new ArgumentNullException(nameof(auditingStorageFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CatalogIndexEntry> FindPackageDetailsEntry(PackageIdentity identity, CancellationToken cancellationToken)
        {
            var package = await _databaseService.GetPackageOrNull(identity.Id, identity.Version.ToNormalizedString());
            return await FindEntryAsync(
                identity, 
                "PackageDetails", 
                package.LastEditedDate, 
                cancellationToken);
        }

        public async Task<CatalogIndexEntry> FindPackageDeleteEntry(PackageIdentity identity, CancellationToken cancellationToken)
        {
            var auditEntries = await DeletionAuditEntry.GetAsync(_auditingStorageFactory, cancellationToken, identity, logger: _logger);
            return await FindEntryAsync(
                identity, 
                "PackageDelete", 
                auditEntries.Max(e => e.TimestampUtc).Value, 
                cancellationToken);
        }

        private async Task<CatalogIndexEntry> FindEntryAsync(PackageIdentity identity, string type, DateTime timestamp, CancellationToken cancellationToken)
        {
            var pages = await GetIndexPagesAsync();
            var interner = new StringInterner();
            return await FindEntryAsync(pages, interner, identity, type, timestamp, cancellationToken);
        }

        private async Task<CatalogIndexEntry> FindEntryAsync(SortedList<DateTime, Uri> pageUris, StringInterner interner, PackageIdentity identity, string type, DateTime timestamp, CancellationToken cancellationToken)
        {
            if (!pageUris.Any())
            {
                return null;
            }

            var halfPagesCount = pageUris.Keys.Count() / 2;
            var page = pageUris.ElementAt(halfPagesCount);
            var json = await _httpClient.GetJObjectAsync(page.Value, cancellationToken);
            var items = json["items"];
            var entries = items
                .Select(i => ParseItem(i, interner))
                .Where(i => i.Types.Any(t => t == type));

            if (!entries.Any())
            {
                pageUris.Remove(page.Key);
                return await FindEntryAsync(pageUris, interner, identity, type, timestamp, cancellationToken);
            }

            var findResult = await FindEntryAsync(
                entries.ToDictionary(e => e.CommitTimeStamp, e => e), 
                interner, 
                identity, 
                type, 
                timestamp, 
                cancellationToken);

            if (findResult.Item1 != null || !findResult.Item2.HasValue)
            {
                return findResult.Item1;
            }

            var nextPageUris = findResult.Item2.Value 
                ? pageUris.Skip(halfPagesCount) 
                : pageUris.Take(halfPagesCount);

            var sortedNextPageUris = new SortedList<DateTime, Uri>();
            foreach (var nextPageUri in nextPageUris)
            {
                sortedNextPageUris.Add(nextPageUri.Key, nextPageUri.Value);
            }

            return await FindEntryAsync(
                sortedNextPageUris, 
                interner, 
                identity, 
                type, 
                timestamp, 
                cancellationToken);
        }

        private async Task<Tuple<CatalogIndexEntry, bool?>> FindEntryAsync(IEnumerable<KeyValuePair<DateTime, CatalogIndexEntry>> entries, StringInterner interner, PackageIdentity identity, string type, DateTime timestamp, CancellationToken cancellationToken)
        {
            var sortedEntries = new SortedList<DateTime, CatalogIndexEntry>();
            foreach (var unsortedEntry in entries)
            {
                sortedEntries.Add(unsortedEntry.Key, unsortedEntry.Value);
            }

            var halfEntriesCount = sortedEntries.Count / 2;
            var entry = sortedEntries.ElementAt(halfEntriesCount).Value;
            var entryTimestamp = await PackageTimestampMetadata.FromCatalogEntry(_httpClient, entry);
            if (entryTimestamp.Last > timestamp)
            {
                var nextEntries = sortedEntries.Skip(halfEntriesCount);
                if (!nextEntries.Any())
                {
                    return Tuple.Create<CatalogIndexEntry, bool?>(null, true);
                }

                return await FindEntryAsync(
                    nextEntries, 
                    interner, 
                    identity, 
                    type, 
                    timestamp, 
                    cancellationToken);
            }
            else if (entryTimestamp.Last == timestamp)
            {
                return Tuple.Create<CatalogIndexEntry, bool?>(entry, null);
            }
            else
            {
                var nextEntries = sortedEntries.Take(halfEntriesCount);
                if (!nextEntries.Any())
                {
                    return Tuple.Create<CatalogIndexEntry, bool?>(null, false);
                }

                return await FindEntryAsync(
                    nextEntries, 
                    interner, 
                    identity, 
                    type, 
                    timestamp, 
                    cancellationToken);
            }
        }
    }
}
