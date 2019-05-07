// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class StringStorageContentWithAccessCondition : StringStorageContent
    {
        public StringStorageContentWithAccessCondition(
            string content, 
            AccessCondition accessCondition, 
            string contentType = "", 
            string cacheControl = "")
            : base(content, contentType, cacheControl)
        {
            AccessCondition = accessCondition;
        }

        public AccessCondition AccessCondition { get; }
    }
}
