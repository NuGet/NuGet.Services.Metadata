// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Lucene.Net.Documents;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NuGet.Indexing;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;

namespace Ng
{
    public class SearchIndexFromCatalogCollector : CommitCollector
    {
        private readonly string _baseAddress;

        private readonly AzureSearchIndexWriter _indexWriter;
        private readonly bool _commitEachBatch;
        private readonly ILogger _logger;

        private LuceneCommitMetadata _metadataForNextCommit;

        public SearchIndexFromCatalogCollector(
            Uri index,
            AzureSearchIndexWriter indexWriter,
            bool commitEachBatch,
            string baseAddress,
            ITelemetryService telemetryService,
            ILogger logger,
            Func<HttpMessageHandler> handlerFunc = null)
            : base(index, telemetryService, handlerFunc)
        {
            _indexWriter = indexWriter;
            _commitEachBatch = commitEachBatch;
            _baseAddress = baseAddress;
            _logger = logger;
        }

        protected override async Task<bool> OnProcessBatch(CollectorHttpClient client, IEnumerable<JToken> items, JToken context, DateTime commitTimeStamp, bool isLastBatch, CancellationToken cancellationToken)
        {
            JObject catalogIndex = null; ;
            if (_baseAddress != null)
            {
                var stopwatch = Stopwatch.StartNew();
                catalogIndex = await client.GetJObjectAsync(Index, cancellationToken);
                _telemetryService.TrackCatalogIndexReadDuration(stopwatch.Elapsed, Index);
            }

            IEnumerable<JObject> catalogItems = await FetchCatalogItems(client, items, cancellationToken);

            ProcessCatalogItems(_indexWriter, catalogItems, _baseAddress);

            _logger.LogInformation("Processed catalog items.");

            return true;
        }

        private void UpdateCommitMetadata(DateTime commitTimeStamp, int docsDifference)
        {
            var count = docsDifference;
            if (_metadataForNextCommit != null)
            {
                // we want the total for the entire commit, so add to the number we already have
                count += _metadataForNextCommit.Count; 
            }

            _metadataForNextCommit = DocumentCreator.CreateCommitMetadata(
                commitTimeStamp, "from catalog", count, Guid.NewGuid().ToString());
        }

        private static async Task<IEnumerable<JObject>> FetchCatalogItems(CollectorHttpClient client, IEnumerable<JToken> items, CancellationToken cancellationToken)
        {
            IList<Task<JObject>> tasks = new List<Task<JObject>>();

            foreach (JToken item in items)
            {
                Uri catalogItemUri = item["@id"].ToObject<Uri>();

                tasks.Add(client.GetJObjectAsync(catalogItemUri, cancellationToken));
            }

            await Task.WhenAll(tasks);

            return tasks.Select(t => t.Result);
        }

        private void ProcessCatalogItems(AzureSearchIndexWriter indexWriter, IEnumerable<JObject> catalogItems, string baseAddress)
        {
            int count = 0;
            int batch = 0;

            foreach (JObject catalogItem in catalogItems)
            {
                _logger.LogInformation("Process CatalogItem {CatalogItem}", catalogItem["@id"]);

                NormalizeId(catalogItem);

                if (Utils.IsType(GetContext(catalogItem), catalogItem, Schema.DataTypes.PackageDetails))
                {
                    ProcessPackageDetails(indexWriter, catalogItem);
                }
                else if (Utils.IsType(GetContext(catalogItem), catalogItem, Schema.DataTypes.PackageDelete))
                {
                    ProcessPackageDelete(indexWriter, catalogItem);
                }
                else
                {
                    _logger.LogInformation("Unrecognized @type ignoring CatalogItem");
                }

                if (++batch == 1000)
                {
                    indexWriter.Commit();
                    batch = 0;
                }

                count++;
            }

            if (batch > 0)
            {
                indexWriter.Commit();
            }

            _logger.LogInformation(string.Format("Processed {0} CatalogItems", count));
        }

        private static void NormalizeId(JObject catalogItem)
        {
            // for now, for apiapps, we have prepended the id in the catalog with the namespace, however we don't want this to impact the Lucene index
            JToken originalId = catalogItem["originalId"];
            if (originalId != null)
            {
                catalogItem["id"] = originalId.ToString();
            }
        }

        private static JToken GetContext(JObject catalogItem)
        {
            return catalogItem["@context"];
        }

        private void ProcessPackageDetails(AzureSearchIndexWriter indexWriter, JObject catalogItem)
        {
            _logger.LogDebug("ProcessPackageDetails");

            var package = CatalogPackageMetadataExtraction.MakePackageMetadata(catalogItem);
            var document = DocumentCreator.CreateDocument(package);

            indexWriter.AddDocument(document);
        }

        private void ProcessPackageDelete(AzureSearchIndexWriter indexWriter, JObject catalogItem)
        {
            _logger.LogDebug("ProcessPackageDelete");

            string id = catalogItem["id"].ToString();
            string version = catalogItem["version"].ToString();

            version = NuGetVersion.Parse(version).ToNormalizedString();

            indexWriter.DeleteDocument(id, version);
        }

        private static void AddStoragePaths(Document doc, IEnumerable<string> storagePaths, string baseAddress)
        {
            int len = baseAddress.Length;
            foreach (string storagePath in storagePaths)
            {
                if (storagePath.StartsWith(baseAddress))
                {
                    string relativePath = storagePath.Substring(len);
                    doc.Add(new Field("StoragePath", relativePath, Field.Store.YES, Field.Index.NOT_ANALYZED));
                }
            }
        }

        private static IEnumerable<string> GetStoragePaths(JObject package)
        {
            IList<string> storagePaths = new List<string>();
            storagePaths.Add(package["@id"].ToString());
            storagePaths.Add(package["packageContent"].ToString());

            foreach (JObject entry in package["entries"])
            {
                storagePaths.Add(entry["location"].ToString());
            }

            return storagePaths;
        }

        private static IEnumerable<string> GetCatalogStoragePaths(JObject index)
        {
            IList<string> storagePaths = new List<string>();
            storagePaths.Add(index["@id"].ToString());

            foreach (JObject page in index["items"])
            {
                storagePaths.Add(page["@id"].ToString());
            }

            return storagePaths;
        }
    }
}
