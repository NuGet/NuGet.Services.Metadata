using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Packaging.Core;
using NuGet.Services.Configuration;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Services.Storage;

namespace Ng.Jobs
{
    public class Catalog2MonitoringReprocessingJob : LoopingNgJob
    {
        private IPackageMonitoringStatusService _statusService;
        private IStorageQueue<PackageValidatorContext> _queue;
        private CatalogIndexReader _catalogIndexReader;

        public Catalog2MonitoringReprocessingJob(ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            var source = arguments.GetOrThrow<string>(Arguments.Source);
            var verbose = arguments.GetOrDefault(Arguments.Verbose, false);

            if (arguments.GetOrThrow<string>(Arguments.StorageType) != Arguments.AzureStorageType)
            {
                throw new ArgumentException("File storage is not supported!");
            }

            var monitoringStorageFactory = CommandHelpers.CreateStorageFactory(arguments, verbose);

            var messageHandlerFactory = CommandHelpers.GetHttpMessageHandlerFactory(verbose);

            _statusService = CommandHelpers.GetPackageMonitoringStatusService(arguments, monitoringStorageFactory, LoggerFactory);

            _queue = CommandHelpers.CreateStorageQueue<PackageValidatorContext>(arguments);

            _catalogIndexReader = new CatalogIndexReader(new Uri(source), new CollectorHttpClient(messageHandlerFactory()));
        }

        protected override async Task RunInternal(CancellationToken cancellationToken)
        {
            Logger.LogInformation("Fetching list of validated packages.");
            var validatedPackages = await GetValidatedPackages(cancellationToken);

            Logger.LogInformation("Parsing catalog for all entries.");
            var entries = await _catalogIndexReader.GetEntries();

            var packageEntries = entries
                .GroupBy(c => new PackageIdentity(c.Id, c.Version))
                .Select(g => g.OrderByDescending(c => c.CommitTimeStamp).First());

            Logger.LogInformation("Processing packages.");
            foreach (var packageEntry in packageEntries)
            {
                var feedPackage = new FeedPackageIdentity(new PackageIdentity(packageEntry.Id, packageEntry.Version));
                Logger.LogInformation("Processing {Id} {Version}", feedPackage.Id, feedPackage.Version);

                if (!validatedPackages.Contains(feedPackage))
                {
                    Logger.LogError("{Id} {Version} was skipped!", feedPackage.Id, feedPackage.Version);
                    await _queue.Add(
                        new PackageValidatorContext(feedPackage, new CatalogIndexEntry[] { packageEntry }),
                        cancellationToken);
                }
            }
        }

        private async Task<HashSet<FeedPackageIdentity>> GetValidatedPackages(CancellationToken cancellationToken)
        {
            var validatedPackagesList = await _statusService.ListAsync(cancellationToken);
            return new HashSet<FeedPackageIdentity>(validatedPackagesList.Select(p => p.Package).ToList());
        }
    }
}