﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Search.Models;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.AzureSearch.Owners2AzureSearch
{
    public class OwnerIndexActionBuilder : IOwnerIndexActionBuilder
    {
        private static readonly IReadOnlyList<SearchFilters> AllSearchFilters = Enum
            .GetValues(typeof(SearchFilters))
            .Cast<SearchFilters>()
            .ToList();

        private readonly IVersionListDataClient _versionListDataClient;
        private readonly ISearchDocumentBuilder _search;
        private readonly ILogger<OwnerIndexActionBuilder> _logger;

        public OwnerIndexActionBuilder(
            IVersionListDataClient versionListDataClient,
            ISearchDocumentBuilder search,
            ILogger<OwnerIndexActionBuilder> logger)
        {
            _versionListDataClient = versionListDataClient ?? throw new ArgumentNullException(nameof(versionListDataClient));
            _search = search ?? throw new ArgumentNullException(nameof(search));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IndexActions> UpdateOwnersAsync(string packageId, string[] owners)
        {
            var versionListDataResult = await _versionListDataClient.ReadAsync(packageId);
            var versionLists = new VersionLists(versionListDataResult.Result);

            /// Update all of the search documents that exist for this package ID with the provided owners. Note that
            /// this owner list can be empty (e.g. if the last owner was deleted). Here are some examples of different
            /// search filter combinations that could occur.
            ///
            /// Example #1: 1.0.0 (listed)
            ///
            ///   A stable SemVer 1.0.0 package matches all search filters, so one index action will be produced for
            ///   each search document. That is four in total. All owner fields will be set to the same thing. All of
            ///   these search documents have the same latest version: 1.0.0.
            ///
            /// Example #2: 1.0.0 (unlisted), 2.0.0 (unlisted)
            ///
            ///   There are no search documents at all in this case since there is no listed version. No index actions
            ///   are produced in this case.
            ///
            /// Example #3: 1.0.0-beta (listed), 2.0.0-beta.1 (listed)
            ///
            ///   All of the versions are prerelease so there are no search documents for "stable" search filters. There
            ///   two search documents to be updated, one for <see cref="SearchFilters.IncludePrerelease"/> and one for
            ///   <see cref="SearchFilters.IncludePrereleaseAndSemVer2"/>. The latest version for each of these two
            ///   documents is different, but both have the same update with regards to the owners field.
            var search = new List<IndexAction<KeyedDocument>>();
            var searchFilters = new List<SearchFilters>();
            foreach (var searchFilter in AllSearchFilters)
            {
                // Determine if there is a document for this ID and search filter.
                if (versionLists.GetLatestVersionInfoOrNull(searchFilter) == null)
                {
                    continue;
                }

                var document = _search.UpdateOwners(packageId, searchFilter, owners);
                var indexAction = IndexAction.Merge<KeyedDocument>(document);
                search.Add(indexAction);
                searchFilters.Add(searchFilter);
            }

            _logger.LogInformation(
                "Package ID {PackageId} has {Count} search document changes for search filters: {SearchFilters}",
                packageId,
                searchFilters.Count,
                searchFilters);

            // No changes are made to the hijack index.
            var hijack = new List<IndexAction<KeyedDocument>>();

            // We never make any change to the version list but still want to push it back to storage. This will give
            // us an etag mismatch if the version list has changed. This is good because if the version list has
            // changed it's possible there is another search document that we have to update. If we didn't do this,
            // then a race condition could occur where one of the search documents for an ID would have the old owners
            // until the ownership changes again.
            var newVersionListDataResult = versionListDataResult;

            return new IndexActions(
                search,
                hijack,
                newVersionListDataResult);
        }
    }
}