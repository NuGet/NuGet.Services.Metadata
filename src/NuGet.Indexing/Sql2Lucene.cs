// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Search;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace NuGet.Indexing
{
    public class Sql2Lucene
    {
        static PackageDocument CreateDocument(SqlDataReader reader, IDictionary<int, List<string>> packageFrameworks)
        {
            var package = new Dictionary<string, string>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                if (!reader.IsDBNull(i))
                {
                    string name = reader.GetName(i);
                    object obj = reader.GetValue(i);

                    if (name == "key")
                    {
                        var key = (int)obj;
                        List<string> targetFrameworks;
                        if (packageFrameworks.TryGetValue(key, out targetFrameworks))
                        {
                            package.Add("supportedFrameworks", string.Join("|", targetFrameworks));
                        }
                    }

                    var value = (obj is DateTime) ? ((DateTime)obj).ToUniversalTime().ToString("O") : obj.ToString();

                    package.Add(name, value);
                }
            }

            return DocumentCreator.CreateDocument(package);
        }

        static void IndexBatch(
            string connectionString,
            ISearchServiceClient searchClient,
            string indexName,
            IDictionary<int, List<string>> packageFrameworks,
            int beginKey,
            int endKey)
        {
            // Build the documents for this batch.
            var documents = new List<PackageDocument>();

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                var cmdText = @"
                    SELECT
                        Packages.[Key]                          'key',
                        PackageRegistrations.Id                 'id',
                        Packages.[Version]                      'verbatimVersion',
                        Packages.NormalizedVersion              'version',
                        Packages.Title                          'title',
                        Packages.Tags                           'tags',
                        Packages.[Description]                  'description',
                        Packages.DownloadCount                  'downloadCount',
                        Packages.FlattenedAuthors               'authors',
                        Packages.Summary                        'summary',
                        Packages.IconUrl                        'iconUrl',
                        Packages.ProjectUrl                     'projectUrl',
                        Packages.MinClientVersion               'minClientVersion',
                        Packages.ReleaseNotes                   'releaseNotes',
                        Packages.Copyright                      'copyright',
                        Packages.[Language]                     'language',
                        Packages.LicenseUrl                     'licenseUrl',
                        Packages.RequiresLicenseAcceptance      'requireLicenseAcceptance',
                        Packages.[Hash]                         'packageHash',
                        Packages.HashAlgorithm                  'packageHashAlgorithm',
                        Packages.PackageFileSize                'packageSize',
                        Packages.FlattenedDependencies          'flattenedDependencies',
                        PackageRegistrations.DownloadCount      'totalDownloadCount',
                        PackageRegistrations.IsVerified         'isVerified',
                        Packages.Created                        'created',
                        Packages.LastEdited                     'lastEdited',
                        Packages.Published                      'published',
                        Packages.Listed                         'listed',
                        Packages.SemVerLevelKey                 'semVerLevelKey'
                    FROM Packages
                    INNER JOIN PackageRegistrations ON Packages.PackageRegistrationKey = PackageRegistrations.[Key]
                      AND PackageRegistrations.[Key] >= @BeginKey
                      AND PackageRegistrations.[Key] < @EndKey
                    WHERE Packages.PackageStatusKey = 0
                    ORDER BY PackageRegistrations.[Key]";

                using (var command = new SqlCommand(cmdText, connection))
                {
                    command.CommandTimeout = (int)TimeSpan.FromMinutes(15).TotalSeconds;
                    command.Parameters.AddWithValue("BeginKey", beginKey);
                    command.Parameters.AddWithValue("EndKey", endKey);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            documents.Add(CreateDocument(reader, packageFrameworks));
                        }
                    }
                }
            }

            // Determine the latest packages for each registration.
            var packagesByKey = documents.GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.Single());

            var packageGroups = documents.GroupBy(p => p.Id);

            PackageDocument FindLatestOrDefault(
                IEnumerable<Tuple<PackageDocument, NuGetVersion>> ordered,
                bool includeUnlisted = false,
                bool includePrerelease = false,
                bool includeSemVer2 = false)
            {
                var filtered = ordered;

                if (!includeUnlisted) filtered = filtered.Where(p => p.Item1.Listed);
                if (!includePrerelease) filtered = filtered.Where(p => !p.Item2.IsPrerelease);
                if (!includeSemVer2) filtered = filtered.Where(p => !p.Item2.IsSemVer2);

                return filtered.FirstOrDefault()?.Item1;
            }

            foreach (var group in packageGroups)
            {
                var ordered = group.Select(p => Tuple.Create(p, NuGetVersion.Parse(p.VerbatimVersion)))
                    .OrderByDescending(p => p.Item2)
                    .ToList();

                var latest = FindLatestOrDefault(ordered);
                var latestIncludeSemVer2 = FindLatestOrDefault(ordered, includeSemVer2: true);
                var latestIncludePrerelease = FindLatestOrDefault(ordered, includePrerelease: true);
                var latestIncludePrereleaseAndSemVer2 = FindLatestOrDefault(ordered, includePrerelease: true, includeSemVer2: true);
                var latestIncludeUnlisted = FindLatestOrDefault(ordered, includeUnlisted: true);
                var latestIncludeUnlistedAndSemVer2 = FindLatestOrDefault(ordered, includeUnlisted: true, includeSemVer2: true);
                var latestIncludeUnlistedAndPrerelease = FindLatestOrDefault(ordered, includeUnlisted: true, includePrerelease: true);
                var latestIncludeUnlistedAndPrereleaseAndSemVer2 = FindLatestOrDefault(ordered, includeUnlisted: true, includePrerelease: true, includeSemVer2: true);

                if (latest != null) latest.Latest = true;
                if (latestIncludeSemVer2 != null) latestIncludeSemVer2.LatestIncludeSemVer2 = true;
                if (latestIncludePrerelease != null) latestIncludePrerelease.LatestIncludePrerelease = true;
                if (latestIncludePrereleaseAndSemVer2 != null) latestIncludePrereleaseAndSemVer2.LatestIncludePrereleaseAndSemVer2 = true;
                if (latestIncludeUnlisted != null) latestIncludeUnlisted.LatestIncludeUnlisted = true;
                if (latestIncludeUnlistedAndSemVer2 != null) latestIncludeUnlistedAndSemVer2.LatestIncludeUnlistedAndSemVer2 = true;
                if (latestIncludeUnlistedAndPrerelease != null) latestIncludeUnlistedAndPrerelease.LatestIncludeUnlistedAndPrerelease = true;
                if (latestIncludeUnlistedAndPrereleaseAndSemVer2 != null) latestIncludeUnlistedAndPrereleaseAndSemVer2.LatestIncludeUnlistedAndPrereleaseAndSemVer2 = true;
            }

            // Save the documents to the index.
            var batch = 0;

            using (var writer = new AzureSearchIndexWriter(searchClient.Indexes.GetClient(indexName)))
            {
                foreach (var document in documents)
                {
                    writer.AddDocument(document);

                    if (++batch == 1000)
                    {
                        writer.Commit();
                        batch = 0;
                    }
                }

                if (batch > 0)
                {
                    writer.Commit();
                }
            }
        }

        static List<Tuple<int, int>> CalculateBatches(string connectionString)
        {
            // Build an ordered list of package registration keys and a count of dependent packages.
            var list = new List<Tuple<int, int>>();

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string cmdText = @"
                    SELECT PackageRegistrations.[Key], COUNT(*)
                    FROM Packages
                    INNER JOIN PackageRegistrations ON Packages.PackageRegistrationKey = PackageRegistrations.[Key]
                    WHERE Packages.PackageStatusKey = 0
                    GROUP BY PackageRegistrations.[Key]
                    ORDER BY PackageRegistrations.[Key]
                ";

                using (var command = new SqlCommand(cmdText, connection))
                {
                    command.CommandTimeout = (int)TimeSpan.FromMinutes(15).TotalSeconds;

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(Tuple.Create(reader.GetInt32(0), reader.GetInt32(1)));
                        }
                    }
                }
            }

            // Batch the package registrations so that roughly 1,000 packages are included per batch.
            // Batches may be larger than 1,000 elements if a single package registration has more than
            // 1,000 packages.
            var batches = new List<Tuple<int, int>>();

            int beginKey = list.First().Item1;
            int batch = list.First().Item2;
            int endKey = 0;

            foreach (var x in list)
            {
                endKey = x.Item1;
                batch += x.Item2;

                if (batch > 1000)
                {
                    batches.Add(Tuple.Create(beginKey, endKey));
                    batch = x.Item2;
                    beginKey = endKey;
                }
            }

            batches.Add(Tuple.Create(beginKey, endKey + 1));

            return batches;
        }

        static IDictionary<int, List<string>> LoadPackageFrameworks(string connectionString)
        {
            var result = new Dictionary<int, List<string>>();

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                var cmdText = @"SELECT Package_Key, TargetFramework FROM PackageFrameworks";

                var command = new SqlCommand(cmdText, connection);
                command.CommandTimeout = (int)TimeSpan.FromMinutes(15).TotalSeconds;

                var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    if (reader.IsDBNull(0) || reader.IsDBNull(1))
                    {
                        continue;
                    }

                    int packageKey = reader.GetInt32(0);
                    string targetFramework = reader.GetString(1);

                    List<string> targetFrameworks;
                    if (!result.TryGetValue(packageKey, out targetFrameworks))
                    {
                        targetFrameworks = new List<string>();
                        result.Add(packageKey, targetFrameworks);
                    }

                    targetFrameworks.Add(targetFramework);
                }
            }

            return result;
        }

        public static void Export(string sourceConnectionString, SearchServiceClient searchClient, string indexName, ILoggerFactory loggerFactory)
        {
            var logger = loggerFactory.CreateLogger<Sql2Lucene>();
            var stopwatch = new Stopwatch();

            stopwatch.Start();

            var batches = CalculateBatches(sourceConnectionString);
            logger.LogInformation("Calculated {BatchCount} batches (took {BatchCalculationTime} seconds)", batches.Count, stopwatch.Elapsed.TotalSeconds);

            stopwatch.Restart();

            var packageFrameworks = LoadPackageFrameworks(sourceConnectionString);
            logger.LogInformation("Loaded package frameworks (took {PackageFrameworksLoadTime} seconds)", stopwatch.Elapsed.TotalSeconds);

            stopwatch.Restart();

            foreach (var group in GroupBatches(batches, 20))
            {
                var tasks = new List<Task>();
                foreach (var batch in group)
                {
                    tasks.Add(Task.Run(() => { IndexBatch(sourceConnectionString, searchClient, indexName, packageFrameworks, batch.Item1, batch.Item2); }));
                }

                try
                {
                    Task.WaitAll(tasks.ToArray());
                }
                catch (AggregateException ex)
                {
                    logger.LogError("An AggregateException occurred while running batches.", ex);

                    throw;
                }
            }

            logger.LogInformation("Indexes generated (took {PartitionIndexGenerationTime} seconds)", stopwatch.Elapsed.TotalSeconds);

            stopwatch.Reset();
        }

        private static List<List<Tuple<int, int>>> GroupBatches(List<Tuple<int, int>> batches, int groupSize)
        {
            var result = new List<List<Tuple<int, int>>>();
            List<Tuple<int, int>> current = null;

            foreach (var batch in batches)
            {
                if (current == null || current.Count == groupSize)
                {
                    current = new List<Tuple<int, int>>();
                    result.Add(current);
                }

                current.Add(batch);
            }

            return result;
        }
    }
}
