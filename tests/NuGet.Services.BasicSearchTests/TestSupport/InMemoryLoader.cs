// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet.Indexing;

namespace NuGet.Services.BasicSearchTests.TestSupport
{
    public class InMemoryLoader : Dictionary<string, string>, ILoader
    {
        public JsonReader GetReader(string name)
        {
            return new JsonTextReader(new StringReader(this[name]));
        }

        public Task<bool> Reload()
        {
            // no-op because in-memory files do not need to be reloaded
            return Task.FromResult(false);
        }
    }
}