// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Azure.Search.Models;
using System.Collections.Generic;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// Filters characters that are ASCII but not alphanumeric.
    /// </summary>
    public static class AsciiNonAlphanumericCharFilter
    {
        public const string Name = "nuget_ascii_nonalphanumeric_char_filter";

        public static MappingCharFilter Instance = new MappingCharFilter(
            Name,
            new List<string>
            {
                "!=>",
                "\"=>",
                "#=>",
                "$=>",
                "%=>",
                "&=>",
                "'=>",
                "(=>",
                ")=>",
                "*=>",
                "+=>",
                ",=>",
                "-=>",
                ".=>",
                "/=>",
                ":=>",
                ";=>",
                "<=>",
                "==>",
                ">=>",
                "?=>",
                "@=>",
                "[=>",
                "\\=>",
                "]=>",
                "^=>",
                "_=>",
                "`=>",
                "{=>",
                "|=>",
                "}=>",
                "~=>",
            });
    }
}
