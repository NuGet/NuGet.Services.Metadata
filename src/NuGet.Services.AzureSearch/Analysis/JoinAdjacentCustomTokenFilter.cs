// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// Joins adjacent tokens without consuming tokens.
    /// For example, "a" and "b" would become "a", "b", and "ab".
    /// </summary>
    public class JoinAdjacentCustomTokenFilter
    {
        public const string Name = "nuget-join-adjacent";

        public static readonly ShingleTokenFilter Instance = new ShingleTokenFilter(
            Name,
            minShingleSize: 2,
            maxShingleSize: 2,
            outputUnigrams: true,
            tokenSeparator: string.Empty);
    }
}
