// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Configuration;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Icons;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace Ng.Jobs
{
    public class Catalog2IconJob : LoopingNgJob
    {
        private const int DegreeOfParallelism = 100;
        private IconsCollector _collector;
        private DurableCursor _front;

        public Catalog2IconJob(ITelemetryService telemetryService, ILoggerFactory loggerFactory)
            : base(telemetryService, loggerFactory)
        {
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            ServicePointManager.DefaultConnectionLimit = DegreeOfParallelism;

            var verbose = arguments.GetOrDefault(Arguments.Verbose, false);
            var cursorStorageFactory = CreateIconCursorStorageFactory(arguments, verbose);
            var source = arguments.GetOrThrow<string>(Arguments.Source);
            _collector = new IconsCollector(
                new Uri(source),
                TelemetryService,
                CommandHelpers.GetHttpMessageHandlerFactory(TelemetryService, verbose),
                LoggerFactory.CreateLogger<IconsCollector>());
            var cursorStorage = cursorStorageFactory.Create();
            _front = new DurableCursor(cursorStorage.ResolveUri("c2icursor.json"), cursorStorage, DateTime.MinValue.ToUniversalTime());
        }

        protected override async Task RunInternalAsync(CancellationToken cancellationToken)
        {
            bool run;
            do
            {
                run = await _collector.RunAsync(_front, MemoryCursor.CreateMax(), cancellationToken);
            } while (run);
        }

        private IStorageFactory CreateIconCursorStorageFactory(IDictionary<string, string> arguments, bool verbose)
        {
            return CommandHelpers.CreateSuffixedStorageFactory("Icon", arguments, verbose);
        }
    }
}
