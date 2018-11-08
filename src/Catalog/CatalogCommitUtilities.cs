// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace NuGet.Services.Metadata.Catalog
{
    public static class CatalogCommitUtilities
    {
        internal delegate Task<IEnumerable<CatalogCommitItemBatch>> CreateBatchesAsync(
            IEnumerable<CatalogCommitItem> catalogItems);

        internal delegate Task<IEnumerable<CatalogCommitItem>> FetchCatalogItemsAsync(
            CollectorHttpClient client,
            ReadWriteCursor front,
            CancellationToken cancellationToken);

        public delegate string GetKey(JObject item);

        public delegate Task ProcessBatchAsync(
            CollectorHttpClient client,
            JToken context,
            string packageId,
            CatalogCommitItemBatch batch,
            CatalogCommitItemBatch lastBatch,
            CancellationToken cancellationToken);

        private static async Task<CatalogCommitItemBatch[]> CreateBatchesForAllAvailableItemsInPageAsync(
            ReadWriteCursor front,
            ReadCursor back,
            JObject page,
            CreateBatchesAsync createBatchesAsync)
        {
            IEnumerable<CatalogCommitItem> pageItems = page["items"]
                .Select(item => new CatalogCommitItem((JObject)item))
                .Where(item => item.CommitTimeStamp > front.Value && item.CommitTimeStamp <= back.Value);

            IEnumerable<CatalogCommitItemBatch> batches = await createBatchesAsync(pageItems);

            return batches
                .OrderBy(batch => batch.CommitTimeStamp)
                .ToArray();
        }

        public static Task<IEnumerable<CatalogCommitItemBatch>> CreateCommitBatchesByPackageIdAsync(
            IEnumerable<CatalogCommitItem> catalogItems,
            Func<JObject, string> getKey)
        {
            if (catalogItems == null)
            {
                throw new ArgumentNullException(nameof(catalogItems));
            }

            if (getKey == null)
            {
                throw new ArgumentNullException(nameof(getKey));
            }

            // Grouping batches by commit is slow if it contains
            // the same package registration id over and over again.
            // This happens when, for example, a package publish "wave"
            // occurs.
            //
            // If one package registration id is part of 20 batches,
            // we'll have to process all registration leafs 20 times.
            // It would be better to process these leafs only once.
            //
            // So let's batch by package registration id here,
            // ensuring we never write a commit timestamp to the cursor
            // that is higher than the last item currently processed.
            //
            // So, group by id, then make sure the batch key is the
            // *lowest*  timestamp of all commits in that batch.
            // This ensures that on retries, we will retry
            // from the correct location (even though we may have
            // a little rework).

            var batches = catalogItems
                .GroupBy(item => getKey(item.Value))
                .Select(group => new CatalogCommitItemBatch(
                    group.Min(item => item.CommitTimeStamp),
                    group));

            return Task.FromResult(batches);
        }

        public static SortedDictionary<DateTime, CatalogCommitBatchTasks> CreateCommitBatchTasksMap(
            CatalogCommitItemBatch[] batches,
            GetKey getKey)
        {
            if (batches == null || !batches.Any())
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullOrEmpty, nameof(batches));
            }

            if (getKey == null)
            {
                throw new ArgumentNullException(nameof(getKey));
            }

            var map = new SortedDictionary<DateTime, CatalogCommitBatchTasks>();

            foreach (var batch in batches)
            {
                var jObject = batch.Items.First().Value;
                var packageId = getKey(jObject);
                var batchTask = new CatalogCommitBatchTask(batch.CommitTimeStamp, packageId);

                foreach (var commitTimeStamp in batch.Items.Select(item => item.CommitTimeStamp))
                {
                    CatalogCommitBatchTasks commitBatchTasks;

                    if (!map.TryGetValue(commitTimeStamp, out commitBatchTasks))
                    {
                        commitBatchTasks = new CatalogCommitBatchTasks(commitTimeStamp);

                        map[commitTimeStamp] = commitBatchTasks;
                    }

                    commitBatchTasks.BatchTasks.Add(batchTask);
                }
            }

            return map;
        }

        public static void DequeueBatchesWhileMatches(
            Queue<CatalogCommitBatchTask> batches,
            Func<CatalogCommitBatchTask, bool> isMatch)
        {
            if (batches == null)
            {
                throw new ArgumentNullException(nameof(batches));
            }

            if (isMatch == null)
            {
                throw new ArgumentNullException(nameof(isMatch));
            }

            CatalogCommitBatchTask batch;

            while ((batch = batches.FirstOrDefault()) != null)
            {
                if (isMatch(batch))
                {
                    batches.Dequeue();
                }
                else
                {
                    break;
                }
            }
        }

        public static void EnqueueBatchesIfNoFailures(
            CollectorHttpClient client,
            JToken context,
            SortedDictionary<DateTime, CatalogCommitBatchTasks> commitBatchTasksMap,
            Queue<CatalogCommitItemBatch> unprocessedBatches,
            Queue<CatalogCommitBatchTask> processingBatches,
            CatalogCommitItemBatch lastBatch,
            int maxConcurrentBatches,
            GetKey getKey,
            ProcessBatchAsync processBatchAsync,
            CancellationToken cancellationToken)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (commitBatchTasksMap == null)
            {
                throw new ArgumentNullException(nameof(commitBatchTasksMap));
            }

            if (unprocessedBatches == null)
            {
                throw new ArgumentNullException(nameof(unprocessedBatches));
            }

            if (processingBatches == null)
            {
                throw new ArgumentNullException(nameof(processingBatches));
            }

            if (lastBatch == null)
            {
                throw new ArgumentNullException(nameof(lastBatch));
            }

            if (maxConcurrentBatches < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxConcurrentBatches),
                    maxConcurrentBatches,
                    string.Format(Strings.ArgumentOutOfRange, 1, int.MaxValue));
            }

            if (getKey == null)
            {
                throw new ArgumentNullException(nameof(getKey));
            }

            if (processBatchAsync == null)
            {
                throw new ArgumentNullException(nameof(processBatchAsync));
            }

            var hasAnyBatchFailed = processingBatches.Any(batch => batch.Task.IsFaulted || batch.Task.IsCanceled);

            if (hasAnyBatchFailed)
            {
                return;
            }

            var batchesToEnqueue = Math.Min(
                maxConcurrentBatches - processingBatches.Count(batch => !batch.Task.IsCompleted),
                unprocessedBatches.Count);

            for (var i = 0; i < batchesToEnqueue; ++i)
            {
                var batch = unprocessedBatches.Dequeue();
                var batchItem = batch.Items.First();
                var packageId = getKey(batchItem.Value);

                var batchTask = commitBatchTasksMap[batchItem.CommitTimeStamp].BatchTasks
                    .Single(bt => bt.PackageId == packageId);

                batchTask.Task = processBatchAsync(client, context, packageId, batch, lastBatch, cancellationToken);

                processingBatches.Enqueue(batchTask);
            }
        }

        internal static async Task<bool> FetchAsync(
            CollectorHttpClient client,
            ReadWriteCursor front,
            ReadCursor back,
            FetchCatalogItemsAsync fetchCatalogItemsAsync,
            GetKey getKey,
            CreateBatchesAsync createBatchesAsync,
            ProcessBatchAsync processBatchAsync,
            int maxConcurrentBatches,
            string typeName,
            CancellationToken cancellationToken)
        {
            IEnumerable<CatalogCommitItem> catalogItems = await fetchCatalogItemsAsync(client, front, cancellationToken);

            var hasAnyBatchFailed = false;
            var hasAnyBatchBeenProcessed = false;

            foreach (CatalogCommitItem catalogItem in catalogItems)
            {
                JObject page = await client.GetJObjectAsync(catalogItem.Uri, cancellationToken);
                JToken context = page["@context"];
                CatalogCommitItemBatch[] batches = await CreateBatchesForAllAvailableItemsInPageAsync(front, back, page, createBatchesAsync);
                SortedDictionary<DateTime, CatalogCommitBatchTasks> commitBatchTasksMap = CreateCommitBatchTasksMap(batches, getKey);

                var unprocessedBatches = new Queue<CatalogCommitItemBatch>(batches);
                var processingBatches = new Queue<CatalogCommitBatchTask>();

                CatalogCommitItemBatch lastBatch = unprocessedBatches.LastOrDefault();
                var exceptions = new List<Exception>();

                EnqueueBatchesIfNoFailures(
                    client,
                    context,
                    commitBatchTasksMap,
                    unprocessedBatches,
                    processingBatches,
                    lastBatch,
                    maxConcurrentBatches,
                    getKey,
                    processBatchAsync,
                    cancellationToken);

                while (processingBatches.Any())
                {
                    var activeTasks = processingBatches.Where(batch => !batch.Task.IsCompleted)
                        .Select(batch => batch.Task)
                        .DefaultIfEmpty(Task.CompletedTask);

                    await Task.WhenAny(activeTasks);

                    while (!hasAnyBatchFailed && commitBatchTasksMap.Any())
                    {
                        var commitBatchTasks = commitBatchTasksMap.First().Value;
                        var isCommitFullyProcessed = commitBatchTasks.BatchTasks.All(batch => batch.Task != null && batch.Task.IsCompleted);

                        if (!isCommitFullyProcessed)
                        {
                            break;
                        }

                        var isCommitSuccessfullyProcessed = commitBatchTasks.BatchTasks.All(batch => batch.Task.Status == TaskStatus.RanToCompletion);

                        if (isCommitSuccessfullyProcessed)
                        {
                            var commitTimeStamp = commitBatchTasks.CommitTimeStamp;

                            front.Value = commitTimeStamp;

                            await front.SaveAsync(cancellationToken);

                            Trace.TraceInformation($"{typeName}.{nameof(FetchAsync)} {nameof(front)}.{nameof(front.Value)} saved since timestamp changed from previous: {{0}}", front);

                            DequeueBatchesWhileMatches(processingBatches, batch => batch.MinCommitTimeStamp == commitTimeStamp);

                            commitBatchTasksMap.Remove(commitTimeStamp);
                        }
                        else // Canceled or Failed
                        {
                            hasAnyBatchFailed = true;

                            exceptions.AddRange(
                                commitBatchTasks.BatchTasks
                                    .Select(batch => batch.Task)
                                    .Where(task => (task.IsFaulted || task.IsCanceled) && task.Exception != null)
                                    .Select(task => ExceptionUtilities.Unwrap(task.Exception)));
                        }
                    }

                    if (hasAnyBatchFailed)
                    {
                        DequeueBatchesWhileMatches(processingBatches, batch => batch.Task.IsCompleted);
                    }

                    hasAnyBatchBeenProcessed = true;

                    EnqueueBatchesIfNoFailures(
                        client,
                        context,
                        commitBatchTasksMap,
                        unprocessedBatches,
                        processingBatches,
                        lastBatch,
                        maxConcurrentBatches,
                        getKey,
                        processBatchAsync,
                        cancellationToken);
                }

                if (hasAnyBatchFailed)
                {
                    var innerException = exceptions.Count == 1 ? exceptions.Single() : new AggregateException(exceptions);

                    throw new BatchProcessingException(innerException);
                }
            }

            return hasAnyBatchBeenProcessed;
        }

        public static string GetPackageIdKey(JObject item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            return item["nuget:id"].ToString().ToLowerInvariant();
        }
    }
}