// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    public class DeletionAuditEntryAuditedEntityJsonConverter<T> : JsonConverter
    {
        public DeletionAuditEntryAuditedEntityJsonConverter()
        {
        }

        public override bool CanConvert(Type objectType)
        {
            return 
                objectType == typeof(T);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var serializerWithoutThisConverter = 
                JsonSerializer.Create(
                    new JsonSerializerSettings
                    {
                        ContractResolver = serializer.ContractResolver,
                        Converters = serializer.Converters.Where(c => c != this).ToList()
                    });

            try
            {
                return JObject.Load(reader).ToObject(objectType, serializerWithoutThisConverter);
            }
            catch
            {
                if (reader.TokenType != JsonToken.StartArray)
                {
                    throw;
                }

                var jArray = JArray.Load(reader);
                var auditedEntities = jArray.ToObject<IEnumerable<T>>(serializerWithoutThisConverter);
                return auditedEntities.First();
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanWrite => false;
    }
}
