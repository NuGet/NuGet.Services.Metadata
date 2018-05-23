using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Search;
using Newtonsoft.Json;
using NuGet.Indexing;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch.Controllers
{
    public class V3SearchController : Controller
    {
        private readonly SearchIndexClient _searchClient;
        private readonly AuxiliaryManager _auxiliary;
        private readonly UrlBuilder _url;

        public V3SearchController(SearchIndexClient searchClient, AuxiliaryManager auxiliary, UrlBuilder urlBuilder)
        {
            _searchClient = searchClient ?? throw new ArgumentNullException(nameof(searchClient));
            _auxiliary = auxiliary ?? throw new ArgumentNullException(nameof(auxiliary));
            _url = urlBuilder ?? throw new ArgumentNullException(nameof(urlBuilder));
        }

        public async Task<object> Get([FromQuery(Name = "q")] string query = null)
        {
            query = query ?? string.Empty;

            var results = await SearchAsync(query);

            return new
            {
                TotalHits = results.Count,
                Data = results.Select(p => new SearchResultModel(p, _url))
            };
        }

        public async Task<IActionResult> Autocomplete([FromQuery(Name = "q")] string query = null)
        {
            /*
            var results = await _searchService.AutocompleteAsync(query);

            return Json(new
            {
                TotalHits = results.Count,
                Data = results,
            });*/
            await Task.Yield();

            return Json(new { });
        }

        private async Task<IReadOnlyList<SearchResult>> SearchAsync(string query)
        {
            var search = await _searchClient.Documents.SearchAsync<PackageDocument>(query);
            var packages = search.Results
                .Select(r => r.Document)
                .GroupBy(p => p.Id)
                .ToList();

            var result = new List<SearchResult>();

            // TODO: Don't parse NuGetVersion three times
            foreach (var package in packages)
            {
                var versions = package.OrderByDescending(p => NuGetVersion.Parse(p.Version)).ToList();
                var latest = versions.First();

                var versionResults = versions.Select(p => new SearchResultVersion(NuGetVersion.Parse(p.Version), p.Downloads));

                result.Add(new SearchResult
                {
                    Id = latest.Id,
                    Version = NuGetVersion.Parse(latest.Version),
                    Description = latest.Description,
                    Authors = string.Join(", ", latest.Authors),
                    IconUrl = latest.IconUrl,
                    LicenseUrl = latest.LicenseUrl,
                    ProjectUrl = latest.ProjectUrl,
                    Summary = latest.Summary,
                    Tags = latest.Tags,
                    Title = latest.Title,
                    TotalDownloads = package.Sum(p => p.Downloads),
                    Verified = latest.Verified,
                    Versions = versionResults.ToList().AsReadOnly(),
                });
            }

            return result.AsReadOnly();
        }

        private async Task<IReadOnlyList<SearchResult>> SearchWithAuxiliaryAsync(string query)
        {
            var search = await _searchClient.Documents.SearchAsync<PackageDocument>(query);
            var packages = search.Results
                .Select(r => r.Document)
                .GroupBy(p => p.Id)
                .ToList();

            var result = new List<SearchResult>();

            // TODO: Don't parse NuGetVersion three times
            foreach (var package in packages)
            {
                var data = _auxiliary[package.Key];

                var versions = package.OrderByDescending(p => NuGetVersion.Parse(p.Version)).ToList();
                var latest = versions.First();

                var versionResults = versions.Select(p => new SearchResultVersion(NuGetVersion.Parse(p.Version), data.Downloads[p.Version]));

                result.Add(new SearchResult
                {
                    Id = latest.Id,
                    Version = NuGetVersion.Parse(latest.Version),
                    Description = latest.Description,
                    Authors = string.Join(", ", latest.Authors),
                    IconUrl = latest.IconUrl,
                    LicenseUrl = latest.LicenseUrl,
                    ProjectUrl = latest.ProjectUrl,
                    Summary = latest.Summary,
                    Tags = latest.Tags,
                    Title = latest.Title,
                    TotalDownloads = data.Downloads.Total,
                    Verified = data.Verified,
                    Versions = versionResults.ToList().AsReadOnly(),
                });
            }

            return result.AsReadOnly();
        }

        private class SearchResult
        {
            public string Id { get; set; }

            public NuGetVersion Version { get; set; }

            public string Description { get; set; }
            public string Authors { get; set; }
            public string IconUrl { get; set; }
            public string LicenseUrl { get; set; }
            public string ProjectUrl { get; set; }
            public string Summary { get; set; }
            public string[] Tags { get; set; }
            public string Title { get; set; }
            public long TotalDownloads { get; set; }
            public bool Verified { get; set; }

            public IReadOnlyList<SearchResultVersion> Versions { get; set; }
        }

        private class SearchResultVersion
        {
            public SearchResultVersion(NuGetVersion version, long downloads)
            {
                Version = version ?? throw new ArgumentNullException(nameof(version));
                Downloads = downloads;
            }

            public NuGetVersion Version { get; }

            public long Downloads { get; }
        }

        private class SearchResultModel
        {
            private readonly SearchResult _result;
            private readonly UrlBuilder _url;

            public SearchResultModel(SearchResult result, UrlBuilder url)
            {
                _result = result ?? throw new ArgumentNullException(nameof(result));
                _url = url ?? throw new ArgumentNullException(nameof(url));

                var versions = result.Versions.Select(
                    v => new SearchResultVersionModel(
                        url.Registration(result.Id, v.Version),
                        v.Version.ToNormalizedString(),
                        v.Downloads));

                Versions = versions.ToList().AsReadOnly();
            }

            public string Id => _result.Id;
            public string Version => _result.Version.ToNormalizedString();
            public string Description => _result.Description;
            public string Authors => _result.Authors;
            public string IconUrl => _result.IconUrl;
            public string LicenseUrl => _result.LicenseUrl;
            public string ProjectUrl => _result.ProjectUrl;
            public string Registration => _url.Registration(_result.Id);
            public string Summary => _result.Summary;
            public string[] Tags => _result.Tags;
            public string Title => _result.Title;
            public long TotalDownloads => _result.TotalDownloads;

            public IReadOnlyList<SearchResultVersionModel> Versions { get; }
        }

        private class SearchResultVersionModel
        {
            public SearchResultVersionModel(string registrationUrl, string version, long downloads)
            {
                if (string.IsNullOrEmpty(registrationUrl)) throw new ArgumentNullException(nameof(registrationUrl));
                if (string.IsNullOrEmpty(version)) throw new ArgumentNullException(nameof(version));

                RegistrationUrl = registrationUrl;
                Version = version;
                Downloads = downloads;
            }

            [JsonProperty(PropertyName = "id")]
            public string RegistrationUrl { get; }

            public string Version { get; }

            public long Downloads { get; }
        }
    }
}
