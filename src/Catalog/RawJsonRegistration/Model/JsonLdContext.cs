// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json.Linq;

namespace NuGet.Services.Metadata.Catalog.RawJsonRegistration.Model
{
    public static class JsonLdContext
    {
        public static readonly JObject Registration;
        public static readonly JObject Package;

        static JsonLdContext()
        {
            Registration = JObject.Parse(Utils.GetResource("context.Registration.json"));
            Package = JObject.Parse(Utils.GetResource("context.Package.json"));
        }
    }
}