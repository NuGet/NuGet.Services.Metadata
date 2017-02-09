// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.ApplicationInsights.Owin
{
    public class ExceptionTrackingMiddleware
        : OwinMiddleware
    {
        public const string OwinRequestIdKey = "owin.RequestId";

        private readonly TelemetryClient _telemetryClient;

        public ExceptionTrackingMiddleware(OwinMiddleware next)
            : this(next, null)
        {
        }

        public ExceptionTrackingMiddleware(OwinMiddleware next, TelemetryConfiguration telemetryConfiguration) 
            : base(next)
        {
            _telemetryClient = telemetryConfiguration == null
                ? new TelemetryClient()
                : new TelemetryClient(telemetryConfiguration);
        }

        public override async Task Invoke(IOwinContext context)
        {
            try
            {
                await this.Next.Invoke(context);
            }
            catch (Exception e)
            {
                this._telemetryClient.TrackException(e);
            }
        }
    }
}
