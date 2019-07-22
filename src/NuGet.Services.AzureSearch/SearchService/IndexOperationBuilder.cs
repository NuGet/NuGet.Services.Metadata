// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Indexing;
using NuGet.Packaging;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class IndexOperationBuilder : IIndexOperationBuilder
    {
        private readonly ISearchTextBuilder _textBuilder;
        private readonly ISearchParametersBuilder _parametersBuilder;

        public IndexOperationBuilder(
            ISearchTextBuilder textBuilder,
            ISearchParametersBuilder parametersBuilder)
        {
            _textBuilder = textBuilder ?? throw new ArgumentNullException(nameof(textBuilder));
            _parametersBuilder = parametersBuilder ?? throw new ArgumentNullException(nameof(parametersBuilder));
        }

        public IndexOperation V3Search(V3SearchRequest request)
        {
            var parsed = _textBuilder.ParseV3Search(request);

            IndexOperation indexOperation;
            if (TryGetSearchDocumentByKey(request, parsed, out indexOperation))
            {
                return indexOperation;
            }

            var text = _textBuilder.Build(parsed);
            var parameters = _parametersBuilder.V3Search(request);
            return IndexOperation.Search(text, parameters);
        }

        public IndexOperation V2SearchWithSearchIndex(V2SearchRequest request)
        {
            var parsed = _textBuilder.ParseV2Search(request);

            IndexOperation indexOperation;
            if (TryGetSearchDocumentByKey(request, parsed, out indexOperation))
            {
                return indexOperation;
            }

            var text = _textBuilder.Build(parsed);
            var parameters = _parametersBuilder.V2SearchWithSearchIndex(request);
            return IndexOperation.Search(text, parameters);
        }

        public IndexOperation V2SearchWithHijackIndex(V2SearchRequest request)
        {
            var parsed = _textBuilder.ParseV2Search(request);

            IndexOperation indexOperation;
            string packageIdFilter = null;
            if (TryGetSinglePackageId(parsed, out var packageId))
            {
                if (TryGetHijackDocumentByKey(request, parsed, packageId, out indexOperation))
                {
                    return indexOperation;
                }
                
                if (parsed.Grouping.Count == 1)
                {
                    // Only filter by package ID if it's the only thing in the query. This is the most common case
                    // because of the hijack from gallery for the OData FindPackagesById function. Also, if the only
                    // thing in the query is a package ID, clear the grouping so that we generate empty search text.
                    packageIdFilter = packageId;
                    parsed.Grouping.Remove(QueryField.PackageId);
                }
            }

            var text = _textBuilder.Build(parsed);
            var parameters = _parametersBuilder.V2SearchWithHijackIndex(request, packageIdFilter);
            return IndexOperation.Search(text, parameters);
        }

        public IndexOperation Autocomplete(AutocompleteRequest request)
        {
            var text = _textBuilder.Autocomplete(request);
            var parameters = _parametersBuilder.Autocomplete(request);
            return IndexOperation.Search(text, parameters);
        }

        private bool TryGetSearchDocumentByKey(
            SearchRequest request,
            ParsedQuery parsed,
            out IndexOperation indexOperation)
        {
            if (PagedToFirstItem(request)
                && parsed.Grouping.Count == 1
                && TryGetSinglePackageId(parsed, out var packageId))
            {
                var searchFilters = _parametersBuilder.GetSearchFilters(request);
                var documentKey = DocumentUtilities.GetSearchDocumentKey(packageId, searchFilters);

                indexOperation = IndexOperation.Get(documentKey);
                return true;
            }

            indexOperation = null;
            return false;
        }

        private bool TryGetHijackDocumentByKey(
            SearchRequest request,
            ParsedQuery parsed,
            string packageId,
            out IndexOperation indexOperation)
        {
            if (PagedToFirstItem(request)
                && parsed.Grouping.Count == 2
                && TryGetSingleVersion(parsed, out var normalizedVersion))
            {
                var documentKey = DocumentUtilities.GetHijackDocumentKey(packageId, normalizedVersion);

                indexOperation = IndexOperation.Get(documentKey);
                return true;
            }

            indexOperation = null;
            return false;
        }

        private bool TryGetSinglePackageId(
            ParsedQuery parsed,
            out string packageId)
        {
            if (parsed.Grouping.TryGetValue(QueryField.PackageId, out var terms)
                && terms.Count == 1)
            {
                packageId = terms.First();
                if (packageId.Length <= PackageIdValidator.MaxPackageIdLength
                    && PackageIdValidator.IsValidPackageId(packageId))
                {
                    return true;
                }
            }

            packageId = null;
            return false;
        }

        private bool TryGetSingleVersion(
            ParsedQuery parsed,
            out string normalizedVersion)
        {
            if (parsed.Grouping.TryGetValue(QueryField.Version, out var terms)
                && terms.Count == 1)
            {
                if (NuGetVersion.TryParse(terms.First(), out var parsedVersion))
                {
                    normalizedVersion = parsedVersion.ToNormalizedString();
                    return true;
                }
            }

            normalizedVersion = null;
            return false;
        }

        private static bool PagedToFirstItem(SearchRequest request)
        {
            return request.Skip <= 0 && request.Take >= 1;
        }
    }
}
