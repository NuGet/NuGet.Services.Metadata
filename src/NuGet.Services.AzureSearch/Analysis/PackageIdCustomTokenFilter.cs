// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// Splits tokens on camel casing and non alpha-numeric characters.
    /// </summary>
    public static class PackageIdCustomTokenFilter
    {
        public const string Name = "package_id_delimiter";

        public static WordDelimiterTokenFilter Instance = new WordDelimiterTokenFilter(
            Name,
            splitOnCaseChange: true);
    }
}
