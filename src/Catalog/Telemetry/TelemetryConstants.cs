// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Metadata.Catalog
{
    public static class TelemetryConstants
    {
        public const string CatalogIterationProcessing = "CatalogIterationProcessing";
        public const string CatalogPackageProcessing = "CatalogPackageProcessing";
        public const string CatalogPackageDownloadDuration = "CatalogPackageDownloadDuration";

        public const string Destination = "Destination";
        public const string Deletes = "Deletes";
        public const string Creates = "Creates";
        public const string Edits = "Edits";

        public const string Id = "Id";
        public const string Version = "Version";
        public const string Size = "Size";

        public const string HttpHeaderDurationSeconds = "HttpHeaderDurationSeconds";
        public const string Method = "Method";
        public const string Uri = "Uri";
        public const string Success = "Success";
        public const string StatusCode = "StatusCode";
        public const string ContentLength = "ContentLength";

        public const string CatalogIndexReadDurationSeconds = "CatalogIndexReadDurationSeconds";

    }
}
