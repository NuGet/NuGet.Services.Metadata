// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGetGallery;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public interface IExcludeIdDataClient
    {
        Task<ResultAndAccessCondition<ExcludeIdData>> ReadLatestIndexedAsync();
        Task ReplaceLatestIndexedAsync(ExcludeIdData newData, IAccessCondition accessCondition);
    }
}