// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Lucene.Net.Search;
using System.Collections.Generic;
using Lucene.Net.Index;
using static Lucene.Net.Search.FieldCache;

namespace NuGet.Indexing
{
    public class KeyCollector : Collector
    {
        private Ints _keys;
        private Ints _checksums;
        private IList<DocumentKey> _pairs;

        public KeyCollector(IList<DocumentKey> pairs)
        {
            _pairs = pairs;
        }

        public override AtomicReaderContext NextReader
        {
            set
            {
                _keys = DEFAULT.GetInts(value.AtomicReader, "Keys", false);
                _checksums = DEFAULT.GetInts(value.AtomicReader, "Checksum", false);
            }
        }

        public override Scorer Scorer
        {
            set { }
        }

        public override bool AcceptsDocsOutOfOrder()
        {
            return true;
        }

        public override void Collect(int docID)
        {
            _pairs.Add(new DocumentKey(_keys.Get(docID), docID, _checksums.Get(docID)));
        }
    }

    public class DocumentKey
    {
        public int PackageKey { get; private set; }
        public int DocumentId { get; private set; }
        public int Checksum { get; private set; }

        public DocumentKey(int packageKey, int documentId, int checksum)
        {
            PackageKey = packageKey;
            DocumentId = documentId;
            Checksum = checksum;
        }
    }
}
