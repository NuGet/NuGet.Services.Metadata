// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using System;
using System.Collections.Generic;
using Lucene.Net.Analysis.Miscellaneous;

namespace NuGet.Indexing
{
    public class PackageAnalyzer : AnalyzerWrapper
    {
        static readonly IDictionary<string, Analyzer> _fieldAnalyzers;

        static PackageAnalyzer()
        {
            _fieldAnalyzers = new Dictionary<string, Analyzer>(StringComparer.OrdinalIgnoreCase)
            {
                { "Id", new IdentifierKeywordAnalyzer() },
                { "IdAutocomplete", new IdentifierAutocompleteAnalyzer() },
                { "TokenizedId", new IdentifierAnalyzer() },
                { "ShingledId", new ShingledIdentifierAnalyzer() },
                { "Version", new VersionAnalyzer() },
                { "Title", new DescriptionAnalyzer() },
                { "Description", new DescriptionAnalyzer() },
                { "Summary", new DescriptionAnalyzer() },
                { "Authors", new DescriptionAnalyzer() },
                { "Owner", new OwnerAnalyzer() },
                { "Tags", new TagsAnalyzer() }
            };
        }

        public PackageAnalyzer()
            : base()
        {
        }

        protected override Analyzer GetWrappedAnalyzer(string fieldName)
        {
            if (_fieldAnalyzers.ContainsKey(fieldName))
            {
                return _fieldAnalyzers[fieldName];
            }

            return new StandardAnalyzer(Lucene.Net.Util.LuceneVersion.LUCENE_48);
        }
    }
}
