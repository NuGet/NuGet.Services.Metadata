// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class StringStorageContentWithETag : StringStorageContent
    {
        public StringStorageContentWithETag(
            string content,
            string eTag,
            string contentType = "",
            string cacheControl = "")
            : base(content, contentType, cacheControl)
        {
            ETag = eTag;
        }

        public string ETag { get; }
    }
}
