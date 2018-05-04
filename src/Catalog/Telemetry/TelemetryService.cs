// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights;
using NuGet.Services.Logging;

namespace NuGet.Services.Metadata.Catalog
{
    public class TelemetryService : ITelemetryService
    {
        private readonly TelemetryClient _telemetryClient;
        private readonly TelemetryClientWrapper _telemetryClientWrapper;

        private const string CatalogIndexReadDurationSeconds = "CatalogIndexReadDurationSeconds";
        private const string CatalogIndexWriteDurationSeconds = "CatalogIndexWriteDurationSeconds";

        private const string HttpHeaderDurationSeconds = "HttpHeaderDurationSeconds";
        private const string Method = "Method";
        private const string Uri = "Uri";
        private const string Success = "Success";
        private const string StatusCode = "StatusCode";
        private const string ContentLength = "ContentLength";

        public IDictionary<string, string> GlobalDimensions { get; private set; }

        public TelemetryService(TelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
            _telemetryClientWrapper = new TelemetryClientWrapper(_telemetryClient);

            GlobalDimensions = new Dictionary<string, string>();
        }

        public void TrackCatalogIndexReadDuration(TimeSpan duration, Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            _telemetryClient.TrackMetric(
                CatalogIndexReadDurationSeconds,
                duration.TotalSeconds,
                new Dictionary<string, string>
                {
                    { Uri, uri.AbsoluteUri },
                });
        }

        public void TrackCatalogIndexWriteDuration(TimeSpan duration, Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            _telemetryClient.TrackMetric(
                CatalogIndexWriteDurationSeconds,
                duration.TotalSeconds,
                new Dictionary<string, string>
                {
                    { Uri, uri.AbsoluteUri },
                });
        }
        
        public virtual DurationMetric TrackDuration(string name, IDictionary<string, string> properties = null)
        {
            return new DurationMetric(_telemetryClientWrapper, name, properties);   
        }
    }
}
