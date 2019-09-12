// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.Icons
{
    public class CatalogLeafDataProcessor : ICatalogLeafDataProcessor
    {
        private const int MaxExternalIconIngestAttempts = 3;
        private const int MaxBlobStorageCopyAttempts = 3;
        private const string CacheFilename = "c2i_cache.json";

        private static ConcurrentDictionary<Uri, ExternalIconCopyResult> ExternalIconCopyResults = null;

        private readonly IAzureStorage _packageStorage;
        private readonly IStorage _auxStorage;
        private readonly IIconProcessor _iconProcessor;
        private readonly IExternalIconContentProvider _externalIconContentProvider;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<CatalogLeafDataProcessor> _logger;

        public CatalogLeafDataProcessor(
            IAzureStorage packageStorage,
            IStorage auxStorage,
            IIconProcessor iconProcessor,
            IExternalIconContentProvider externalIconContentProvider,
            ITelemetryService telemetryService,
            ILogger<CatalogLeafDataProcessor> logger
            )
        {
            _packageStorage = packageStorage ?? throw new ArgumentNullException(nameof(packageStorage));
            _auxStorage = auxStorage ?? throw new ArgumentNullException(nameof(auxStorage));
            _iconProcessor = iconProcessor ?? throw new ArgumentNullException(nameof(iconProcessor));
            _externalIconContentProvider = externalIconContentProvider ?? throw new ArgumentNullException(nameof(externalIconContentProvider));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ProcessPackageDelete(Storage storage, CatalogCommitItem item, CancellationToken cancellationToken)
        {
            var targetStoragePath = GetTargetStorageIconPath(item);
            await _iconProcessor.DeleteIcon(storage, targetStoragePath, cancellationToken, item.PackageIdentity.Id, item.PackageIdentity.Version.ToNormalizedString());
        }

        public async Task ProcessPackageDetails(Storage destinationStorage, CatalogCommitItem item, string iconUrlString, string iconFile, CancellationToken cancellationToken)
        {
            var hasExternalIconUrl = !string.IsNullOrWhiteSpace(iconUrlString);
            var hasEmbeddedIcon = !string.IsNullOrWhiteSpace(iconFile);
            if (hasExternalIconUrl && !hasEmbeddedIcon && Uri.TryCreate(iconUrlString, UriKind.Absolute, out var iconUrl))
            {
                await ProcessExternalIconUrl(destinationStorage, item, iconUrl, cancellationToken);
            }
            else if (hasEmbeddedIcon)
            {
                await ProcessEmbeddedIcon(destinationStorage, item, iconFile, cancellationToken);
            }
        }

        public async Task InitializeIconUrlCache(CancellationToken cancellationToken)
        {
            if (ExternalIconCopyResults != null)
            {
                return;
            }

            var cacheUrl = _auxStorage.ResolveUri(CacheFilename);
            var content = await _auxStorage.LoadAsync(cacheUrl, cancellationToken);
            if (content == null)
            {
                ExternalIconCopyResults = new ConcurrentDictionary<Uri, ExternalIconCopyResult>();
                return;
            }
            using (var contentStream = content.GetContentStream())
            using (var reader = new StreamReader(contentStream))
            {
                var serializer = new JsonSerializer();
                var dictionary = (Dictionary<Uri, ExternalIconCopyResult>)serializer.Deserialize(reader, typeof(Dictionary<Uri, ExternalIconCopyResult>));
                ExternalIconCopyResults = new ConcurrentDictionary<Uri, ExternalIconCopyResult>(dictionary);
            }
        }

        public async Task StoreIconUrlCache(CancellationToken cancellationToken)
        {
            var cacheUrl = _auxStorage.ResolveUri(CacheFilename);
            var serialized = JsonConvert.SerializeObject(ExternalIconCopyResults);
            var content = new StringStorageContent(serialized, contentType: "text/json");
            await _auxStorage.SaveAsync(cacheUrl, content, cancellationToken);
        }

        private async Task ProcessExternalIconUrl(Storage destinationStorage, CatalogCommitItem item, Uri iconUrl, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Found external icon url {IconUrl} for {PackageId} {PackageVersion}",
                iconUrl,
                item.PackageIdentity.Id,
                item.PackageIdentity.Version);
            if (!IsValidIconUrl(iconUrl))
            {
                _logger.LogInformation("Invalid icon URL {IconUrl}", iconUrl);
                return;
            }
            if (ExternalIconCopyResults.TryGetValue(iconUrl, out var result))
            {
                if (result.IsCopySucceeded)
                {
                    _logger.LogInformation("Seen {IconUrl} before, will copy from {CachedLocation}",
                        iconUrl,
                        result.StorageUrl);
                    var storageUrl = result.StorageUrl;
                    var targetStoragePath = GetTargetStorageIconPath(item);
                    var destinationUrl = destinationStorage.ResolveUri(targetStoragePath);
                    if (storageUrl == destinationUrl)
                    {
                        // We came across the package that initially caused the icon to be added to the cache.
                        // Skipping it.
                        return;
                    }
                    await Retry.IncrementalAsync(
                        async () => await destinationStorage.CopyAsync(storageUrl, destinationStorage, destinationUrl, null, cancellationToken),
                        e => { _logger.LogError(0, e, "Exception while copying from cache"); return true; },
                        MaxBlobStorageCopyAttempts,
                        initialWaitInterval: TimeSpan.FromSeconds(5),
                        waitIncrement: TimeSpan.FromSeconds(1));
                    return;
                }
                else
                {
                    _logger.LogInformation("Previous copy attempt failed, skipping {IconUrl} for {PackageId} {PackageVersion}",
                        iconUrl,
                        item.PackageIdentity.Id,
                        item.PackageIdentity.Version);
                    return;
                }
            }
            using (_logger.BeginScope("Processing icon url {IconUrl}", iconUrl))
            using (_telemetryService.TrackExternalIconProcessingDuration(item.PackageIdentity.Id, item.PackageIdentity.Version.ToNormalizedString()))
            {
                var ingestionResult = await Retry.IncrementalAsync(
                    async () => await TryIngestExternalIconAsync(item, iconUrl, destinationStorage, cancellationToken),
                    e => false,
                    r => r.Result == AttemptResult.FailCanRetry,
                    MaxExternalIconIngestAttempts,
                    initialWaitInterval: TimeSpan.FromSeconds(5),
                    waitIncrement: TimeSpan.FromSeconds(1));

                ExternalIconCopyResult cacheItem;
                if (ingestionResult.Result == AttemptResult.Success)
                {
                    cacheItem = ExternalIconCopyResult.Success(iconUrl, ingestionResult.ResultUrl);
                }
                else
                {
                    _telemetryService.TrackExternalIconIngestionFailure(item.PackageIdentity.Id, item.PackageIdentity.Version.ToNormalizedString());
                    cacheItem = ExternalIconCopyResult.Fail(iconUrl);
                }
                ExternalIconCopyResults.AddOrUpdate(iconUrl, cacheItem, (_, v) => v); // will not overwrite existing entries
            }
        }

        private async Task ProcessEmbeddedIcon(Storage destinationStorage, CatalogCommitItem item, string iconFile, CancellationToken cancellationToken)
        {
            var packageFilename = PackageUtility.GetPackageFileName(item.PackageIdentity.Id, item.PackageIdentity.Version.ToNormalizedString());
            var packageUri = _packageStorage.ResolveUri(packageFilename);
            var packageBlobReference = await _packageStorage.GetCloudBlockBlobReferenceAsync(packageUri);
            using (_telemetryService.TrackEmbeddedIconProcessingDuration(item.PackageIdentity.Id, item.PackageIdentity.Version.ToNormalizedString()))
            using (var packageStream = await packageBlobReference.GetStreamAsync(cancellationToken))
            {
                var targetStoragePath = GetTargetStorageIconPath(item);
                var resultUrl = await _iconProcessor.CopyEmbeddedIconFromPackage(
                    packageStream,
                    iconFile,
                    destinationStorage,
                    targetStoragePath,
                    cancellationToken,
                    item.PackageIdentity.Id,
                    item.PackageIdentity.Version.ToNormalizedString());
            }
        }

        private bool IsValidIconUrl(Uri iconUrl)
        {
            return iconUrl.Scheme == Uri.UriSchemeHttp || iconUrl.Scheme == Uri.UriSchemeHttps;
        }

        private class TryIngestExternalIconAsyncResult
        {
            public AttemptResult Result { get; private set; }
            public Uri ResultUrl { get; private set; }
            public static TryIngestExternalIconAsyncResult Fail(AttemptResult failResult)
            {
                if (failResult == AttemptResult.Success)
                {
                    throw new ArgumentException($"{nameof(failResult)} cannot be {AttemptResult.Success}", nameof(failResult));
                }

                return new TryIngestExternalIconAsyncResult
                {
                    Result = failResult,
                    ResultUrl = null,
                };
            }
            public static TryIngestExternalIconAsyncResult FailCannotRetry() => Fail(AttemptResult.FailCannotRetry);
            public static TryIngestExternalIconAsyncResult FailCanRetry() => Fail(AttemptResult.FailCanRetry);
            public static TryIngestExternalIconAsyncResult Success(Uri resultUrl)
                => new TryIngestExternalIconAsyncResult
                {
                    Result = AttemptResult.Success,
                    ResultUrl = resultUrl ?? throw new ArgumentNullException(nameof(resultUrl))
                };
        }

        private async Task<TryIngestExternalIconAsyncResult> TryIngestExternalIconAsync(CatalogCommitItem item, Uri iconUrl, Storage destinationStorage, CancellationToken cancellationToken)
        {
            bool retry;
            var resultUrl = (Uri)null;
            int maxRetries = 10;
            do
            {
                retry = false;
                var getResult = await _externalIconContentProvider.TryGetResponseAsync(iconUrl, cancellationToken);
                if (getResult.AttemptResult != AttemptResult.Success)
                {
                    return TryIngestExternalIconAsyncResult.Fail(getResult.AttemptResult);
                }
                using (var response = getResult.HttpResponseMessage)
                {
                    if (response.StatusCode >= HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.MovedPermanently || response.StatusCode == HttpStatusCode.Found)
                    {
                        // normally, HttpClient follows redirects on its own, but there is a limit to it, so if the redirect chain is too long
                        // it will return 301 or 302, so we'll ignore these specifically.
                        _logger.LogInformation("Icon url {IconUrl} responded with {ResponseCode}", iconUrl, response.StatusCode);
                        return response.StatusCode < HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound ? TryIngestExternalIconAsyncResult.FailCannotRetry() : TryIngestExternalIconAsyncResult.FailCanRetry();
                    }
                    if (response.StatusCode == (HttpStatusCode)308)
                    {
                        // HttpClient does not seem to support HTTP status code 308, and we have at least one case when we get it:
                        // http://app.exceptionless.com/images/exceptionless-32.png
                        // so, we'll had it processed manually

                        var newUrl = response.Headers.Location;

                        if (iconUrl == newUrl || newUrl == null || !IsValidIconUrl(newUrl))
                        {
                            return TryIngestExternalIconAsyncResult.FailCannotRetry();
                        }

                        iconUrl = newUrl;
                        retry = true;
                        continue;
                    }
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Unexpected response code {ResponseCode} for {IconUrl}", response.StatusCode, iconUrl);
                        return TryIngestExternalIconAsyncResult.FailCanRetry();
                    }

                    using (var iconDataStream = await response.Content.ReadAsStreamAsync())
                    {
                        var targetStoragePath = GetTargetStorageIconPath(item);
                        resultUrl = await _iconProcessor.CopyIconFromExternalSource(iconDataStream, destinationStorage, targetStoragePath, cancellationToken, item.PackageIdentity.Id, item.PackageIdentity.Version.ToNormalizedString());
                    }
                }
            } while (retry && --maxRetries >= 0);

            if (resultUrl == null)
            {
                return TryIngestExternalIconAsyncResult.FailCannotRetry();
            }
            return TryIngestExternalIconAsyncResult.Success(resultUrl);
        }

        private static string GetTargetStorageIconPath(CatalogCommitItem item)
        {
            return $"{item.PackageIdentity.Id.ToLowerInvariant()}/{item.PackageIdentity.Version.ToNormalizedString().ToLowerInvariant()}/icon";
        }
    }
}
