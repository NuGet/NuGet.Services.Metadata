// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch
{
    public class AutocompleteCustomTokenFilter
    {
        public const string Name = "nuget_autocomplete_filter";

        public static EdgeNGramTokenFilterV2 Instance = new EdgeNGramTokenFilterV2(
            Name,
            minGram: 1,
            maxGram: 8,
            side: EdgeNGramTokenFilterSide.Front);
    }
}
