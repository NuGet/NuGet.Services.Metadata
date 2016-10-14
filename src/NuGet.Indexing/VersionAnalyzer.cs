// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.IO;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Core;

namespace NuGet.Indexing
{
    public class VersionAnalyzer : Analyzer
    {
        public override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
        {
            var source = new KeywordTokenizer(reader);
            var filter = new SemanticVersionFilter(source);

            return new TokenStreamComponents(source, filter);
        }
    }
}
