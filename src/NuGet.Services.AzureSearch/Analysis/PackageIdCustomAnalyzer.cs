// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// Support for NuGet style package id searching.
    /// See <see cref="PackageIdCustomTokenFilter"/> for more information.
    /// </summary>
    public static class PackageIdCustomAnalyzer
    {
        public const string Name = "package_identifier";

        public static readonly CustomAnalyzer Instance = new CustomAnalyzer(
            Name,
            TokenizerName.Keyword,
            new List<TokenFilterName>
            {
                TokenFilterName.Lowercase,
                PackageIdCustomTokenFilter.Name,
            });
    }
}
