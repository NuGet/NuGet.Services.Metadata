// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Util;

namespace NuGet.Indexing
{
    public class BitSetCollector : Collector
    {
        int _docBase;
        OpenBitSet _bitSet;

        public override Scorer Scorer
        {
            set { }
        }

        public override AtomicReaderContext NextReader
        {
            set
            {
                _docBase = value.DocBase;
            }
        }

        public BitSetCollector(OpenBitSet bitSet)
        {
            _bitSet = bitSet;
        }

        public override bool AcceptsDocsOutOfOrder()
        {
            return true;
        }

        public override void Collect(int doc)
        {
            _bitSet.Set(doc + _docBase);
        }

        public static OpenBitSet CreateBitSet(IndexReader reader, Filter filter)
        {
            IndexSearcher searcher = new IndexSearcher(reader);
            OpenBitSet result = new OpenBitSet();
            searcher.Search(new MatchAllDocsQuery(), filter, new BitSetCollector(result));
            return result;
        }
    }
}
