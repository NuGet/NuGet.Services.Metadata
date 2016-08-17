// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGet.Services.Metadata.Catalog.Json
{
    public class PropertyFilteringJsonReader
        : InterceptingJsonReader
    {
        public PropertyFilteringJsonReader(JsonReader innerReader, IEnumerable<string> interceptPaths)
            : base(innerReader, interceptPaths)
        {
        }

        public override bool Read()
        {
            if (!InnerReader.Read())
            {
                return false;
            }

            if (InnerReader.TokenType == JsonToken.PropertyName && TestPath(InnerReader.Path))
            {
                // Always read matching property names
                return true;
            }
            else if (InnerReader.TokenType == JsonToken.PropertyName)
            {
                // Skip property names that do not match
                InnerReader.Skip();

                return Read();
            }

            return true;
        }

        protected override bool OnReadInterceptedPropertyName()
        {
            // no-op
            return true;
        }
    }
}