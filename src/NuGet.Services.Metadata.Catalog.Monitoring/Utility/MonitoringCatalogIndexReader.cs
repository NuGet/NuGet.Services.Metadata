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

        public async Task<IEnumerable<CatalogIndexEntry>> FindPackageDetailsEntry(PackageIdentity identity, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to fetch PackageDetails catalog entry of {PackageId} {PackageVersion}.",
                identity.Id, identity.Version);

            var package = await _databaseService.GetPackageOrNull(identity.Id, identity.Version.ToNormalizedString());
            if (package == null)
            {
                _logger.LogInformation("Cannot find PackageDetails catalog entry for {PackageId} {PackageVersion} because it does not exist in the database.",
                    identity.Id, identity.Version);

                return null;
            }

            return await FindEntryFromPagesAsync(
                identity, 
                e => e.IsDetails, 
                package.LastEditedDate, 
                cancellationToken);
        }

        public async Task<IEnumerable<CatalogIndexEntry>> FindPackageDeleteEntry(PackageIdentity identity, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to fetch PackageDelete catalog entry of {PackageId} {PackageVersion}.",
                identity.Id, identity.Version);

            var auditEntries = await DeletionAuditEntry.GetAsync(_auditingStorageFactory, cancellationToken, identity, logger: _logger);
            if (auditEntries == null || !auditEntries.Any() || auditEntries.All(e => !e.TimestampUtc.HasValue))
            {
                _logger.LogInformation("Cannot find PackageDetails catalog entry for {PackageId} {PackageVersion} because it does not have any deletion audit records.",
                    identity.Id, identity.Version);

                return null;
            }

            return await FindEntryFromPagesAsync(
                identity,
                e => e.IsDelete, 
                auditEntries.Max(e => e.TimestampUtc).Value, 
                cancellationToken);
        }

        private async Task<IEnumerable<CatalogIndexEntry>> FindEntryFromPagesAsync(PackageIdentity identity, Func<CatalogIndexEntry, bool> isType, DateTime timestamp, CancellationToken cancellationToken)
        {
            var pages = await GetIndexPagesAsync();
            var interner = new StringInterner();
            return await FindEntryFromPagesAsync(pages, interner, identity, isType, timestamp, cancellationToken);
        }

        private async Task<IEnumerable<CatalogIndexEntry>> FindEntryFromPagesAsync(SortedList<DateTime, Uri> pageUris, StringInterner interner, PackageIdentity identity, Func<CatalogIndexEntry, bool> isType, DateTime timestamp, CancellationToken cancellationToken)
        {
            while (pageUris.Any())
            {
                var halfPagesCount = pageUris.Keys.Count() / 2;
                var page = pageUris.ElementAt(halfPagesCount);
                _logger.LogInformation("Searching page {PageUri} for catalog entry for {PackageId} {PackageVersion}",
                    page.Value, identity.Id, identity.Version);

                var entries = (await GetEntriesAsync(page.Value, interner))
                    .Select(g => new KeyValuePair<DateTime, IEnumerable<CatalogIndexEntry>>(g.Key, g.Value.Where(isType)))
                    .Where(g => g.Value.Any());

                pageUris.Remove(page.Key);

                if (!entries.Any())
                {
                    _logger.LogInformation("Page {PageUri} does not have any entries of the desired type of {PackageId} {PackageVersion}!",
                        page.Value, identity.Id, identity.Version);

                    continue;
                }

                var findResult = await FindEntryFromEntriesEnumerableAsync(
                    entries,
                    identity,
                    timestamp);

                if (findResult.Item1 != null)
                {
                    _logger.LogInformation("Found catalog entry for {PackageId} {PackageVersion} on page {PageUri}.",
                        identity.Id, identity.Version, page.Value);

                    return findResult.Item1;
                }

                if (findResult.Item2.HasValue)
                {
                    if (findResult.Item2.Value)
                    {
                        _logger.LogInformation("Catalog entry for {PackageId} {PackageVersion} is on a later page than {PageUri}",
                            identity.Id, identity.Version, page.Value);
                    }
                    else
                    {
                        _logger.LogInformation("Catalog entry for {PackageId} {PackageVersion} is on an earlier page than {PageUri}",
                            identity.Id, identity.Version, page.Value);
                    }

                    RemoveFromSortedList(pageUris, halfPagesCount, findResult.Item2.Value);
                }
            }

            return null;
        }

        private Task<Tuple<IEnumerable<CatalogIndexEntry>, bool?>> FindEntryFromEntriesEnumerableAsync(
            IEnumerable<KeyValuePair<DateTime, IEnumerable<CatalogIndexEntry>>> entries, 
            PackageIdentity identity,
            DateTime timestamp)
        {
            return FindEntryFromEntriesAsync(
                new SortedList<DateTime, IEnumerable<CatalogIndexEntry>>(entries.ToDictionary(e => e.Key, e => e.Value)),
                identity,
                timestamp);
        }

        private async Task<Tuple<IEnumerable<CatalogIndexEntry>, bool?>> FindEntryFromEntriesAsync(
            SortedList<DateTime, IEnumerable<CatalogIndexEntry>> entries, 
            PackageIdentity identity,
            DateTime timestamp)
        {
            bool? isTimestampHigher = null;
            while (entries.Any())
            {
                var halfEntriesCount = entries.Count / 2;
                var middleEntries = entries.ElementAt(halfEntriesCount);
                entries.Remove(middleEntries.Key);
                var timestamps = await Task.WhenAll(middleEntries.Value.Select(e => PackageTimestampMetadata.FromCatalogEntry(_httpClient, e)));
                if (timestamps.Any(t => t.Last == timestamp))
                {
                    // This is the right timestamp--but it might not be the right package.
                    var middleEntriesForPackage = middleEntries.Value.Where(e => e.Id == identity.Id && e.Version == identity.Version);
                    var middleEntriesForPackageTimestamp = await PackageTimestampMetadata.FromCatalogEntries(_httpClient, middleEntriesForPackage);
                    if (middleEntriesForPackageTimestamp.Last == timestamp)
                    {
                        return Tuple.Create<IEnumerable<CatalogIndexEntry>, bool?>(middleEntriesForPackage, null);
                    }

                    isTimestampHigher = null;
                    continue;
                }

                isTimestampHigher = timestamps.Max(t => t.Last) < timestamp;
                RemoveFromSortedList(entries, halfEntriesCount, isTimestampHigher.Value);
            }

            return Tuple.Create<IEnumerable<CatalogIndexEntry>, bool?>(null, isTimestampHigher);
        }

        private void RemoveFromSortedList<TKey, TValue>(
            SortedList<TKey, TValue> list,
            int count,
            bool fromFirst)
        {
            Func<SortedList<TKey, TValue>, KeyValuePair<TKey, TValue>> getNextToRemove;
            if (fromFirst)
            {
                getNextToRemove = l => l.First();
            }
            else
            {
                getNextToRemove = l => l.Last();
            }

            var numRemoved = 0;
            while (numRemoved++ < count && list.Any())
            {
                list.Remove(getNextToRemove(list).Key);
            }
        }
    }
}
