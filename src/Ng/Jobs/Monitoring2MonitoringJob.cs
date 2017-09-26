using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Configuration;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Services.Storage;

namespace Ng.Jobs
{
    public class Monitoring2MonitoringJob : LoopingNgJob
    {
        private IPackageMonitoringStatusService _statusService;
        private IStorageQueue<PackageValidatorContext> _queue;

        public Monitoring2MonitoringJob(ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            var verbose = arguments.GetOrDefault(Arguments.Verbose, false);

            if (arguments.GetOrThrow<string>(Arguments.StorageType) != Arguments.AzureStorageType)
            {
                throw new ArgumentException("File storage is not supported!");
            }

            var monitoringStorageFactory = CommandHelpers.CreateStorageFactory(arguments, verbose);

            _statusService = CommandHelpers.GetPackageMonitoringStatusService(arguments, monitoringStorageFactory, LoggerFactory);

            _queue = CommandHelpers.CreateStorageQueue<PackageValidatorContext>(arguments);
        }

        protected override async Task RunInternal(CancellationToken cancellationToken)
        {
            var invalidPackages = await _statusService.GetAsync(PackageState.Invalid, cancellationToken);

            foreach (var invalidPackage in invalidPackages)
            {
                Logger.LogInformation("Requeuing invalid package {PackageId} {PackageVersion}.", invalidPackage.Package.Id, invalidPackage.Package.Version);

                await _queue.Add(
                    new PackageValidatorContext(invalidPackage.Package, invalidPackage.ValidationResult?.CatalogEntries),
                    cancellationToken);
            }
        }
    }
}