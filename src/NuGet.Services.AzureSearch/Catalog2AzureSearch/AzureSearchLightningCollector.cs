// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.CodeDom;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Packaging.Core;
using NuGet.Protocol.Catalog;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch
{
    /// <summary>
    /// An Azure Search collector that processes all applicable catalog leafs
    /// in a single batch. Use this when rebuilding the index. For normal scenarios,
    /// use <see cref="AzureSearchCollector"/>.
    /// </summary>
    public class AzureSearchLightningCollector : ICollector
    {
        private static readonly Uri[] PackageDetailsTypeUris = new[] { Schema.DataTypes.PackageDetails };
        private static readonly Uri[] PackageDeleteTypeUris = new[] { Schema.DataTypes.PackageDelete };

        private readonly ICatalogClient _catalogClient;
        private readonly ICommitCollectorLogic _logic;
        private readonly IOptionsSnapshot<Catalog2AzureSearchConfiguration> _options;
        private readonly ILogger<AzureSearchLightningCollector> _logger;

        public AzureSearchLightningCollector(
            ICatalogClient catalogClient,
            ICommitCollectorLogic logic,
            IOptionsSnapshot<Catalog2AzureSearchConfiguration> options,
            ILogger<AzureSearchLightningCollector> logger)
        {
            _catalogClient = catalogClient ?? throw new ArgumentNullException(nameof(catalogClient));
            _logic = logic ?? throw new ArgumentNullException(nameof(logic));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> RunAsync(ReadWriteCursor front, ReadCursor back, CancellationToken cancellationToken)
        {
            var catalogItems = await GetCatalogItemsAsync(front, back, cancellationToken);

            _logger.LogInformation("Processing {ItemCount} catalog items...", catalogItems.Count);

            await _logic.OnProcessBatchAsync(catalogItems);

            _logger.LogInformation("Processed {ItemCount} catalog items", catalogItems.Count);

            return true;
        }

        private async Task<IReadOnlyList<CatalogCommitItem>> GetCatalogItemsAsync(ReadWriteCursor front, ReadCursor back, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Finding catalog pages...");

            var catalogIndex = await _catalogClient.GetIndexAsync(_options.Value.Source);
            var pageUrls = catalogIndex.Items
                .Where(i => i.CommitTimestamp.UtcDateTime >= front.Value)
                .Where(i => i.CommitTimestamp.UtcDateTime <= back.Value)
                .Select(i => i.Url)
                .ToList();

            _logger.LogInformation("Found {PageCount} catalog pages. Finding catalog items...", pageUrls.Count);

            var allWork = new ConcurrentBag<string>(pageUrls);
            var allItems = new ConcurrentBag<CatalogLeafItem>();

            await ParallelAsync.Repeat(
                degreeOfParallelism: _options.Value.MaxConcurrentCatalogLeafDownloads,
                taskFactory: async () =>
                {
                    await Task.Yield();

                    while (allWork.TryTake(out var pageUrl))
                    {
                        await AddCatalogCommitsAsync(pageUrl, front, back, allItems);
                    }
                });

            _logger.LogInformation("Found {ItemCount} catalog items. Deduping...", allItems.Count);

            return allItems
                .GroupBy(item => new PackageIdentity(item.PackageId, item.ParsePackageVersion()))
                .Select(group => group.OrderByDescending(i => i.CommitTimestamp).First())
                .Select(item => new CatalogCommitItem(
                    uri: new Uri(item.Url, UriKind.Absolute),
                    commitId: item.CommitId,
                    commitTimeStamp: item.CommitTimestamp.UtcDateTime,
                    types: new string[0],
                    typeUris: item.IsPackageDetails()
                        ? PackageDetailsTypeUris
                        : PackageDeleteTypeUris,
                    packageIdentity: new PackageIdentity(item.PackageId, item.ParsePackageVersion())))
                .ToList();
        }

        private async Task AddCatalogCommitsAsync(
            string pageUrl,
            ReadWriteCursor front,
            ReadCursor back,
            ConcurrentBag<CatalogLeafItem> allItems)
        {
            try
            {
                _logger.LogInformation("Finding catalog items from page {PageUrl}...", pageUrl);

                var catalogPage = await _catalogClient.GetPageAsync(pageUrl);
                var catalogCommits = catalogPage.Items
                    .Where(i => i.CommitTimestamp.UtcDateTime >= front.Value)
                    .Where(i => i.CommitTimestamp.UtcDateTime <= back.Value);

                foreach (var catalogCommit in catalogCommits)
                {
                    allItems.Add(catalogCommit);
                }

                _logger.LogInformation(
                    "Added {CommitCount} catalog items from page {PageUrl}",
                    catalogPage.Items.Count,
                    pageUrl);
            }
            catch (Exception e)
            {
                _logger.LogError(0, e, "Unable to add catalog items from page {PageUrl}", pageUrl);
            }
        }

        private async Task AddCatalogCommitsOldAsync(
            string pageUrl,
            ReadWriteCursor front,
            ReadCursor back,
            ConcurrentBag<CatalogCommitItem> allCommits)
        {
            try
            {
                _logger.LogInformation("Finding catalog commits from page {PageUrl}...", pageUrl);

                var catalogPage = await _catalogClient.GetPageAsync(pageUrl);
                var catalogCommits = catalogPage.Items
                    .Where(i => i.CommitTimestamp.UtcDateTime >= front.Value)
                    .Where(i => i.CommitTimestamp.UtcDateTime <= back.Value)
                    .Select(item => new CatalogCommitItem(
                        uri: new Uri(item.Url, UriKind.Absolute),
                        commitId: item.CommitId,
                        commitTimeStamp: item.CommitTimestamp.UtcDateTime,
                        types: new string[0],
                        typeUris: item.IsPackageDetails()
                            ? PackageDetailsTypeUris
                            : PackageDeleteTypeUris,
                        packageIdentity: new PackageIdentity(item.PackageId, item.ParsePackageVersion())));

                foreach (var catalogCommit in catalogCommits)
                {
                    allCommits.Add(catalogCommit);
                }

                _logger.LogInformation(
                    "Added {CommitCount} catalog commits from page {PageUrl}",
                    catalogPage.Items.Count,
                    pageUrl);
            }
            catch (Exception e)
            {
                _logger.LogError(0, e, "Unable to add catalog items from page {PageUrl}", pageUrl);
            }
        }
    }
}
