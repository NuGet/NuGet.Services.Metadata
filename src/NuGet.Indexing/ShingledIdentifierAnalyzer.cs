// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.IO;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.Shingle;
using Lucene.Net.Util;

namespace NuGet.Indexing
{
    public class ShingledIdentifierAnalyzer : Analyzer
    {
        public override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
        {
            var source = new DotTokenizer(reader);
            var filter = new LowerCaseFilter(LuceneVersion.LUCENE_48, new ShingleFilter(source));

            return new TokenStreamComponents(source, filter);
        }
    }
}
