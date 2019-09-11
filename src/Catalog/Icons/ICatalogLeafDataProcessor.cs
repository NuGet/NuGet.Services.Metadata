// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.Icons
{
    public interface ICatalogLeafDataProcessor
    {
        Task InitializeIconUrlCache(CancellationToken cancellationToken);
        Task ProcessPackageDelete(Storage storage, CatalogCommitItem item, CancellationToken cancellationToken);
        Task ProcessPackageDetails(Storage destinationStorage, CatalogCommitItem item, string iconUrlString, string iconFile, CancellationToken cancellationToken);
        Task StoreIconUrlCache(CancellationToken cancellationToken);
    }
}