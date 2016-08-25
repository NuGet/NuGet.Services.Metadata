// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Metadata.Catalog.Json
{
    public static class JObjectExtensions
    {
        public static void CopyPropertyFrom(this JObject target, JObject source, string propertyName)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (string.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentNullException(nameof(propertyName));
            }

            if (source[propertyName] != null)
            {
                target[propertyName] = source[propertyName].DeepClone();
            }
        }

        public static void CopyPropertyFrom(this JObject target, JObject source, string propertyName, JToken fallbackValue)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (string.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentNullException(nameof(propertyName));
            }

            if (source[propertyName] != null)
            {
                target[propertyName] = source[propertyName].DeepClone();
            }
            else
            {
                target[propertyName] = fallbackValue;
            }
        }
    }
}