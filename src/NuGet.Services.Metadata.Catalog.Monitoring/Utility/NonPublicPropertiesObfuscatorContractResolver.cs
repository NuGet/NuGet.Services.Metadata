// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NuGetGallery.Auditing.Obfuscation;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class NonPublicPropertiesObfuscatorContractResolver : ObfuscatorContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            if (!property.Writable)
            {
                var propertyInfo = member as PropertyInfo;
                if (propertyInfo != null)
                {
                    property.Writable = propertyInfo.GetSetMethod(true) != null;
                }
            }

            return property;
        }
    }
}
