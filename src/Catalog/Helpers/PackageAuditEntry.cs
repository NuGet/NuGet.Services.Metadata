// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Auditing;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    public class PackageAuditEntry
    {
        public PackageAuditRecord Record { get; set; }
        public AuditActor Actor { get; set; }

        public PackageAuditEntry(PackageAuditRecord record, AuditActor actor)
        {
            Record = record;
            Actor = actor;
        }
    }
}
