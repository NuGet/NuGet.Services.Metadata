// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Services.Metadata.Catalog
{
    public sealed class CatalogCommitItemBatch
    {
        public CatalogCommitItemBatch(DateTime commitTimeStamp, IEnumerable<CatalogCommitItem> items)
        {
            if (items == null || !items.Any())
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullOrEmpty, nameof(items));
            }

            CommitTimeStamp = commitTimeStamp;
            Items = items.ToList();
            Items.Sort();
        }

        public DateTime CommitTimeStamp { get; }
        public List<CatalogCommitItem> Items { get; }
    }
}