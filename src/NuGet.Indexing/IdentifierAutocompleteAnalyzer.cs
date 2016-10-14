// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.IO;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.Ngram;
using Lucene.Net.Util;

namespace NuGet.Indexing
{
    public class IdentifierAutocompleteAnalyzer : Analyzer
    {
        public override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
        {
            var source = new DotTokenizer(reader);
            var filter = new EdgeNGramTokenFilter(LuceneVersion.LUCENE_48, new LowerCaseFilter(LuceneVersion.LUCENE_48, new CamelCaseFilter(source)), 1, 8);

            return new TokenStreamComponents(source, filter);
        }
    }
}