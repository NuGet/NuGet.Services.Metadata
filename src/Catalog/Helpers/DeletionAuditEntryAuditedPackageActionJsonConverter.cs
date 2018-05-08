// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGetGallery.Auditing;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    public class DeletionAuditEntryAuditedPackageActionJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(AuditedPackageAction);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            try
            {
                return JProperty.Load(reader).ToObject<AuditedPackageAction>(serializer);
            }
            catch
            {
                return AuditedPackageAction.Delete;
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanWrite => false;
    }
}
