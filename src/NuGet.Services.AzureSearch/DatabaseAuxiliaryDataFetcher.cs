// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;

namespace NuGet.Services.AzureSearch
{
    public class DatabaseAuxiliaryDataFetcher : IDatabaseAuxiliaryDataFetcher
    {
        private static readonly string[] EmptyStringArray = new string[0];

        private readonly ISqlConnectionFactory<GalleryDbConfiguration> _connectionFactory;
        private readonly IEntitiesContextFactory _entitiesContextFactory;
        private readonly IAzureSearchTelemetryService _telemetryService;
        private readonly ILogger<DatabaseAuxiliaryDataFetcher> _logger;

        private const string GetVerifiedPackagesSql = @"
SELECT pr.Id
FROM PackageRegistrations pr (NOLOCK)
WHERE pr.IsVerified = 1
";

        private const string GetPackageIdToOwnersSql = @"
SELECT
    pr.Id,
    u.Username
FROM PackageRegistrations pr (NOLOCK)
INNER JOIN PackageRegistrationOwners pro (NOLOCK) ON pro.PackageRegistrationKey = pr.[Key]
INNER JOIN Users u (NOLOCK) ON pro.UserKey = u.[Key]
";

        private const int GetPopularityTransfersPageSize = 1000;
        private const string GetPopularityTransfersSkipParameter = "@skip";
        private const string GetPopularityTransfersTakeParameter = "@take";
        private const string GetPopularityTransfersSql = @"
SELECT TOP (@take)
    fpr.Id AS FromPackageId,
    tpr.Id AS ToPackageId
FROM PackageRenames r (NOLOCK)
INNER JOIN PackageRegistrations fpr (NOLOCK) ON fpr.[Key] = r.[FromPackageRegistrationKey]
INNER JOIN PackageRegistrations tpr (NOLOCK) ON tpr.[Key] = r.[ToPackageRegistrationKey]
WHERE r.TransferPopularity != 0 AND r.[Key] >= @skip
ORDER BY r.[Key] ASC
";

        public DatabaseAuxiliaryDataFetcher(
            ISqlConnectionFactory<GalleryDbConfiguration> connectionFactory,
            IEntitiesContextFactory entitiesContextFactory,
            IAzureSearchTelemetryService telemetryService,
            ILogger<DatabaseAuxiliaryDataFetcher> logger)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _entitiesContextFactory = entitiesContextFactory ?? throw new ArgumentNullException(nameof(entitiesContextFactory));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger;
        }

        public async Task<string[]> GetOwnersOrEmptyAsync(string id)
        {
            var stopwatch = Stopwatch.StartNew();
            using (var entitiesContext = await _entitiesContextFactory.CreateAsync(readOnly: true))
            {
                _logger.LogInformation("Fetching owners for package registration with ID {PackageId}.", id);
                var owners = await entitiesContext
                    .PackageRegistrations
                    .Where(pr => pr.Id == id)
                    .Select(pr => pr.Owners.Select(u => u.Username).ToList())
                    .FirstOrDefaultAsync();

                if (owners == null)
                {
                    _logger.LogWarning("No package registration with ID {PackageId} was found. Assuming no owners.", id);
                    return EmptyStringArray;
                }

                if (owners.Count == 0)
                {
                    _logger.LogInformation("The package registration with ID {PackageId} has no owners.", id);
                    return EmptyStringArray;
                }

                // Sort the usernames in a consistent manner.
                var sortedOwners = owners
                    .OrderBy(o => o, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                stopwatch.Stop();
                _telemetryService.TrackGetOwnersForPackageId(sortedOwners.Length, stopwatch.Elapsed);
                _logger.LogInformation("The package registration with ID {PackageId} has {Count} owners.", id, sortedOwners.Length);
                return sortedOwners;
            }
        }

        public async Task<HashSet<string>> GetVerifiedPackagesAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            using (var connection = await _connectionFactory.OpenAsync())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = GetVerifiedPackagesSql;

                using (var reader = await command.ExecuteReaderAsync())
                {
                    var output = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    while (await reader.ReadAsync())
                    {
                        var id = reader.GetString(0);
                        output.Add(id);
                    }

                    stopwatch.Stop();
                    _telemetryService.TrackReadLatestVerifiedPackagesFromDatabase(output.Count, stopwatch.Elapsed);

                    return output;
                }
            }
        }

        public async Task<SortedDictionary<string, SortedSet<string>>> GetPackageIdToOwnersAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            using (var connection = await _connectionFactory.OpenAsync())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = GetPackageIdToOwnersSql;

                using (var reader = await command.ExecuteReaderAsync())
                {
                    var builder = new PackageIdToOwnersBuilder(_logger);
                    while (await reader.ReadAsync())
                    {
                        var id = reader.GetString(0);
                        var username = reader.GetString(1);

                        builder.Add(id, username);
                    }

                    var output = builder.GetResult();
                    stopwatch.Stop();
                    _telemetryService.TrackReadLatestOwnersFromDatabase(output.Count, stopwatch.Elapsed);

                    return output;
                }
            }
        }

        public async Task<SortedDictionary<string, SortedSet<string>>> GetPackageIdToPopularityTransfersAsync()
        {
            await Task.Yield();

            var builder = new PackageIdToPopularityTransfersBuilder(_logger);
            	
            var popularityTransfers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                // Azure SDK
                { "Microsoft.Azure.Storage.Common", new List<string> { "Azure.Storage.Blobs", "Azure.Storage.Queues", "Azure.Storage.Files.Shares" } }
                { "Microsoft.Azure.CosmosDB.Table", new List<string> { "Microsoft.Azure.Cosmos.Table" } },
                { "Microsoft.Azure.EventHubs", new List<string> { "Azure.Messaging.EventHubs" } },
                { "Microsoft.Azure.EventHubs.Processor", new List<string> { "Azure.Messaging.EventHubs.Processor" } },
                { "Microsoft.Azure.KeyVault", new List<string> { "Azure.Security.KeyVault.Keys", "Azure.Security.KeyVault.Secrets", "Azure.Security.KeyVault.Certificates" } },
                { "Microsoft.Azure.KeyVault.Core", new List<string> { "Azure.Security.KeyVault.Keys", "Azure.Security.KeyVault.Secrets", "Azure.Security.KeyVault.Certificates" } },
                { "Microsoft.Azure.KeyVault.Cryptography", new List<string> { "Azure.Security.KeyVault.Keys", "Azure.Security.KeyVault.Secrets", "Azure.Security.KeyVault.Certificates" } },
                { "Microsoft.Azure.KeyVault.Extensions", new List<string> { "Azure.Security.KeyVault.Keys", "Azure.Security.KeyVault.Secrets", "Azure.Security.KeyVault.Certificates" } },
                { "Microsoft.Azure.KeyVault.WebKey", new List<string> { "Azure.Security.KeyVault.Keys", "Azure.Security.KeyVault.Secrets", "Azure.Security.KeyVault.Certificates" } },
                { "Microsoft.Azure.ServiceBus", new List<string> { "Azure.Messaging.ServiceBus" } },
                { "Microsoft.Azure.Storage.Blob", new List<string> { "Azure.Storage.Blobs" } },
                { "Microsoft.Azure.Storage.File", new List<string> { "Azure.Storage.Files.Shares" } },
                { "Microsoft.Azure.Storage.Queue", new List<string> { "Azure.Storage.Queues" } },
                { "WindowsAzure.ServiceBus", new List<string> { "Azure.Messaging.ServiceBus", "Azure.Messaging.EventHubs" } },
                { "WindowsAzure.Storage", new List<string> { "Azure.Storage.Blobs", "Azure.Storage.Queues" } },

                // ASP.NET Core -> Azure migration
                { "Microsoft.Extensions.Configuration.AzureKeyVault", new List<string> { "Azure.Extensions.Configuration.Secrets" } },
                { "Microsoft.AspNetCore.DataProtection.AzureKeyVault", new List<string> { "Azure.AspNetCore.DataProtection.Keys" } },
                { "Microsoft.AspNetCore.DataProtection.AzureStorage", new List<string> { "Azure.AspNetCore.DataProtection.Blobs" } },

                // Community
                //{ "Microsoft.Net.Compilers", new List<string> { "Microsoft.Net.Compilers.Toolset" } },
                //{ "iTextSharp", new List<string> { "itext7" } },
                //{ "KendoUIWeb", new List<string> { "KendoUICore" } },
                //{ "Optional", new List<string> { "Optional.Collections", "Optional.Utilities" } },
            };

            foreach (var popularityTransfer in popularityTransfers)
            {
                var fromId = popularityTransfer.Key;

                foreach (var toId in popularityTransfer.Value)
                {
                    builder.Add(fromId, toId);
                }
            }

            return builder.GetResult();
        }
    }
}

