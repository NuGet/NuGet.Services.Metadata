// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// Shingles package id tokens. 
    /// Example input: "Foo", "Bar", "Qux"
    /// Example output: "Foo", "Bar", "Qux", "FooBar", "BarQux"
    /// </summary>
    public static class PackageIdShinglerCustomTokenFilter
    {
        public const string Name = "nuget_id_shingle_filter";

        public static ShingleTokenFilter Instance = new ShingleTokenFilter(
            Name,
            minShingleSize: 2,
            maxShingleSize: 3,
            outputUnigrams: true,
            tokenSeparator: string.Empty);
    }
}
