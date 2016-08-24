// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Metadata.Catalog
{
    public static class UriExtensions
    {
        public static string ToAbsoluteString(this Uri current)
        {
            if (current.IsAbsoluteUri)
            {
                return current.AbsoluteUri.ToLowerInvariant();
            }

            return current.ToString().ToLowerInvariant();
        }
    }
}