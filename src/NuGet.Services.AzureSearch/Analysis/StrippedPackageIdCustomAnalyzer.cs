// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// An analyzer for the package id, stripped of any non-alphanumeric characters and lowercased.
    /// Folds unicode characters into their equivalent ascii character, if one exists.
    /// </summary>
    public static class StrippedPackageIdCustomAnalyzer
    {
        public const string Name = "nuget_stripped_package_id_analyzer";

        public static readonly CustomAnalyzer Instance = new CustomAnalyzer(
            Name,
            TokenizerName.Keyword,
            new List<TokenFilterName>
            {
                TokenFilterName.AsciiFolding,
                TokenFilterName.Lowercase,
                TokenFilterName.Truncate,
            },
            new List<CharFilterName>
            {
                // The ASCII Folding Token Filter does not remove nonalphanumerical ASCII characters.
                // Char filters are applied before the tokenizer and token filters.
                NonAlphanumericPackageIdCharFilter.Name
            });
    }
}
