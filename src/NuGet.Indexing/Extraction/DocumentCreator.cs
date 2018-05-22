// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Newtonsoft.Json;
using NuGet.Versioning;
using LuceneConstants = NuGet.Indexing.MetadataConstants.LuceneMetadata;

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
            public string SupportedFrameworks { get; set; }

            [IsSearchable]
            public string Summary { get; set; }

            [IsSearchable, IsFilterable, IsFacetable]
            public string[] Tags { get; set; }

            [IsSearchable]
            public string Title { get; set; }

            [IsFilterable, IsSortable]
            public long TotalDownloads { get; set; }

            [IsFilterable, IsSortable]
            public int DownloadsMagnitude { get; set; }

            public string[] Versions { get; set; }
            public string[] VersionDownloads { get; set; }
        }

        public static PackageDocument CreateDocument(IDictionary<string, string> package)
        {
            var errors = new List<string>();
            var document = new PackageDocument();

            // add fields used by search queries
            AddId(document, package, errors);
            AddVersion(document, package, errors);
            AddTitle(document, package);
            document.Description = GetStringField(package, MetadataConstants.DescriptionPropertyName);
            document.Summary = GetStringField(package, MetadataConstants.SummaryPropertyName);

            //AddField(document, LuceneConstants.TagsPropertyName, package, MetadataConstants.TagsPropertyName, Field.Index.ANALYZED, 2.0f);
            //AddField(document, LuceneConstants.AuthorsPropertyName, package, MetadataConstants.AuthorsPropertyName, Field.Index.ANALYZED);

            // add fields used by filtering and sorting
            //AddField(document, LuceneConstants.SemVerLevelPropertyName, package, MetadataConstants.SemVerLevelKeyPropertyName, Field.Index.ANALYZED);
            AddListed(document, package, errors);
            AddDates(document, package, errors);
            AddSortableTitle(document, package);

            // add fields used when materializing the result
            //AddField(document, LuceneConstants.IconUrlPropertyName, package, MetadataConstants.IconUrlPropertyName, Field.Index.NOT_ANALYZED);
            //AddField(document, LuceneConstants.ProjectUrlPropertyName, package, MetadataConstants.ProjectUrlPropertyName, Field.Index.NOT_ANALYZED);
            //AddField(document, LuceneConstants.MinClientVersionPropertyName, package, MetadataConstants.MinClientVersionPropertyName, Field.Index.NOT_ANALYZED);
            //AddField(document, LuceneConstants.ReleaseNotesPropertyName, package, MetadataConstants.ReleaseNotesPropertyName, Field.Index.NOT_ANALYZED);
            //AddField(document, LuceneConstants.CopyrightPropertyName, package, MetadataConstants.CopyrightPropertyName, Field.Index.NOT_ANALYZED);
            //AddField(document, LuceneConstants.LanguagePropertyName, package, MetadataConstants.LanguagePropertyName, Field.Index.NOT_ANALYZED);
            //AddField(document, LuceneConstants.LicenseUrlPropertyName, package, MetadataConstants.LicenseUrlPropertyName, Field.Index.NOT_ANALYZED);
            //AddField(document, LuceneConstants.PackageHashPropertyName, package, MetadataConstants.PackageHashPropertyName, Field.Index.NOT_ANALYZED);
            //AddField(document, LuceneConstants.PackageHashAlgorithmPropertyName, package, MetadataConstants.PackageHashAlgorithmPropertyName, Field.Index.NOT_ANALYZED);
            AddPackageSize(document, package, errors);
            AddRequiresLicenseAcceptance(document, package, errors);
            AddDependencies(document, package);
            AddSupportedFrameworks(document, package);

            CheckErrors(errors);

            return document;
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
            string value;
            if (package.TryGetValue(MetadataConstants.SupportedFrameworksPropertyName, out value))
            {
                using (var textWriter = new StringWriter())
                {
                    using (var jsonWriter = new JsonTextWriter(textWriter))
                    {
                        jsonWriter.WriteStartArray();
                        foreach (var s in value.Split('|'))
                        {
                            jsonWriter.WriteValue(s);
                        }
                        jsonWriter.WriteEndArray();
                        jsonWriter.Flush();
                        textWriter.Flush();
                        string supportedFrameworks = textWriter.ToString();

                        document.SupportedFrameworks = supportedFrameworks;
                    }
                }
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
}
