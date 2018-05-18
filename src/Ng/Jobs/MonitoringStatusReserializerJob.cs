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
            Logger.LogInformation("Fetching list of package monitoring statuses");
            var statuses = await _statusService.ListAsync(cancellationToken);
            Logger.LogInformation("Finished fetching list of package monitoring statuses");
            Logger.LogInformation("Processing package monitoring statuses");
            await Task.WhenAll(statuses.Select(s => ReserializePackage(s, cancellationToken)));
        }

        public async Task ReserializePackage(PackageMonitoringStatusListItem listItem, CancellationToken cancellationToken)
        {
            var packageId = listItem.Package.Id;
            var packageVersion = listItem.Package.Version;

            try
            {
                Logger.LogInformation("Processing status for {PackageId} {PackageVersion}", packageId, packageVersion);
                var status = await _statusService.GetAsync(listItem.Package, cancellationToken);
                Logger.LogInformation("Successfully retrieved current status of {PackageId} {PackageVersion}", packageId, packageVersion);

                if (!status.ValidationResult?.DeletionAuditEntries.Any() ?? true)
                {
                    Logger.LogInformation("Status of {PackageId} {PackageVersion} does not have any deletion audit entries, will not reserialize", 
                        packageId, packageVersion);
                    return;
                }

                Logger.LogInformation("Reserializing status of {PackageId} {PackageVersion}", packageId, packageVersion);
                await _statusService.UpdateAsync(status, cancellationToken);
                Logger.LogInformation("Successfully reserialized status of {PackageId} {PackageVersion}", packageId, packageVersion);
            }
            catch (Exception e)
            {
                Logger.LogError(NuGet.Services.Metadata.Catalog.Monitoring.LogEvents.StatusReserializationFailure, e, 
                    "Failed to reserialize status of {PackageId} {PackageVersion}", packageId, packageVersion);
            }
        }
    }
}
