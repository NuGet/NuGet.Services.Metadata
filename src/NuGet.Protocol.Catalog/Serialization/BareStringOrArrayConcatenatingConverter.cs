// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

namespace NuGet.Protocol.Catalog
{
    /// <summary>
    /// A <see cref="JsonConverter"/> implementation that leaves the bare string values as is, but converts arrays of strings
    /// into single strings by concatenating all elements.
    /// 
    /// Exists to work around Catalog issues like in the following leaf:
    /// https://api.nuget.org/v3/catalog0/data/2018.03.11.05.06.09/fluentconsoleapplication.0.1.0.json
    /// (see releaseNotes).
    /// </summary>
    public class BareStringOrArrayConcatenatingConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(string);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartArray)
            {
                var array = serializer.Deserialize<string[]>(reader);
                return string.Join("", array);
            }

            return serializer.Deserialize<string>(reader);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value, typeof(string));
        }
    }
}
