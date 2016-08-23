// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Tokenattributes;
using Lucene.Net.Index;
using Lucene.Net.Search;
using NuGet.Versioning;

namespace NuGet.Indexing
{
    class PackageTypeFilter : Filter
    {
        private readonly PackageTypesResult _packageTypes;
        private readonly string _packageType;

        public PackageTypeFilter(PackageTypesResult packageTypes, string packageType)
        {
            _packageTypes = packageTypes;
            _packageType = packageType;
        }

        public override DocIdSet GetDocIdSet(IndexReader reader)
        {
            SegmentReader segmentReader = reader as SegmentReader;

            var readerName = segmentReader != null
                    ? segmentReader.SegmentName
                    : string.Empty;

            IDictionary<string, DynamicDocIdSet> segmentOwnersMapping;
            if (_packageTypes.Mappings.TryGetValue(readerName, out segmentOwnersMapping))
            {
                DynamicDocIdSet docIdSet;
                if (segmentOwnersMapping.TryGetValue(_packageType, out docIdSet))
                {
                    return docIdSet;
                }
            }

            return null;
        }
    }
}
