// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Configuration;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Monitoring;

namespace Ng.Jobs
{
    public class MonitoringStatusReserializerJob : NgJob
    {
        private IPackageMonitoringStatusService _statusService;
        private CatalogIndexReader _catalogIndexReader;

        public MonitoringStatusReserializerJob(ITelemetryService telemetryService, ILoggerFactory loggerFactory)
            : base(telemetryService, loggerFactory)
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

            var messageHandlerFactory = CommandHelpers.GetHttpMessageHandlerFactory(TelemetryService, verbose);

            _statusService = CommandHelpers.GetPackageMonitoringStatusService(arguments, monitoringStorageFactory, LoggerFactory);
        }

        protected override async Task RunInternal(CancellationToken cancellationToken)
        {
            var validStatusesTask = _statusService.GetAsync(PackageState.Valid, cancellationToken);
            var invalidStatusesTask = _statusService.GetAsync(PackageState.Invalid, cancellationToken);
            var statuses = 
                (await Task.WhenAll(
                    new[] { validStatusesTask, invalidStatusesTask }))
                    .SelectMany(p => p)
                    .Where(p => p.ValidationResult?.DeletionAuditEntries.Any() ?? false);

            foreach (var status in statuses)
            {
                try
                {
                    var existingStatus = await _statusService.GetAsync(status.Package, cancellationToken);

                    if (existingStatus?.ValidationResult != null && CompareCatalogEntries(status.ValidationResult.CatalogEntries, existingStatus.ValidationResult.CatalogEntries))
                    {
                        // A newer catalog entry of this package has already been validated.
                        Logger.LogInformation("A newer catalog entry of {PackageId} {PackageVersion} has already been processed ({OldCommitTimeStamp} < {NewCommitTimeStamp}).",
                            status.Package.Id, status.Package.Version,
                            status.ValidationResult.CatalogEntries.Max(c => c.CommitTimeStamp),
                            existingStatus.ValidationResult.CatalogEntries.Max(c => c.CommitTimeStamp));

                        continue;
                    }

                    await _statusService.UpdateAsync(status, cancellationToken);
                }
                catch (Exception e)
                {
                    Logger.LogError(
                        NuGet.Services.Metadata.Catalog.Monitoring.LogEvents.StatusReserializationFailure, 
                        e, 
                        "Failed to reserialize the status of {PackageId} {PackageVersion}", 
                        status.Package.Id, 
                        status.Package.Version);
                }
            }
        }

        /// <summary>
        /// Returns if the newest entry in <paramref name="first"/> is older than the newest entry in <paramref name="second"/>.
        /// </summary>
        private bool CompareCatalogEntries(IEnumerable<CatalogIndexEntry> first, IEnumerable<CatalogIndexEntry> second)
        {
            return first.Max(c => c.CommitTimeStamp) < second.Max(c => c.CommitTimeStamp);
        }
    }
}
