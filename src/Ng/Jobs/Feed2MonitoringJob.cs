// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Services.Configuration;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Services.Storage;

namespace Ng.Jobs
{
    public class Feed2MonitoringJob : LoopingNgJob
    {
        private V2FeedParser _feedParser;
        private IPackageMonitoringStatusService _statusService;
        private IStorageQueue<PackageValidatorContext> _packageValidatorQueue;
        private ReadCursor _front;

        // Start at -1 because the first thread will increment it to 0 and then fetch that page.
        private int _pagesProcessed = -1;

        private const int _degreeOfParallelism = 32;
        private const int _pageSize = 100;
        private const int _maxRequestFailures = 5;

        public Feed2MonitoringJob(ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
        }

        public override string GetUsage()
        {
            return "Usage: ng endpointmonitoringvalid "
                   + $"-{Arguments.Gallery} <v2-feed-address> "
                   + $"-{Arguments.StorageBaseAddress} <storage-base-address> "
                   + $"-{Arguments.StorageType} azure "
                   + $"[-{Arguments.StoragePath} <path>]"
                   + $"[-{Arguments.PackageStatusFolder} <folder>]"
                   + $"[-{Arguments.Verbose} true|false] ";
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            var gallery = arguments.GetOrThrow<string>(Arguments.Gallery);
            var verbose = arguments.GetOrDefault(Arguments.Verbose, false);

            if (arguments.GetOrThrow<string>(Arguments.StorageType) != Arguments.AzureStorageType)
            {
                throw new ArgumentException("File storage is not supported!");
            }

            var monitoringStorageFactory = CommandHelpers.CreateStorageFactory(arguments, verbose);

            _statusService = CommandHelpers.GetPackageMonitoringStatusService(arguments, monitoringStorageFactory, LoggerFactory);

            _packageValidatorQueue = CommandHelpers.CreateStorageQueue<PackageValidatorContext>(arguments);

            _feedParser = new V2FeedParser(HttpSource.Create(Repository.Factory.GetCoreV2(new PackageSource(gallery))), gallery);

            _front = ValidationCollectorFactory.GetFront(monitoringStorageFactory);
        }

        protected override async Task RunInternal(CancellationToken cancellationToken)
        {
            var validPackagesSet = await GetValidPackages(cancellationToken);

            var invalidPackages = new ConcurrentQueue<FeedPackageIdentity>();
            _pagesProcessed = 0;

            await ParallelExecutionUtility.ProcessInParallel(() => ProcessPackages(validPackagesSet, invalidPackages, cancellationToken), _degreeOfParallelism);

            if (!invalidPackages.IsEmpty)
            {
                var invalidPackagesList = new List<FeedPackageIdentity>();
                Logger.LogError("The following packages are missing:");
                while (invalidPackages.TryDequeue(out var invalidPackage))
                {
                    invalidPackagesList.Add(invalidPackage);
                    Logger.LogError($"{invalidPackage.Id} {invalidPackage.Version}");
                }
            }
        }

        private async Task ProcessPackages(HashSet<FeedPackageIdentity> validPackagesSet, ConcurrentQueue<FeedPackageIdentity> invalidPackages, CancellationToken cancellationToken)
        {
            var queue = new Queue<V2FeedPackageInfo>();

            bool shouldContinue = true;
            while (shouldContinue)
            {
                if (queue.Count > 0)
                {
                    var package = queue.Dequeue();
                    foreach (var version in await RepeatUntilSuccessOrMaxFailures(() => _feedParser.FindPackagesByIdAsync(package.Id, NullLogger.Instance, cancellationToken)))
                    {
                        var feedPackage = new FeedPackageIdentity(version);

                        var found = false;
                        try
                        {
                            if (validPackagesSet.Contains(feedPackage))
                            {
                                found = true;
                            }
                        }
                        finally
                        {
                            if (!found)
                            {
                                // The package may have been scanned while we were parsing the feed, so check for its status again.
                                var status = await _statusService.GetAsync(feedPackage, cancellationToken);
                                if (status == null)
                                {
                                    // We can't find the package's status, so add it to the list of missing packages.
                                    Logger.LogError($"Failed to find {feedPackage.Id} {feedPackage.Version}");
                                    await _packageValidatorQueue.Add(
                                        new PackageValidatorContext(feedPackage, null),
                                        cancellationToken);
                                    invalidPackages.Enqueue(feedPackage);
                                }
                                else
                                {
                                    // We found the package's status in our second check.
                                    found = true;
                                }
                            }
                        }
                    }
                }
                else
                {
                    var page = Interlocked.Increment(ref _pagesProcessed);

                    V2FeedPage nextFeedPage = await RepeatUntilSuccessOrMaxFailures(() => _feedParser.GetPackagesPageAsync("", new SearchFilter(true), page * _pageSize, _pageSize, NullLogger.Instance, cancellationToken));

                    if (nextFeedPage != null && nextFeedPage.Items.Count > 0)
                    {
                        foreach (var unprocessedPackage in nextFeedPage.Items)
                        {
                            queue.Enqueue(unprocessedPackage);
                        }
                    }
                    else
                    {
                        shouldContinue = false;
                    }
                }
            }
        }

        private async Task<HashSet<FeedPackageIdentity>> GetValidPackages(CancellationToken cancellationToken)
        {
            var validPackagesList = await _statusService.ListAsync(cancellationToken);
            return new HashSet<FeedPackageIdentity>(validPackagesList.Select(p => p.Package).ToList());
        }

        private static async Task<T> RepeatUntilSuccessOrMaxFailures<T>(Func<Task<T>> action) where T : class
        {
            var requestSuccessful = false;
            var failures = 0;

            T result = null;
            while (!requestSuccessful || failures > _maxRequestFailures)
            {
                try
                {
                    result = await action();
                    requestSuccessful = true;
                }
                catch
                {
                    failures++;
                }
            }

            return result;
        }
    }
}