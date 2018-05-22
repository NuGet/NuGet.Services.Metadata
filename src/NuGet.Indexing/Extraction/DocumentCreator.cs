// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text;
using Lucene.Net.Index;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Newtonsoft.Json;
using NuGet.Versioning;

namespace NuGet.Indexing
{
    public static class DocumentCreator
    {
        const int MergeFactor = 10;        //  Define the size of a file in a level (exponentially) and the count of files that constitue a level
        const int MaxMergeDocs = 7999;     //  Except never merge segments that have more docs than this

        public static IndexWriter CreateIndexWriter(Lucene.Net.Store.Directory directory, bool create)
        {
            IndexWriter indexWriter = new IndexWriter(directory, new PackageAnalyzer(), create, IndexWriter.MaxFieldLength.UNLIMITED);
            indexWriter.MergeFactor = MergeFactor;
            indexWriter.MaxMergeDocs = MaxMergeDocs;

            indexWriter.SetSimilarity(new CustomSimilarity());
            return indexWriter;
        }

        public static LuceneCommitMetadata CreateCommitMetadata(DateTime commitTimeStamp, string description, int count, string trace)
        {
            return new LuceneCommitMetadata(commitTimeStamp, description, count, trace);
        }

        public static PackageDocument CreateDocument(IDictionary<string, string> package)
        {
            var errors = new List<string>();
            var document = new PackageDocument();

            // add fields used by search queries
            AddId(document, package, errors);
            AddVersion(document, package, errors);
            AddTitle(document, package);

            document.Key = BuildPackageKey(document.Id, document.Version);
            document.Description = GetStringField(package, MetadataConstants.DescriptionPropertyName);
            document.Summary = GetStringField(package, MetadataConstants.SummaryPropertyName);

            AddTags(document, package);
            AddAuthors(document, package);

            // add fields used by filtering and sorting
            document.SemVerLevel = GetStringField(package, MetadataConstants.SemVerLevelKeyPropertyName);
            AddListed(document, package, errors);
            AddDates(document, package, errors);
            AddSortableTitle(document, package);
            AddDownloads(document, package, errors);

            // add fields used when materializing the result
            document.IconUrl = GetStringField(package, MetadataConstants.IconUrlPropertyName);
            document.ProjectUrl = GetStringField(package, MetadataConstants.ProjectUrlPropertyName);
            document.MinClientVersion = GetStringField(package, MetadataConstants.MinClientVersionPropertyName);
            document.ReleaseNotes = GetStringField(package, MetadataConstants.ReleaseNotesPropertyName);
            document.Copyright = GetStringField(package, MetadataConstants.CopyrightPropertyName);
            document.Language = GetStringField(package, MetadataConstants.LanguagePropertyName);
            document.LicenseUrl = GetStringField(package, MetadataConstants.LicenseUrlPropertyName);
            document.PackageHash = GetStringField(package, MetadataConstants.PackageHashPropertyName);
            document.PackageHashAlgorithm = GetStringField(package, MetadataConstants.PackageHashAlgorithmPropertyName);

            AddPackageSize(document, package, errors);
            AddRequiresLicenseAcceptance(document, package, errors);
            AddDependencies(document, package);
            AddSupportedFrameworks(document, package);

            CheckErrors(errors);

            return document;
        }

        public static string BuildPackageKey(string packageId, string normalizedVersion)
        {
            var keyString = $"{packageId.ToLowerInvariant()}:{normalizedVersion.ToLowerInvariant()}";

            // Keys can only contain letters, digits, underscore(_), dash(-), or equal sign(=).
            var bytes = Encoding.UTF8.GetBytes(keyString);
            var base64 = Convert.ToBase64String(bytes);

            return base64.Replace('+', '-').Replace('/', '_');
        }

        private static void AddId(PackageDocument document, IDictionary<string, string> package, List<string> errors)
        {
            if (package.TryGetValue(MetadataConstants.IdPropertyName, out string value))
            {
                document.Id = value;
            }
            else
            {
                errors.Add($"Required property '{MetadataConstants.IdPropertyName}' not found.");
            }
        }

        private static void AddVersion(PackageDocument document, IDictionary<string, string> package, List<string> errors)
        {
            if (package.TryGetValue(MetadataConstants.VerbatimVersionPropertyName, out string verbatimVersion))
            {
                document.VerbatimVersion = verbatimVersion;

                if (NuGetVersion.TryParse(verbatimVersion, out NuGetVersion parsedVerbatimVersion))
                {
                    document.Version = parsedVerbatimVersion.ToNormalizedString();
                    document.FullVersion = parsedVerbatimVersion.ToFullString();
                }
                else
                {
                    errors.Add($"Unable to parse '{MetadataConstants.VerbatimVersionPropertyName}' as NuGetVersion.");
                }
            }
            else
            {
                errors.Add($"Required property '{MetadataConstants.VerbatimVersionPropertyName}' not found.");
            }
        }

        private static void AddTitle(PackageDocument document, IDictionary<string, string> package)
        {
            package.TryGetValue(MetadataConstants.TitlePropertyName, out string value);

            if (string.IsNullOrEmpty(value))
            {
                package.TryGetValue(MetadataConstants.IdPropertyName, out value);
            }

            document.Title = value ?? string.Empty;
        }

        private static void AddTags(PackageDocument document, IDictionary<string, string> package)
        {
            package.TryGetValue(MetadataConstants.TagsPropertyName, out string tagsString);

            if (string.IsNullOrEmpty(tagsString))
            {
                document.Tags = new string[0];
            }
            else
            {
                document.Tags = tagsString.Split(new[] { ',', ';', ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        private static void AddAuthors(PackageDocument document, IDictionary<string, string> package)
        {
            package.TryGetValue(MetadataConstants.AuthorsPropertyName, out string authorsString);

            if (string.IsNullOrEmpty(authorsString))
            {
                document.Authors = new string[0];
            }
            else
            {
                document.Authors = authorsString.Split(new[] { ',', ';', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        private static void AddListed(PackageDocument document, IDictionary<string, string> package, List<string> errors)
        {
            if (package.TryGetValue(MetadataConstants.ListedPropertyName, out string value))
            {
                if (bool.TryParse(value, out bool listed))
                {
                    document.Listed = listed;
                }
                else
                {
                    errors.Add($"Unable to parse '{MetadataConstants.ListedPropertyName}' as Boolean.");
                }
            }
            else
            {
                errors.Add($"Required property '{MetadataConstants.ListedPropertyName}' not found.");
            }
        }

        private static void AddSortableTitle(PackageDocument document, IDictionary<string, string> package)
        {
            package.TryGetValue(MetadataConstants.TitlePropertyName, out string value);

            if (string.IsNullOrEmpty(value))
            {
                package.TryGetValue(MetadataConstants.IdPropertyName, out value);
            }

            document.Title = (value ?? string.Empty).Trim().ToLower();
        }

        private static void AddDownloads(PackageDocument document, IDictionary<string, string> package, List<string> errors)
        {
            if (package.TryGetValue(MetadataConstants.DownloadCountPropertyName, out string value))
            {
                if (int.TryParse(value, out int downloads))
                {
                    document.Downloads = downloads;
                }
                else
                {
                    errors.Add($"Unable to parse '{MetadataConstants.DownloadCountPropertyName}' as Int32.");
                }
            }
        }

        private static void AddDates(PackageDocument document, IDictionary<string, string> package, List<string> errors)
        {
            if (package.TryGetValue(MetadataConstants.CreatedPropertyName, out string created))
            {
                if (DateTimeOffset.TryParse(created, out DateTimeOffset createdDateTime))
                {
                    document.Created = createdDateTime;
                }
            }

            if (package.TryGetValue(MetadataConstants.PublishedPropertyName, out string published))
            {
                if (DateTimeOffset.TryParse(published, out DateTimeOffset publishedDateTime))
                {
                    document.Published = publishedDateTime;
                }
                else
                {
                    errors.Add($"Unable to parse '{MetadataConstants.PublishedPropertyName}' as DateTime.");
                }

                string lastEdited;
                if (!package.TryGetValue(MetadataConstants.LastEditedPropertyName, out lastEdited) || lastEdited == MetadataConstants.DateTimeZeroStringValue)
                {
                    lastEdited = publishedDateTime.ToString("O");
                }

                if (DateTimeOffset.TryParse(lastEdited, out DateTimeOffset lastEditedDateTime))
                {
                    document.LastEdited = lastEditedDateTime;
                }
                else
                {
                    errors.Add($"Unable to parse '{MetadataConstants.LastEditedPropertyName}' as DateTime.");
                }
            }
            else
            {
                errors.Add($"Required property '{MetadataConstants.PublishedPropertyName}' not found.");
            }
        }

        private static void AddPackageSize(PackageDocument document, IDictionary<string, string> package, List<string> errors)
        {
            if (package.TryGetValue(MetadataConstants.PackageSizePropertyName, out string value))
            {
                if (int.TryParse(value, out int packageSize))
                {
                    document.PackageSize = packageSize;
                }
                else
                {
                    errors.Add($"Unable to parse '{MetadataConstants.PackageSizePropertyName}' as Int32.");
                }
            }
        }

        private static void AddRequiresLicenseAcceptance(PackageDocument document, IDictionary<string, string> package, List<string> errors)
        {
            if (package.TryGetValue(MetadataConstants.RequiresLicenseAcceptancePropertyName, out string value))
            {
                if (bool.TryParse(value, out bool requiresLicenseAcceptance))
                {
                    document.RequiresLicenseAcceptance = requiresLicenseAcceptance;
                }
                else
                {
                    errors.Add($"Unable to parse '{MetadataConstants.RequiresLicenseAcceptancePropertyName}' as Boolean.");
                }
            }
        }

        private static void AddDependencies(PackageDocument document, IDictionary<string, string> package)
        {
            if (package.TryGetValue(MetadataConstants.FlattenedDependenciesPropertyName, out string value))
            {
                document.FlattenedDependencies = value;

                if (!string.IsNullOrWhiteSpace(value))
                {
                    using (var textWriter = new StringWriter())
                    {
                        using (var jsonWriter = new JsonTextWriter(textWriter))
                        {
                            jsonWriter.WriteStartArray();

                            foreach (var dependency in value.Split('|'))
                            {
                                string[] fields = dependency.Split(':');
                                if (fields.Length > 0)
                                {
                                    jsonWriter.WriteStartObject();
                                    jsonWriter.WritePropertyName("Id");
                                    jsonWriter.WriteValue(fields[0]);
                                    if (fields.Length > 1)
                                    {
                                        jsonWriter.WritePropertyName("VersionSpec");
                                        jsonWriter.WriteValue(fields[1]);
                                    }
                                    if (fields.Length > 2)
                                    {
                                        jsonWriter.WritePropertyName("TargetFramework");
                                        jsonWriter.WriteValue(fields[2]);
                                    }
                                    jsonWriter.WriteEndObject();
                                }
                            }
                            jsonWriter.WriteEndArray();
                            jsonWriter.Flush();
                            textWriter.Flush();
                            string dependencies = textWriter.ToString();

                            document.Dependencies = dependencies;
                        }
                    }
                }
            }
        }

        private static void AddSupportedFrameworks(PackageDocument document, IDictionary<string, string> package)
        {
            if (package.TryGetValue(MetadataConstants.SupportedFrameworksPropertyName, out string value))
            {
                document.SupportedFrameworks = value.Split('|');
            }
            else
            {
                document.SupportedFrameworks = new string[0];
            }
        }

        private static string GetStringField(IDictionary<string, string> package, string field)
        {
            if (!package.TryGetValue(field, out string value))
            {
                return string.Empty;
            }

            return value;
        }

        private static void CheckErrors(List<string> errors)
        {
            if (errors.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (string error in errors)
                {
                    sb.AppendLine(error);
                }
                throw new Exception(sb.ToString());
            }
        }
    }

    public class AzureSearchIndexWriter : IDisposable
    {
        public const int MaxBatchSize = 1000;

        private readonly ISearchIndexClient _indexClient;
        private List<IndexAction<PackageDocument>> _actions;

        public AzureSearchIndexWriter(
            ISearchIndexClient indexClient //,
            /*ILogger<BatchIndexer> logger*/)
        {
            _indexClient = indexClient ?? throw new ArgumentNullException(nameof(indexClient));
            _actions = null;
        }

        // This is not thread-safe
        public void AddDocument(PackageDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            if (_actions == null)
            {
                ResetActions();
            }

            _actions.Add(IndexAction.Upload(document));
        }

        public void DeleteDocument(string packageId, string version)
        {
            if (string.IsNullOrEmpty(nameof(packageId))) throw new ArgumentNullException(nameof(packageId));
            if (string.IsNullOrEmpty(nameof(version))) throw new ArgumentNullException(nameof(version));

            if (_actions == null)
            {
                ResetActions();
            }

            _actions.Add(IndexAction.Delete(new PackageDocument
            {
                Key = DocumentCreator.BuildPackageKey(packageId, version)
            }));
        }

        public void Commit()
        {
            if (_actions == null || _actions.Count == 0) return;

            var batch = IndexBatch.New(_actions);

            // TODO: Use IndexAsync, requires API change for IndexWriter.
            _indexClient.Documents.Index(batch);

            ResetActions();
        }

        public void Dispose() => _indexClient?.Dispose();

        private void ResetActions()
        {
            _actions = new List<IndexAction<PackageDocument>>();
        }
    }

    // See: https://docs.microsoft.com/en-us/nuget/api/search-query-service-resource#search-for-packages
    [SerializePropertyNamesAsCamelCase]
    public class PackageDocument
    {
        public const string IndexName = "packages";

        [Key]
        public string Key { get; set; }

        [IsSearchable, IsFilterable, IsSortable]
        public string Id { get; set; }

        [IsSearchable, IsFilterable, IsSortable]
        public string Version { get; set; }

        [IsSearchable, IsFilterable, IsSortable]
        public string VerbatimVersion { get; set; }

        [IsSearchable, IsFilterable, IsSortable]
        public string FullVersion { get; set; }

        [IsSearchable]
        public string Description { get; set; }
        public string[] Authors { get; set; }
        public string IconUrl { get; set; }
        public string LicenseUrl { get; set; }
        [IsFilterable]
        public bool Listed { get; set; }
        public string ProjectUrl { get; set; }

        public DateTimeOffset Created { get; set; }
        public DateTimeOffset Published { get; set; }
        public DateTimeOffset LastEdited { get; set; }

        public int PackageSize { get; set; }
        public bool RequiresLicenseAcceptance { get; set; }
        public string FlattenedDependencies { get; set; }
        public string Dependencies { get; set; }

        [IsSearchable, IsFilterable, IsFacetable]
        public string[] SupportedFrameworks { get; set; }
        public string SemVerLevel { get; set; }
        public string MinClientVersion { get; set; }
        public string ReleaseNotes { get; set; }
        public string Copyright { get; set; }
        public string Language { get; set; }
        public string PackageHash { get; set; }
        public string PackageHashAlgorithm { get; set; }

        [IsSearchable]
        public string Summary { get; set; }

        [IsSearchable, IsFilterable, IsFacetable]
        public string[] Tags { get; set; }

        [IsSearchable]
        public string Title { get; set; }

        [IsFilterable, IsSortable]
        public long Downloads { get; set; }
    }
}
