// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace Ng
{
    public enum CatalogCommandType
    {
        Created = 0,
        Edited = 10,
        Deleted = 20
    }

    public class CatalogCommand
    {
        public DateTime Timestamp { get; set; }

        public string Id { get; set; }

        public string Version { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime PublishedDate { get; set; }

        public DateTime EditedDate { get; set; }

        public Uri ContentUri { get; set; }

        public string LicenseNames { get; set; }

        public Uri LicenseReportUri { get; set; }

        public CatalogCommandType CommandType { get; set; }

        public override string ToString()
        {
            return string.Format("{0} {1} {2} {3}", CommandType, Id, Version, Timestamp.ToString("O"));
        }
    }

    public class CommandQueue2Catalog
    {
        private const int MaxCatalogPageSize = 550;

        private static async Task WriteCommandsToCatalogAsync(
            HttpClient client, Storage catalogStorage, IEnumerable<CatalogCommand> catalogCommands, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("OPEN CATALOG");

            var catalog = new AppendOnlyCatalogWriter(catalogStorage, MaxCatalogPageSize);

            // Get commit metadata
            var commitMetadata = await PackageCatalog.ReadCommitMetadata(catalog, cancellationToken);
            Trace.TraceInformation("CATALOG LastCreated: {0}", (commitMetadata.LastCreated ?? DateTime.MinValue).ToString("O"));
            Trace.TraceInformation("CATALOG LastEdited: {0}", (commitMetadata.LastEdited ?? DateTime.MinValue).ToString("O"));
            Trace.TraceInformation("CATALOG LastDeleted: {0}", (commitMetadata.LastDeleted ?? DateTime.MinValue).ToString("O"));

            // We want to iterate by timestamp of the message as that determines the order of commands created.
            foreach (var catalogCommand in catalogCommands.OrderBy(command => command.Timestamp))
            {
                // Create catalog item
                CatalogItem catalogItem = null;
                switch (catalogCommand.CommandType)
                {
                    case CatalogCommandType.Created:
                        catalogItem = await CreatePackageDetailsCatalogItemAsync(client, cancellationToken, catalogCommand);
                        if (catalogItem != null)
                        {
                            commitMetadata.LastCreated = catalogCommand.CreatedDate;
                        }
                        break;
                    case CatalogCommandType.Edited:
                        catalogItem = await CreatePackageDetailsCatalogItemAsync(client, cancellationToken, catalogCommand);
                        if (catalogItem != null)
                        {
                            commitMetadata.LastEdited = catalogCommand.EditedDate;
                        }
                        break;
                    case CatalogCommandType.Deleted:
                        catalogItem = new DeleteCatalogItem(catalogCommand.Id, catalogCommand.Version, catalogCommand.Timestamp);
                        commitMetadata.LastDeleted = catalogCommand.Timestamp;
                        break;
                }

                // Append to catalog
                if (catalogItem != null)
                {
                    catalog.Add(catalogItem);
                    Trace.TraceInformation("CatalogItem added from: {0}", catalogCommand);
                }
                else
                {
                    Trace.TraceWarning("No CatalogItem could be added from: {0}", catalogCommand);
                }
            }

            // Log new commit metadata
            Trace.TraceInformation("FEED LastCreated: {0}", (commitMetadata.LastCreated ?? DateTime.MinValue).ToString("O"));
            Trace.TraceInformation("FEED LastEdited: {0}", (commitMetadata.LastEdited ?? DateTime.MinValue).ToString("O"));
            Trace.TraceInformation("FEED LastDeleted: {0}", (commitMetadata.LastDeleted ?? DateTime.MinValue).ToString("O"));

            // Commit
            var commitMetadataGraph = PackageCatalog.CreateCommitMetadata(catalog.RootUri, commitMetadata);
            await catalog.Commit(commitMetadataGraph, cancellationToken);
            Trace.TraceInformation("COMMIT");
        }

        private static async Task<CatalogItem> CreatePackageDetailsCatalogItemAsync(
            HttpClient client, CancellationToken cancellationToken, CatalogCommand catalogCommand)
        {
            if (catalogCommand.CommandType != CatalogCommandType.Created
                && catalogCommand.CommandType != CatalogCommandType.Edited)
            {
                return null;
            }

            var response = await client.GetAsync(catalogCommand.ContentUri, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    var catalogItem = Utils.CreateCatalogItem(stream, catalogCommand.EditedDate, null,
                        catalogCommand.ContentUri.ToString(), catalogCommand.CreatedDate, catalogCommand.EditedDate,
                        catalogCommand.PublishedDate, catalogCommand.LicenseNames, catalogCommand.LicenseReportUri.ToString());

                    if (catalogItem != null)
                    {
                        return catalogItem;
                    }
                    else
                    {
                        Trace.TraceWarning("Unable to extract metadata from: {0}", catalogCommand.ContentUri);
                    }
                }
            }
            else
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    //  the feed is out of sync with the actual package storage - if we don't have the package there is nothing to be done
                    Trace.TraceWarning("Unable to download: {0} http status: {1}", catalogCommand.ContentUri, response.StatusCode);
                }
                else
                {
                    //  this should trigger a restart - of this program - and not move the cursor forward
                    Trace.TraceError(string.Format("Unable to download: {0} http status: {1}", catalogCommand.ContentUri, response.StatusCode));
                    throw new Exception(string.Format("Unable to download: {0} http status: {1}", catalogCommand.ContentUri, response.StatusCode));
                }
            }

            return null;
        }

        protected virtual HttpClient CreateHttpClient(bool verbose)
        {
            var handlerFunc = CommandHelpers.GetHttpMessageHandlerFactory(verbose);

            var handler = (handlerFunc != null) ? handlerFunc() : new WebRequestHandler { AllowPipelining = true };

            return new HttpClient(handler);
        }

        private async Task Loop(
            string gallery, StorageFactory catalogStorageFactory, bool verbose, int interval, CancellationToken cancellationToken)
        {
            // TODO: hook up queue system
            var catalogStorage = catalogStorageFactory.Create();

            var timeout = TimeSpan.FromSeconds(300);

            while (true)
            {
                await ProcessCommandsAsync(gallery, catalogStorage, timeout, verbose, cancellationToken);

                Thread.Sleep(interval * 1000);
            }
            // ReSharper disable once FunctionNeverReturns
        }

        protected async Task ProcessCommandsAsync(
            string gallery, Storage catalogStorage, TimeSpan timeout, bool verbose, CancellationToken cancellationToken)
        {
            var catalogCommands = Enumerable.Empty<CatalogCommand>();

            using (var client = CreateHttpClient(verbose))
            {
                client.Timeout = timeout;

                // TODO: hook up queue system, for now this is our queue :-)

                // We want to ensure a commit only contains each package once at most.
                // Therefore we segment the commands by package id + version.
                var segmentedCatalogCommands = CreateSegmentedCatalogCommands(catalogCommands);
                foreach (var catalogCommandsSegment in segmentedCatalogCommands)
                {
                    await WriteCommandsToCatalogAsync(client, catalogStorage, catalogCommandsSegment, cancellationToken);
                }
            }
        }

        private static IEnumerable<IEnumerable<CatalogCommand>> CreateSegmentedCatalogCommands(
            IEnumerable<CatalogCommand> catalogCommands)
        {
            var currentSegment = new Dictionary<string, CatalogCommand>();
            foreach (var catalogCommand in catalogCommands)
            {
                var key = catalogCommand.Id + "|" + catalogCommand.Version;
                if (currentSegment.ContainsKey(key))
                {
                    // Duplicate, return segment
                    yield return currentSegment.Values;
                    currentSegment.Clear();
                }

                // Add to segment
                currentSegment.Add(key, catalogCommand);
            }

            if (currentSegment.Any())
            {
                yield return currentSegment.Values;
            }
        }

        private static void PrintUsage()
        {
            // todo: update usage string once queues are hooked up
            Console.WriteLine("Usage: ng commandqueue2catalog -gallery <v2-feed-address> -storageBaseAddress <storage-base-address> -storageType file|azure [-storagePath <path>]|[-storageAccountName <azure-acc> -storageKeyValue <azure-key> -storageContainer <azure-container> -storagePath <path>] -storageTypeAuditing file|azure [-storagePathAuditing <path>]|[-storageAccountNameAuditing <azure-acc> -storageKeyValueAuditing <azure-key> -storageContainerAuditing <azure-container> -storagePathAuditing <path>]  [-verbose true|false] [-interval <seconds>] [-startDate <DateTime>]");
        }

        public void Run(string[] args, CancellationToken cancellationToken)
        {
            var arguments = CommandHelpers.GetArguments(args, 1);
            if (arguments == null || arguments.Count == 0)
            {
                PrintUsage();
                return;
            }

            var gallery = CommandHelpers.GetGallery(arguments);
            if (gallery == null)
            {
                PrintUsage();
                return;
            }

            var verbose = CommandHelpers.GetVerbose(arguments);

            var interval = CommandHelpers.GetInterval(arguments);
            
            var catalogStorageFactory = CommandHelpers.CreateStorageFactory(arguments, verbose);
            if (catalogStorageFactory == null)
            {
                PrintUsage();
                return;
            }

            if (verbose)
            {
                Trace.Listeners.Add(new ConsoleTraceListener());
                Trace.AutoFlush = true;
            }

            Trace.TraceInformation("CONFIG source: \"{0}\" storage: \"{1}\" interval: {2}", gallery, catalogStorageFactory, interval);

            Loop(gallery, catalogStorageFactory, verbose, interval, cancellationToken).Wait();
        }
    }
}
