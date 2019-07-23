// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Search;
using NuGet.Indexing;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch.SearchService
{
    public partial class SearchTextBuilder : ISearchTextBuilder
    {
        private const string MatchAllDocumentsQuery = "*";

        private static readonly IReadOnlyDictionary<QueryField, string> FieldNames = new Dictionary<QueryField, string>
        {
            { QueryField.Author, IndexFields.Authors },
            { QueryField.Description, IndexFields.Description },
            { QueryField.Id, IndexFields.TokenizedPackageId },
            { QueryField.Owner, IndexFields.Search.Owners },
            { QueryField.PackageId, IndexFields.PackageId },
            { QueryField.Summary, IndexFields.Summary },
            { QueryField.Tag, IndexFields.Tags },
            { QueryField.Title, IndexFields.Title },
            { QueryField.Version, IndexFields.NormalizedVersion },
        };

        private readonly NuGetQueryParser _parser;

        public SearchTextBuilder()
        {
            _parser = new NuGetQueryParser();
        }

        public ParsedQuery ParseV2Search(V2SearchRequest request)
        {
            var query = request.Query;

            // The old V2 search service would treat "id:" queries (~match) in the same way as it did "packageid:" (==match).
            // If "id:" is in the query, replace it.
            if (request.LuceneQuery && !string.IsNullOrEmpty(query) && query.StartsWith("id:", StringComparison.OrdinalIgnoreCase))
            {
                query = "packageid:" + query.Substring(3);
            }

            return GetParsedQuery(query);
        }

        public ParsedQuery ParseV3Search(V3SearchRequest request)
        {
            return GetParsedQuery(request.Query);
        }

        public string Autocomplete(AutocompleteRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return MatchAllDocumentsQuery;
            }

            // Query package ids. If autocompleting package ids, allow prefix matches.
            var builder = new AzureSearchTextBuilder();

            builder.AppendScopedTerm(
                fieldName: IndexFields.PackageId,
                term: request.Query,
                prefixSearch: request.Type == AutocompleteRequestType.PackageIds);

            return builder.ToString();
        }

        private ParsedQuery GetParsedQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new ParsedQuery(new Dictionary<QueryField, HashSet<string>>());
            }

            var grouping = _parser.ParseQuery(query.Trim(), skipWhiteSpace: true);

            return new ParsedQuery(grouping);
        }

        public string Build(ParsedQuery parsed)
        {
            if (!parsed.Grouping.Any())
            {
                return MatchAllDocumentsQuery;
            }

            var scopedTerms = parsed.Grouping.Where(g => g.Key != QueryField.Any && g.Key != QueryField.Invalid).ToList();
            var unscopedTerms = parsed.Grouping.Where(g => g.Key == QueryField.Any)
                .Select(g => g.Value)
                .SingleOrDefault()?
                .ToList();

            // Don't bother generating Azure Search text if all terms are scoped to invalid fields.
            if (scopedTerms.Count == 0 && (unscopedTerms == null || unscopedTerms.Count == 0))
            {
                return MatchAllDocumentsQuery;
            }

            // Add the terms that are scoped to specific fields.
            var builder = new AzureSearchTextBuilder();
            var requireScopedTerms = unscopedTerms != null || scopedTerms.Count > 1;

            foreach (var scopedTerm in scopedTerms)
            {
                var fieldName = FieldNames[scopedTerm.Key];
                var values = ProcessFieldValues(scopedTerm.Key, scopedTerm.Value).ToList();

                if (values.Count > 1)
                {
                    builder.AppendScopedTerms(fieldName, values, required: requireScopedTerms);
                }
                else
                {
                    builder.AppendScopedTerm(fieldName, values.First(), required: requireScopedTerms);
                }
            }

            // Add the terms that can match any fields.
            if (unscopedTerms != null)
            {
                builder.AppendTerms(unscopedTerms);

                if (unscopedTerms.Count > 1)
                {
                    builder.AppendBoostIfMatchAllTerms(unscopedTerms, boost: 2.0f);
                }
            }

            return builder.ToString();
        }

        private static IEnumerable<string> ProcessFieldValues(QueryField field, IEnumerable<string> values)
        {
            switch (field)
            {
                // Expand tags by their delimiters
                case QueryField.Tag:
                    return values.SelectMany(Utils.SplitTags).Distinct();

                // The "version" query field should be normalized if possible.
                case QueryField.Version:
                    return values.Select(value =>
                    {
                        if (!NuGetVersion.TryParse(value, out var version))
                        {
                            return value;
                        }

                        return version.ToNormalizedString();
                    });

                default:
                    return values;
            }
        }
    }
}
