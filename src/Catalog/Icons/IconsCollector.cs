// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Protocol.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.Icons
{
    public class IconsCollector : CommitCollector
    {
        private const int MaxExternalIconIngestAttempts = 3;
        private const int MaxBlobStorageCopyAttempts = 3;
        private const string CacheFilename = "c2i_cache.json";

        private static ConcurrentDictionary<Uri, ExternalIconCopyResult> ExternalIconCopyResults = null;

        private readonly IAzureStorage _packageStorage;
        private readonly IStorage _auxStorage;
        private readonly IStorageFactory _targetStorageFactory;
        private readonly IIconProcessor _iconProcessor;
        private readonly ICatalogClient _catalogClient;
        private readonly IHttpResponseMessageProvider _httpResponseMessageProvider;
        private readonly ILogger<IconsCollector> _logger;

        public IconsCollector(
            Uri index,
            ITelemetryService telemetryService,
            IAzureStorage packageStorage,
            IStorage auxStorage,
            IStorageFactory targetStorageFactory,
            IIconProcessor iconProcessor,
            ICatalogClient catalogClient,
            IHttpResponseMessageProvider httpResponseMessageProvider,
            Func<HttpMessageHandler> httpHandlerFactory,
            ILogger<IconsCollector> logger)
            : base(index, telemetryService, httpHandlerFactory, httpClientTimeout: TimeSpan.FromMinutes(5))
        {
            _packageStorage = packageStorage ?? throw new ArgumentNullException(nameof(packageStorage));
            _auxStorage = auxStorage ?? throw new ArgumentNullException(nameof(auxStorage));
            _targetStorageFactory = targetStorageFactory ?? throw new ArgumentNullException(nameof(targetStorageFactory));
            _iconProcessor = iconProcessor ?? throw new ArgumentNullException(nameof(iconProcessor));
            _catalogClient = catalogClient ?? throw new ArgumentNullException(nameof(catalogClient));
            _httpResponseMessageProvider = httpResponseMessageProvider ?? throw new ArgumentNullException(nameof(httpResponseMessageProvider));
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
            await InitializeCache(cancellationToken);

            var filteredItems = items
                .GroupBy(i => i.PackageIdentity)                          // if we have multiple commits for the same package (id AND version)
                .Select(g => g.OrderBy(i => i.CommitTimeStamp).ToList()); // group them together for processing in order
            var itemsToProcess = new ConcurrentBag<IReadOnlyCollection<CatalogCommitItem>>(filteredItems);
            var tasks = Enumerable
                .Range(1, ServicePointManager.DefaultConnectionLimit)
                .Select(_ => ProcessIconsAsync(client, _catalogClient, itemsToProcess, cancellationToken));
            await Task.WhenAll(tasks);

            await StoreCache(cancellationToken);

            return true;
        }

        private async Task StoreCache(CancellationToken cancellationToken)
        {
            var cacheUrl = _auxStorage.ResolveUri(CacheFilename);
            var serialized = JsonConvert.SerializeObject(ExternalIconCopyResults);
            var content = new StringStorageContent(serialized, contentType: "text/json");
            await _auxStorage.SaveAsync(cacheUrl, content, cancellationToken);
        }

        private async Task InitializeCache(CancellationToken cancellationToken)
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

        private async Task ProcessIconsAsync(
            CollectorHttpClient httpClient,
            ICatalogClient catalogClient,
            ConcurrentBag<IReadOnlyCollection<CatalogCommitItem>> items,
            CancellationToken cancellationToken)
        {
            await Task.Yield();
            var storage = _targetStorageFactory.Create();

            using (_logger.BeginScope("{CallGuid}", Guid.NewGuid()))
            while (items.TryTake(out var entries))
            {
                var firstItem = entries.First();
                using (_logger.BeginScope("Processing commits for {PackageId} {PackageVersion}", firstItem.PackageIdentity.Id, firstItem.PackageIdentity.Version))
                {
                    foreach (var item in entries)
                    {
                        if (item.IsPackageDetails)
                        {
                            await ProcessPackageDetails(storage, item, cancellationToken);
                        }
                        else if (item.IsPackageDelete)
                        {
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

        private async Task ProcessPackageDetails(Storage destinationStorage, CatalogCommitItem item, CancellationToken cancellationToken)
        {
            var data = await _catalogClient.GetPackageDetailsLeafAsync(item.Uri.AbsoluteUri);
            var hasExternalIconUrl = !string.IsNullOrWhiteSpace(data.IconUrl);
            var hasEmbeddedIcon = !string.IsNullOrWhiteSpace(data.IconFile);
            if (hasExternalIconUrl && !hasEmbeddedIcon && Uri.TryCreate(data.IconUrl, UriKind.Absolute, out var iconUrl))
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
                            e => true,
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
                        cacheItem = ExternalIconCopyResult.Fail(iconUrl);
                    }
                    ExternalIconCopyResults.AddOrUpdate(iconUrl, cacheItem, (_, v) => v); // TODO: double check what to do when overwriting the cache entry
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
                    var resultUrl = await _iconProcessor.CopyEmbeddedIconFromPackage(packageStream, data.IconFile, destinationStorage, targetStoragePath, cancellationToken, item.PackageIdentity.Id, item.PackageIdentity.Version.ToNormalizedString());
                }
            }
        }

        private bool IsValidIconUrl(Uri iconUrl)
        {
            return iconUrl.Scheme == Uri.UriSchemeHttp || iconUrl.Scheme == Uri.UriSchemeHttps;
        }

        private enum AttemptResult
        {
            Success,
            FailCanRetry,
            FailCannotRetry
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
                var getResult = await TryGetResponse(iconUrl, cancellationToken);
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
                        _telemetryService.TrackExternalIconIngestionFailure(item.PackageIdentity.Id, item.PackageIdentity.Version.ToNormalizedString());
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
                    response.EnsureSuccessStatusCode();

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

        private class TryGetResponseResult
        {
            public HttpResponseMessage HttpResponseMessage { get; set; }
            public AttemptResult AttemptResult;

            public static TryGetResponseResult Success(HttpResponseMessage httpResponseMessage)
            {
                return new TryGetResponseResult
                {
                    AttemptResult = AttemptResult.Success,
                    HttpResponseMessage = httpResponseMessage,
                };
            }

            public static TryGetResponseResult FailCanRetry()
            {
                return new TryGetResponseResult
                {
                    AttemptResult = AttemptResult.FailCanRetry,
                    HttpResponseMessage = null,
                };
            }

            public static TryGetResponseResult FailCannotRetry()
            {
                return new TryGetResponseResult
                {
                    AttemptResult = AttemptResult.FailCannotRetry,
                    HttpResponseMessage = null,
                };
            }
        }

        private async Task<TryGetResponseResult> TryGetResponse(Uri iconUrl, CancellationToken cancellationToken)
        {
            try
            {
                return TryGetResponseResult.Success(await _httpResponseMessageProvider.GetAsync(iconUrl, cancellationToken));
            }
            catch (HttpRequestException e) when (IsConnectFailure(e))
            {
                _logger.LogInformation("Failed to connect to remote host to retrieve the icon");
            }
            catch (HttpRequestException e) when (IsDnsFailure(e))
            {
                _logger.LogInformation("Failed to resolve DNS name for the icon URL");
                return TryGetResponseResult.FailCannotRetry();
            }
            catch (HttpRequestException e) when (IsConnectionClosed(e))
            {
                _logger.LogInformation("Connection closed unexpectedly while trying to retrieve the icon");
            }
            catch (HttpRequestException e) when (IsTLSSetupFailure(e))
            {
                _logger.LogInformation("TLS setup failed while trying to retrieve the icon");
            }
            catch (TaskCanceledException e) when (e.CancellationToken != cancellationToken)
            {
                _logger.LogInformation("Timed out while trying to get the icon data");
            }
            catch (HttpRequestException e)
            {
                _logger.LogError(0, e, "HTTP exception while trying to retrieve icon file");
            }
            catch (Exception e)
            {
                _logger.LogError(0, e, "Exception while trying to retrieve URL: {IconUrl}", iconUrl);
            }
            return TryGetResponseResult.FailCanRetry();
        }

        private static bool IsConnectFailure(HttpRequestException e)
        {
            return (e?.InnerException as WebException)?.Status == WebExceptionStatus.ConnectFailure;
        }

        private static bool IsDnsFailure(HttpRequestException e)
        {
            return (e?.InnerException as WebException)?.Status == WebExceptionStatus.NameResolutionFailure;
        }

        private static bool IsConnectionClosed(HttpRequestException e)
        {
            return (e?.InnerException as WebException)?.Status == WebExceptionStatus.ConnectionClosed;
        }

        private static bool IsTLSSetupFailure(HttpRequestException e)
        {
            var innerWebException = e?.InnerException as WebException;
            return innerWebException?.Status == WebExceptionStatus.TrustFailure || innerWebException?.Status == WebExceptionStatus.SecureChannelFailure;
        }

        private static string GetTargetStorageIconPath(CatalogCommitItem item)
        {
            return $"{item.PackageIdentity.Id.ToLowerInvariant()}/{item.PackageIdentity.Version.ToNormalizedString().ToLowerInvariant()}/icon";
        }
    }
}
