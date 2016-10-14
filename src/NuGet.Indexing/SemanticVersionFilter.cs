// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Tokenattributes;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Util;
using NuGet.Versioning;

namespace NuGet.Indexing
{
    public class OwnersFilter : Filter
    {
        private readonly OwnersResult _owners;
        private readonly string _owner;

        public OwnersFilter(OwnersResult owners, string owner)
        {
            _owners = owners;
            _owner = owner;
        }

        public override DocIdSet GetDocIdSet(AtomicReaderContext context, Bits acceptDocs)
        {
            SegmentReader segmentReader = context.Reader as SegmentReader;

            var readerName = segmentReader != null
                    ? segmentReader.SegmentName
                    : string.Empty;

            IDictionary<string, DynamicDocIdSet> segmentOwnersMapping;
            if (_owners.Mappings.TryGetValue(readerName, out segmentOwnersMapping))
            {
                DynamicDocIdSet docIdSet;
                if (segmentOwnersMapping.TryGetValue(_owner, out docIdSet))
                {
                    return docIdSet;
                }
            }

            return null;
        }
    }
    public class SemanticVersionFilter : TokenFilter
    {
        ICharTermAttribute _termAttribute;

        public SemanticVersionFilter(TokenStream stream)
            : base(stream)
        {
            _termAttribute = AddAttribute<ICharTermAttribute>();
        }

        public sealed override bool IncrementToken()
        {
            if (!input.IncrementToken())
            {
                return false;
            }

            string version = _termAttribute.ToString();

            NuGetVersion nuGetVersion;
            if (NuGetVersion.TryParse(version, out nuGetVersion))
            {
                version = nuGetVersion.ToNormalizedString();
            }

            _termAttribute.CopyBuffer(version.ToCharArray(), 0, version.Length);

            return true;
        }
    }
}
