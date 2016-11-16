// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Configuration;

namespace NuGet.IndexingTests
{
    internal class EmptyConfigurationProvider : ConfigurationProvider
    {
        protected override Task<string> Get(string key)
        {
            return Task.FromResult(string.Empty);
        }
    }
}
