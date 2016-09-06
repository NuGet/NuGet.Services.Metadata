// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Indexing;
using NuGet.Services.KeyVault;
using System.Threading.Tasks;

namespace NuGet.Services.BasicSearch
{
    public class EmptySecretReaderFactory : ISecretReaderFactory
    {
        public ISecretInjector CreateSecretInjector(ISecretReader secretReader)
        {
            return new SecretInjector(secretReader);
        }

        public Task<ISecretReader> CreateSecretReader(IArgumentsDictionary arguments)
        {
            return Task.FromResult<ISecretReader>(new EmptySecretReader());
        }
    }
}