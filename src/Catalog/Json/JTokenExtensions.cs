// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Metadata.Catalog.Json
{
    public static class JTokenExtensions
    {
        public static JToken FilterClone(this JToken original, string[] jsonPaths)
        {
            return JToken.Load(original.CreateReader().FilterPaths(jsonPaths));
        }

        public static JsonReader FilterPaths(this JsonReader original, string[] jsonPaths)
        {
            return new PropertyFilteringJsonReader(original, jsonPaths);
        }

        public static JToken SkipClone(this JToken original, string[] jsonPaths)
        {
            return JToken.Load(original.CreateReader().SkipPaths(jsonPaths));
        }

        public static JsonReader SkipPaths(this JsonReader original, string[] jsonPaths)
        {
            return new PropertySkippingJsonReader(original, jsonPaths);
        }
    }
}