// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Util;

namespace NuGet.Indexing
{
    //  this class should be equivallent to:
    //
    //    new QueryWrapperFilter(new TermQuery(new Term("tenantId", tenantId)))
    //
    //  an alternative implementation might be to use that inline or subclass from it

    public class TenantFilter : Filter
    {
        string _tenantId;

        public TenantFilter(string tenantId)
        {
            _tenantId = tenantId;
        }

        public override DocIdSet GetDocIdSet(AtomicReaderContext context, Bits acceptDocs)
        {
            var reader = context.AtomicReader;
            var bitSet = new OpenBitSet(reader.NumDocs);
            var termsEnum = reader.TermDocsEnum(new Term("TenantId", _tenantId));

            while (termsEnum.NextDoc() != DocIdSetIterator.NO_MORE_DOCS)
            {
                if (termsEnum.Freq() > 0)
                {
                    bitSet.Set(termsEnum.DocID());
                }
            }
            return bitSet;
        }
    }
}
