// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// A custom analyzer for case-insensitive exact matching.
    /// </summary>
    public class ExactMatchCustomAnalyzer
    {
        public const string Name = "case_insensitive_exact_match";

        public static readonly CustomAnalyzer Instance = new CustomAnalyzer(
            Name,
            TokenizerName.Keyword,
            new List<TokenFilterName>
            {
                TokenFilterName.Lowercase
            });
    }
}
