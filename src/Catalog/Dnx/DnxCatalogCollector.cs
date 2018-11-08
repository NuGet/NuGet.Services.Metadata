// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;

namespace NuGet.Services.Metadata.Catalog.Dnx
{
    public class DnxCatalogCollector : CommitCollector
    {
        // This is simply the arbitrary limit that I tested.  There may be a better value.
        public const int DefaultMaxConcurrentBatches = 10;

        private readonly StorageFactory _storageFactory;
        private readonly IAzureStorage _sourceStorage;
        private readonly DnxMaker _dnxMaker;
        private readonly ILogger _logger;
        private readonly int _maxDegreeOfParallelism;
        private readonly Uri _contentBaseAddress;
        private readonly int _maxConcurrentBatches;

        public DnxCatalogCollector(
            Uri index,
            StorageFactory storageFactory,
            IAzureStorage preferredPackageSourceStorage,
            Uri contentBaseAddress,
            ITelemetryService telemetryService,
            ILogger logger,
            int maxDegreeOfParallelism,
            Func<HttpMessageHandler> handlerFunc = null,
            TimeSpan? httpClientTimeout = null,
            int maxConcurrentBatches = DefaultMaxConcurrentBatches)
            : base(index, telemetryService, handlerFunc, httpClientTimeout)
        {
            _storageFactory = storageFactory ?? throw new ArgumentNullException(nameof(storageFactory));
            _sourceStorage = preferredPackageSourceStorage;
            _contentBaseAddress = contentBaseAddress;
            _dnxMaker = new DnxMaker(storageFactory);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (maxDegreeOfParallelism < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxDegreeOfParallelism),
                    string.Format(Strings.ArgumentOutOfRange, 1, int.MaxValue));
            }

            _maxDegreeOfParallelism = maxDegreeOfParallelism;

            if (maxConcurrentBatches < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxConcurrentBatches),
                    maxConcurrentBatches,
                    string.Format(Strings.ArgumentOutOfRange, 1, int.MaxValue));
            }

            _maxConcurrentBatches = maxConcurrentBatches;
        }

        protected override Task<IEnumerable<CatalogCommitItemBatch>> CreateBatchesAsync(IEnumerable<CatalogCommitItem> catalogItems)
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

            return CatalogCommitUtilities.CreateCommitBatchesByPackageIdAsync(catalogItems, CatalogCommitUtilities.GetPackageIdKey);
        }

        protected override Task<bool> FetchAsync(
            CollectorHttpClient client,
            ReadWriteCursor front,
            ReadCursor back,
            CancellationToken cancellationToken)
        {
            return CatalogCommitUtilities.FetchAsync(
                client,
                front,
                back,
                FetchCatalogItemsAsync,
                CatalogCommitUtilities.GetPackageIdKey,
                CreateBatchesAsync,
                ProcessBatchAsync,
                _maxConcurrentBatches,
                nameof(DnxCatalogCollector),
                cancellationToken);
        }

        protected override async Task<bool> OnProcessBatchAsync(
            CollectorHttpClient client,
            IEnumerable<JToken> items,
            JToken context,
            DateTime commitTimeStamp,
            bool isLastBatch,
            CancellationToken cancellationToken)
        {
            var catalogEntries = items
                .Select(
                    item => new CatalogEntry(
                        DateTime.Parse(item["commitTimeStamp"].ToString(), DateTimeFormatInfo.InvariantInfo, DateTimeStyles.RoundtripKind),
                        item["nuget:id"].ToString().ToLowerInvariant(),
                        NuGetVersionUtility.NormalizeVersion(item["nuget:version"].ToString()).ToLowerInvariant(),
                        item["@type"].ToString().Replace("nuget:", Schema.Prefixes.NuGet),
                        item))
                .OrderBy(item => item.CommitTimeStamp)
                .GroupBy(catalogEntry => catalogEntry.PackageId)
                .ToList();

            // Process .nupkg/.nuspec adds and deletes.
            var processedCatalogEntries = await ProcessCatalogEntriesAsync(client, catalogEntries, cancellationToken);

            // Update the package version index with adds and deletes.
            await UpdatePackageVersionIndexAsync(processedCatalogEntries, cancellationToken);

            return true;
        }

        private async Task<IEnumerable<CatalogEntry>> ProcessCatalogEntriesAsync(
            CollectorHttpClient client,
            IEnumerable<IEnumerable<CatalogEntry>> catalogEntryGroups,
            CancellationToken cancellationToken)
        {
            var processedCatalogEntries = new ConcurrentBag<CatalogEntry>();

            await catalogEntryGroups.ForEachAsync(_maxDegreeOfParallelism, async catalogEntryGroup =>
            {
                foreach (var catalogEntry in catalogEntryGroup)
                {
                    var packageId = catalogEntry.PackageId;
                    var normalizedPackageVersion = catalogEntry.NormalizedPackageVersion;

                    if (catalogEntry.EntryType == Schema.DataTypes.PackageDetails.ToString())
                    {
                        var properties = GetTelemetryProperties(catalogEntry);

                        using (_telemetryService.TrackDuration(TelemetryConstants.ProcessPackageDetailsSeconds, properties))
                        {
                            var packageFileName = PackageUtility.GetPackageFileName(
                                packageId,
                                normalizedPackageVersion);
                            var sourceUri = new Uri(_contentBaseAddress, packageFileName);
                            var destinationStorage = _storageFactory.Create(packageId);
                            var destinationRelativeUri = DnxMaker.GetRelativeAddressNupkg(
                                packageId,
                                normalizedPackageVersion);
                            var destinationUri = destinationStorage.GetUri(destinationRelativeUri);

                            var isNupkgSynchronized = await destinationStorage.AreSynchronized(sourceUri, destinationUri);
                            var isPackageInIndex = await _dnxMaker.HasPackageInIndexAsync(
                                destinationStorage,
                                packageId,
                                normalizedPackageVersion,
                                cancellationToken);
                            var areRequiredPropertiesPresent = await AreRequiredPropertiesPresentAsync(destinationStorage, destinationUri);

                            if (isNupkgSynchronized && isPackageInIndex && areRequiredPropertiesPresent)
                            {
                                _logger.LogInformation("No changes detected: {Id}/{Version}", packageId, normalizedPackageVersion);

                                return;
                            }

                            if ((isNupkgSynchronized && areRequiredPropertiesPresent)
                                || await ProcessPackageDetailsAsync(
                                    client,
                                    packageId,
                                    normalizedPackageVersion,
                                    sourceUri,
                                    properties,
                                    cancellationToken))
                            {
                                processedCatalogEntries.Add(catalogEntry);
                            }
                        }
                    }
                    else if (catalogEntry.EntryType == Schema.DataTypes.PackageDelete.ToString())
                    {
                        var properties = GetTelemetryProperties(catalogEntry);

                        using (_telemetryService.TrackDuration(TelemetryConstants.ProcessPackageDeleteSeconds, properties))
                        {
                            await ProcessPackageDeleteAsync(packageId, normalizedPackageVersion, cancellationToken);

                            processedCatalogEntries.Add(catalogEntry);
                        }
                    }
                }
            });

            return processedCatalogEntries;
        }

        private async Task<bool> AreRequiredPropertiesPresentAsync(Storage destinationStorage, Uri destinationUri)
        {
            var azureStorage = destinationStorage as IAzureStorage;

            if (azureStorage == null)
            {
                return true;
            }

            return await azureStorage.HasPropertiesAsync(
                destinationUri,
                DnxConstants.ApplicationOctetStreamContentType,
                DnxConstants.DefaultCacheControl);
        }

        private async Task UpdatePackageVersionIndexAsync(
            IEnumerable<CatalogEntry> catalogEntries,
            CancellationToken cancellationToken)
        {
            var count = catalogEntries.Count();

            if (count == 0)
            {
                return;
            }

            var packageId = catalogEntries.First().PackageId;
            var properties = new Dictionary<string, string>()
            {
                { TelemetryConstants.Id, packageId },
                { TelemetryConstants.BatchItemCount, count.ToString() }
            };

            using (_telemetryService.TrackDuration(TelemetryConstants.ProcessPackageVersionIndexSeconds, properties))
            {
                await _dnxMaker.UpdatePackageVersionIndexAsync(packageId, versions =>
                {
                    foreach (var catalogEntry in catalogEntries.OrderBy(item => item.CommitTimeStamp))
                    {
                        if (catalogEntry.EntryType == Schema.DataTypes.PackageDetails.ToString())
                        {
                            versions.Add(NuGetVersion.Parse(catalogEntry.NormalizedPackageVersion));
                        }
                        else if (catalogEntry.EntryType == Schema.DataTypes.PackageDelete.ToString())
                        {
                            versions.Remove(NuGetVersion.Parse(catalogEntry.NormalizedPackageVersion));
                        }
                    }
                }, cancellationToken);
            }

            foreach (var catalogEntry in catalogEntries)
            {
                _logger.LogInformation("Commit: {Id}/{Version}", packageId, catalogEntry.NormalizedPackageVersion);
            }
        }

        private async Task<bool> ProcessPackageDetailsAsync(
            HttpClient client,
            string packageId,
            string normalizedPackageVersion,
            Uri sourceUri,
            Dictionary<string, string> telemetryProperties,
            CancellationToken cancellationToken)
        {
            if (await ProcessPackageDetailsViaStorageAsync(
                packageId,
                normalizedPackageVersion,
                telemetryProperties,
                cancellationToken))
            {
                return true;
            }

            _telemetryService.TrackMetric(
                TelemetryConstants.UsePackageSourceFallback,
                metric: 1,
                properties: GetTelemetryProperties(packageId, normalizedPackageVersion));

            return await ProcessPackageDetailsViaHttpAsync(
                client,
                packageId,
                normalizedPackageVersion,
                sourceUri,
                telemetryProperties,
                cancellationToken);
        }

        private async Task<bool> ProcessPackageDetailsViaStorageAsync(
            string packageId,
            string normalizedPackageVersion,
            Dictionary<string, string> telemetryProperties,
            CancellationToken cancellationToken)
        {
            if (_sourceStorage == null)
            {
                return false;
            }

            var packageFileName = PackageUtility.GetPackageFileName(packageId, normalizedPackageVersion);
            var sourceUri = _sourceStorage.ResolveUri(packageFileName);

            var sourceBlob = await _sourceStorage.GetCloudBlockBlobReferenceAsync(sourceUri);

            if (await sourceBlob.ExistsAsync(cancellationToken))
            {
                // It's possible (though unlikely) that the blob may change between reads.  Reading a blob with a
                // single GET request returns the whole blob in a consistent state, but we're reading the blob many
                // different times.  To detect the blob changing between reads, we check the ETag again later.
                // If the ETag's differ, we'll fall back to using a single HTTP GET request.
                var token1 = await _sourceStorage.GetOptimisticConcurrencyControlTokenAsync(sourceUri, cancellationToken);

                telemetryProperties[TelemetryConstants.SizeInBytes] = sourceBlob.Length.ToString();

                var nuspec = await GetNuspecAsync(sourceBlob, packageId, cancellationToken);

                if (string.IsNullOrEmpty(nuspec))
                {
                    _logger.LogWarning(
                        "No .nuspec available for {Id}/{Version}.  Falling back to HTTP processing.",
                        packageId,
                        normalizedPackageVersion);
                }
                else
                {
                    await _dnxMaker.AddPackageAsync(
                        _sourceStorage,
                        nuspec,
                        packageId,
                        normalizedPackageVersion,
                        cancellationToken);

                    var token2 = await _sourceStorage.GetOptimisticConcurrencyControlTokenAsync(sourceUri, cancellationToken);

                    if (token1 == token2)
                    {
                        _logger.LogInformation("Added .nupkg and .nuspec for package {Id}/{Version}", packageId, normalizedPackageVersion);

                        return true;
                    }
                    else
                    {
                        _telemetryService.TrackMetric(
                            TelemetryConstants.BlobModified,
                            metric: 1,
                            properties: GetTelemetryProperties(packageId, normalizedPackageVersion));
                    }
                }
            }
            else
            {
                _telemetryService.TrackMetric(
                    TelemetryConstants.NonExistentBlob,
                    metric: 1,
                    properties: GetTelemetryProperties(packageId, normalizedPackageVersion));
            }

            return false;
        }

        private async Task<bool> ProcessPackageDetailsViaHttpAsync(
            HttpClient client,
            string id,
            string version,
            Uri sourceUri,
            Dictionary<string, string> telemetryProperties,
            CancellationToken cancellationToken)
        {
            var packageDownloader = new PackageDownloader(client, _logger);
            var requestUri = Utilities.GetNugetCacheBustingUri(sourceUri);

            using (var stream = await packageDownloader.DownloadAsync(requestUri, cancellationToken))
            {
                if (stream == null)
                {
                    _logger.LogWarning("Package {Id}/{Version} not found.", id, version);

                    return false;
                }

                telemetryProperties[TelemetryConstants.SizeInBytes] = stream.Length.ToString();

                var nuspec = GetNuspec(stream, id);

                if (nuspec == null)
                {
                    _logger.LogWarning("No .nuspec available for {Id}/{Version}. Skipping.", id, version);

                    return false;
                }

                stream.Position = 0;

                await _dnxMaker.AddPackageAsync(
                    stream,
                    nuspec,
                    id,
                    version,
                    cancellationToken);
            }

            _logger.LogInformation("Added .nupkg and .nuspec for package {Id}/{Version}", id, version);

            return true;
        }

        private async Task ProcessPackageDeleteAsync(
            string packageId,
            string normalizedPackageVersion,
            CancellationToken cancellationToken)
        {
            await _dnxMaker.DeletePackageAsync(packageId, normalizedPackageVersion, cancellationToken);

            _logger.LogInformation("Commit delete: {Id}/{Version}", packageId, normalizedPackageVersion);
        }

        private async Task ProcessBatchAsync(
            CollectorHttpClient client,
            JToken context,
            string packageId,
            CatalogCommitItemBatch batch,
            CatalogCommitItemBatch lastBatch,
            CancellationToken cancellationToken)
        {
            await Task.Yield();

            using (_telemetryService.TrackDuration(
                TelemetryConstants.ProcessBatchSeconds,
                new Dictionary<string, string>()
                {
                    { TelemetryConstants.Id, packageId },
                    { TelemetryConstants.BatchItemCount, batch.Items.Count.ToString() }
                }))
            {
                await OnProcessBatchAsync(
                    client,
                    batch.Items.OrderBy(item => item.CommitTimeStamp).Select(item => item.Value),
                    context,
                    batch.CommitTimeStamp,
                    batch.CommitTimeStamp == lastBatch.CommitTimeStamp,
                    cancellationToken);
            }
        }

        private static async Task<string> GetNuspecAsync(
            ICloudBlockBlob sourceBlob,
            string packageId,
            CancellationToken cancellationToken)
        {
            using (var stream = await sourceBlob.GetStreamAsync(cancellationToken))
            {
                return GetNuspec(stream, packageId);
            }
        }

        private static string GetNuspec(Stream stream, string id)
        {
            string name = $"{id}.nuspec";

            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
            {
                // first look for a nuspec file named as the package id
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                    {
                        using (TextReader reader = new StreamReader(entry.Open()))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
                // failing that, just return the first file that appears to be a nuspec
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith(".nuspec", StringComparison.InvariantCultureIgnoreCase))
                    {
                        using (TextReader reader = new StreamReader(entry.Open()))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
            }

            return null;
        }

        private static Dictionary<string, string> GetTelemetryProperties(CatalogEntry catalogEntry)
        {
            return GetTelemetryProperties(catalogEntry.PackageId, catalogEntry.NormalizedPackageVersion);
        }

        private static Dictionary<string, string> GetTelemetryProperties(string packageId, string normalizedPackageVersion)
        {
            return new Dictionary<string, string>()
            {
                { TelemetryConstants.Id, packageId },
                { TelemetryConstants.Version, normalizedPackageVersion }
            };
        }

        private sealed class CatalogEntry
        {
            internal DateTime CommitTimeStamp { get; }
            internal string PackageId { get; }
            internal string NormalizedPackageVersion { get; }
            internal string EntryType { get; }
            internal JToken Entry { get; }

            internal CatalogEntry(DateTime commitTimeStamp, string packageId, string normalizedPackageVersion, string entryType, JToken entry)
            {
                CommitTimeStamp = commitTimeStamp;
                PackageId = packageId;
                NormalizedPackageVersion = normalizedPackageVersion;
                EntryType = entryType;
                Entry = entry;
            }
        }
    }
}