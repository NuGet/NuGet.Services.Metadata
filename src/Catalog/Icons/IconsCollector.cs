// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.Icons
{
    public class IconsCollector : CommitCollector
    {
        private readonly IAzureStorage _packageStorage;
        private readonly IStorage _auxStorage;
        private readonly IStorageFactory _targetStorageFactory;
        private readonly IIconProcessor _iconProcessor;
        private readonly ILogger<IconsCollector> _logger;

        public IconsCollector(
            Uri index,
            ITelemetryService telemetryService,
            IAzureStorage packageStorage,
            IStorage auxStorage,
            IStorageFactory targetStorageFactory,
            IIconProcessor iconProcessor,
            Func<HttpMessageHandler> httpHandlerFactory,
            ILogger<IconsCollector> logger)
            : base(index, telemetryService, httpHandlerFactory)
        {
            _packageStorage = packageStorage ?? throw new ArgumentNullException(nameof(packageStorage));
            _auxStorage = auxStorage ?? throw new ArgumentNullException(nameof(auxStorage));
            _targetStorageFactory = targetStorageFactory ?? throw new ArgumentNullException(nameof(targetStorageFactory));
            _iconProcessor = iconProcessor ?? throw new ArgumentNullException(nameof(iconProcessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        protected override async Task<bool> OnProcessBatchAsync(
            CollectorHttpClient client,
            IEnumerable<CatalogCommitItem> items,
            JToken context,
            DateTime commitTimeStamp,
            bool isLastBatch,
            CancellationToken cancellationToken)
        {
            var filteredItems = items
                .GroupBy(i => i.PackageIdentity)                          // if we have multiple commits for the same package
                .Select(g => g.OrderBy(i => i.CommitTimeStamp).ToList()); // group them together for processing in order
            var itemsToProcess = new ConcurrentBag<IReadOnlyCollection<CatalogCommitItem>>(filteredItems);
            var tasks = Enumerable
                .Range(1, ServicePointManager.DefaultConnectionLimit)
                .Select(_ => ProcessIconsAsync(client, itemsToProcess, cancellationToken));
            await Task.WhenAll(tasks);
            return true;
        }

        private async Task ProcessIconsAsync(
            CollectorHttpClient httpClient,
            ConcurrentBag<IReadOnlyCollection<CatalogCommitItem>> items,
            CancellationToken cancellationToken)
        {
            await Task.Yield();
            var storage = _targetStorageFactory.Create();
            while (items.TryTake(out var entries))
            {
                var firstItem = entries.First();
                using (_logger.BeginScope("Processing commits for {PackageId} {PackageVersion}", firstItem.PackageIdentity.Id, firstItem.PackageIdentity.Version))
                {
                    foreach (var item in entries)
                    {
                        if (item.IsPackageDetails)
                        {
                            await ProcessPackageDetails(httpClient, storage, item, cancellationToken);
                        }
                        else if (item.IsPackageDelete)
                        {
                            // TODO: delete icon
                            await ProcessPackageDelete(storage, item, cancellationToken);
                        }
                    }
                }
            }
        }

        private async Task ProcessPackageDelete(Storage storage, CatalogCommitItem item, CancellationToken cancellationToken)
        {
            var targetStoragePath = GetTargetStorageIconPath(item);
            await _iconProcessor.DeleteIcon(storage, targetStoragePath, cancellationToken, item.PackageIdentity.Id, item.PackageIdentity.Version.ToNormalizedString());
        }

        private async Task ProcessPackageDetails(CollectorHttpClient httpClient, Storage storage, CatalogCommitItem item, CancellationToken cancellationToken)
        {
            var leafContent = await httpClient.GetStringAsync(item.Uri, cancellationToken);
            var data = JsonConvert.DeserializeObject<ExternalIconUrlInformation>(leafContent);
            var hasExternalIconUrl = !string.IsNullOrWhiteSpace(data.IconUrl);
            var hasEmbeddedIcon = !string.IsNullOrWhiteSpace(data.IconFile);
            if (hasExternalIconUrl && !hasEmbeddedIcon && Uri.TryCreate(data.IconUrl, UriKind.Absolute, out var iconUrl))
            {
                _logger.LogInformation("Found external icon url {IconUrl} for {PackageId} {PackageVersion}",
                    iconUrl,
                    item.PackageIdentity.Id,
                    item.PackageIdentity.Version);
                using (_telemetryService.TrackExternalIconProcessingDuration(item.PackageIdentity.Id, item.PackageIdentity.Version.ToNormalizedString()))
                using (var iconDataStream = await httpClient.GetStreamAsync(iconUrl))
                {
                    var targetStoragePath = GetTargetStorageIconPath(item);
                    await _iconProcessor.CopyIconFromExternalSource(iconDataStream, storage, targetStoragePath, cancellationToken, item.PackageIdentity.Id, item.PackageIdentity.Version.ToNormalizedString());
                }
            }
            else if (hasEmbeddedIcon)
            {
                var packageFilename = PackageUtility.GetPackageFileName(item.PackageIdentity.Id, item.PackageIdentity.Version.ToNormalizedString());
                var packageUri = _packageStorage.ResolveUri(packageFilename);
                var packageBlobReference = await _packageStorage.GetCloudBlockBlobReferenceAsync(packageUri);
                using (_telemetryService.TrackEmbeddedIconProcessingDuration(item.PackageIdentity.Id, item.PackageIdentity.Version.ToNormalizedString()))
                using (var packageStream = await packageBlobReference.GetStreamAsync(cancellationToken))
                {
                    var targetStoragePath = GetTargetStorageIconPath(item);
                    await _iconProcessor.CopyEmbeddedIconFromPackage(packageStream, data.IconFile, storage, targetStoragePath, cancellationToken, item.PackageIdentity.Id, item.PackageIdentity.Version.ToNormalizedString());
                }
            }
        }

        private static string GetTargetStorageIconPath(CatalogCommitItem item)
        {
            return $"{item.PackageIdentity.Id}/{item.PackageIdentity.Version.ToNormalizedString()}/icon";
        }

        private class ExternalIconUrlInformation
        {
            public string IconUrl { get; set; }
            public string IconFile { get; set; }
        }
    }
}
