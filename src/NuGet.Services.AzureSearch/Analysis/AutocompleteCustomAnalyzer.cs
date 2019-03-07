// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// The custom analyzer for package id autocompletion.
    /// </summary>
    public static class AutocompleteCustomAnalyzer
    {
        public const string Name = "nuget_autocomplete_analyzer";

        public static readonly CustomAnalyzer Instance = new CustomAnalyzer(
            Name,
            PackageIdCustomTokenizer.Name,
            new List<TokenFilterName>
            {
                IdentifierCustomTokenFilter.Name,
                TokenFilterName.Lowercase,
                AutocompleteCustomTokenFilter.Name
            });
    }
}
