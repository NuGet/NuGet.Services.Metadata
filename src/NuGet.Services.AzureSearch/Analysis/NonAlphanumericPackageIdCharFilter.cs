// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Azure.Search.Models;
using System.Collections.Generic;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// Filters valid package id characters that not alphanumeric.
    /// </summary>
    public static class NonAlphanumericPackageIdCharFilter
    {
        public const string Name = "nuget_nonalphanumeric_package_id_char_filter";

        public static MappingCharFilter Instance = new MappingCharFilter(
            Name,
            new List<string>
            {
                "_ => ",
                ". => ",
                "- => ",
            });
    }
}
