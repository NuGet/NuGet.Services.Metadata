// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGet.Indexing
{
    public static class JTokenExtensions
    {
        public static JArray GetJArray(this JToken token, string key)
        {
            var array = token[key];
            if (array == null)
            {
                return new JArray();
            }

            if (!(array is JArray))
            {
                array = new JArray(array);
            }

            return (JArray)array;
        }
    }
}