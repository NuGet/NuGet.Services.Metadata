// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Storage;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Assists with initializing a <see cref="ValidationCollector"/>.
    /// </summary>
    public class ValidationCollectorFactory
    {
        private ILoggerFactory _loggerFactory;
        private ILogger<ValidationCollectorFactory> _logger;

        public class Result
        {
            public Result(ValidationCollector collector, ReadWriteCursor front, ReadCursor back)
            {
                Collector = collector;
                Front = front;
                Back = back;
            }

            public ValidationCollector Collector { get; }
            public ReadWriteCursor Front { get; }
            public ReadCursor Back { get; }
        }

        public ValidationCollectorFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<ValidationCollectorFactory>();
        }

        public Result Create(
            IStorageQueue<PackageValidatorContext> queue,
            string catalogIndexUrl,
            Persistence.IStorageFactory monitoringStorageFactory,
            IEnumerable<EndpointFactory.Input> endpointInputs,
            Func<HttpMessageHandler> messageHandlerFactory)
        {
            var collector = new ValidationCollector(
                queue,
                new Uri(catalogIndexUrl),
                _loggerFactory.CreateLogger<ValidationCollector>(),
                messageHandlerFactory);

            var front = GetFront(monitoringStorageFactory);
            var back = new AggregateCursor(endpointInputs.Select(input => new HttpReadCursor(input.CursorUri, messageHandlerFactory)));

            return new Result(collector, front, back);
        }

        public static DurableCursor GetFront(Persistence.IStorageFactory storageFactory)
        {
            var storage = storageFactory.Create();
            return new DurableCursor(storage.ResolveUri("cursor.json"), storage, MemoryCursor.MinValue);
        }
    }
}