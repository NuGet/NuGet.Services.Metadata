// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class PackageMonitoringStatusListItem
    {
        public FeedPackageIdentity Package { get; private set; }

        public PackageState State { get; private set; }

        public PackageMonitoringStatusListItem(FeedPackageIdentity package, PackageState state)
        {
            Package = package;
            State = state;
        }
    }
}