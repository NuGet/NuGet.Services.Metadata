// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Indexing;
using NuGet.Services.KeyVault;
using System.Threading.Tasks;

namespace NuGet.Services.BasicSearch
{
    public interface ISecretReaderFactory
    {
        ISecretInjector CreateSecretInjector(ISecretReader secretReader);

        Task<ISecretReader> CreateSecretReader(IArgumentsDictionary configuration);
    }
}