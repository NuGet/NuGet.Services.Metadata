// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Metadata.Catalog.Icons
{
    public interface IIconCopyResultCache
    {
        ExternalIconCopyResult GetCachedResult(Uri iconUrl);
        void StoreCachedResult(Uri iconUrl, ExternalIconCopyResult newItem);
    }
}