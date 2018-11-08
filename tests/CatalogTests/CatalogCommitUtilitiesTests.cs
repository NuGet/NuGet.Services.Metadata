// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NgTests;
using NuGet.Services.Metadata.Catalog;
using Xunit;

namespace CatalogTests
{
    public class CatalogCommitUtilitiesTests
    {
        private const int _maxConcurrentBatches = 1;
        private static readonly CatalogCommitItemBatch _lastBatch;
        private static readonly Task FailedTask = Task.FromException(new Exception());

        static CatalogCommitUtilitiesTests()
        {
            var commitTimeStamp = DateTime.UtcNow;
            var commit = TestHelper.CreateCatalogCommitItem(commitTimeStamp, packageId: "a");

            _lastBatch = new CatalogCommitItemBatch(DateTime.UtcNow, new[] { commit });
        }

        [Fact]
        public async Task CreateCommitBatchesByPackageIdAsync_WhenCatalogItemsIsNull_Throws()
        {
            IEnumerable<CatalogCommitItem> catalogItems = null;

            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => CatalogCommitUtilities.CreateCommitBatchesByPackageIdAsync(catalogItems, CatalogCommitUtilities.GetPackageIdKey));

            Assert.Equal("catalogItems", exception.ParamName);
        }

        [Fact]
        public async Task CreateCommitBatchesByPackageIdAsync_WhenGetKeyIsNull_Throws()
        {
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => CatalogCommitUtilities.CreateCommitBatchesByPackageIdAsync(Enumerable.Empty<CatalogCommitItem>(), getKey: null));

            Assert.Equal("getKey", exception.ParamName);
        }

        [Fact]
        public async Task CreateCommitBatchesByPackageIdAsync_WhenArgumentsAreValid_GroupsByPackageId()
        {
            var now = DateTime.UtcNow;
            var commit0 = TestHelper.CreateCatalogCommitItem(now, packageId: "a");
            var commit1 = TestHelper.CreateCatalogCommitItem(now.AddMinutes(1), packageId: "A");
            var commit2 = TestHelper.CreateCatalogCommitItem(now.AddMinutes(2), packageId: "A");
            var commit3 = TestHelper.CreateCatalogCommitItem(now, packageId: "b");
            var commit4 = TestHelper.CreateCatalogCommitItem(now.AddMinutes(1), packageId: "B");
            var commits = new[] { commit4, commit2, commit0, commit3, commit1 }; // not in alphanumeric or chronological order

            var batches = await CatalogCommitUtilities.CreateCommitBatchesByPackageIdAsync(commits, CatalogCommitUtilities.GetPackageIdKey);

            Assert.Collection(
                batches,
                batch =>
                {
                    Assert.Equal(commit3.CommitTimeStamp, batch.CommitTimeStamp);
                    Assert.Collection(
                        batch.Items,
                        commit => Assert.True(ReferenceEquals(commit, commit3)),
                        commit => Assert.True(ReferenceEquals(commit, commit4)));
                },
                batch =>
                {
                    Assert.Equal(commit0.CommitTimeStamp, batch.CommitTimeStamp);
                    Assert.Collection(
                        batch.Items,
                        commit => Assert.True(ReferenceEquals(commit, commit0)),
                        commit => Assert.True(ReferenceEquals(commit, commit1)),
                        commit => Assert.True(ReferenceEquals(commit, commit2)));
                });
        }

        [Fact]
        public void CreateCommitBatchTasksMap_WhenBatchesIsNull_Throws()
        {
            CatalogCommitItemBatch[] batches = null;

            var exception = Assert.Throws<ArgumentException>(
                () => CatalogCommitUtilities.CreateCommitBatchTasksMap(batches, CatalogCommitUtilities.GetPackageIdKey));

            Assert.Equal("batches", exception.ParamName);
        }

        [Fact]
        public void CreateCommitBatchTasksMap_WhenBatchesIsEmpty_Throws()
        {
            var batches = Array.Empty<CatalogCommitItemBatch>();

            var exception = Assert.Throws<ArgumentException>(
                () => CatalogCommitUtilities.CreateCommitBatchTasksMap(batches, CatalogCommitUtilities.GetPackageIdKey));

            Assert.Equal("batches", exception.ParamName);
        }

        [Fact]
        public void CreateCommitBatchTasksMap_WhenGetKeyIsNull_Throws()
        {
            var commitTimeStamp = DateTime.UtcNow;
            var commit = TestHelper.CreateCatalogCommitItem(commitTimeStamp, packageId: "a");
            var commitBatch = new CatalogCommitItemBatch(commitTimeStamp, new[] { commit });
            var commitBatches = new[] { commitBatch };

            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.CreateCommitBatchTasksMap(commitBatches, getKey: null));

            Assert.Equal("getKey", exception.ParamName);
        }

        [Fact]
        public void CreateCommitBatchTasksMap_WhenArgumentsAreValid_ReturnsMap()
        {
            var commitTimeStamp = DateTime.UtcNow;

            const string packageId0 = "a";
            var commit0 = TestHelper.CreateCatalogCommitItem(commitTimeStamp, packageId0);
            var commit1 = TestHelper.CreateCatalogCommitItem(commitTimeStamp.AddMinutes(1), packageId0);
            var commitBatch0 = new CatalogCommitItemBatch(commit0.CommitTimeStamp, new[] { commit0, commit1 });

            const string packageId1 = "b";
            var commit2 = TestHelper.CreateCatalogCommitItem(commitTimeStamp.AddMinutes(1), packageId1);
            var commit3 = TestHelper.CreateCatalogCommitItem(commitTimeStamp.AddMinutes(2), packageId1);
            var commitBatch1 = new CatalogCommitItemBatch(commit2.CommitTimeStamp, new[] { commit2, commit3 });

            var commitBatches = new[] { commitBatch0, commitBatch1 };

            var map = CatalogCommitUtilities.CreateCommitBatchTasksMap(commitBatches, CatalogCommitUtilities.GetPackageIdKey);

            Assert.Collection(
                map,
                element =>
                {
                    Assert.Equal(commitTimeStamp, element.Key.ToUniversalTime());
                    Assert.Equal(commitTimeStamp, element.Value.CommitTimeStamp.ToUniversalTime());
                    Assert.Single(element.Value.BatchTasks);

                    var batchTask = element.Value.BatchTasks.Single();

                    Assert.Equal(commitBatch0.CommitTimeStamp, batchTask.MinCommitTimeStamp);
                    Assert.Equal(packageId0, batchTask.PackageId);
                },
                element =>
                {
                    var expectedCommitTimeStamp = commitTimeStamp.AddMinutes(1);

                    Assert.Equal(expectedCommitTimeStamp, element.Key.ToUniversalTime());
                    Assert.Equal(expectedCommitTimeStamp, element.Value.CommitTimeStamp.ToUniversalTime());
                    Assert.Equal(2, element.Value.BatchTasks.Count);

                    var batchTask = element.Value.BatchTasks.First();

                    Assert.Equal(commitBatch0.CommitTimeStamp, batchTask.MinCommitTimeStamp);
                    Assert.Equal(packageId0, batchTask.PackageId);

                    batchTask = element.Value.BatchTasks.Last();

                    Assert.Equal(commitBatch1.CommitTimeStamp, batchTask.MinCommitTimeStamp);
                    Assert.Equal(packageId1, batchTask.PackageId);
                },
                element =>
                {
                    var expectedCommitTimeStamp = commitTimeStamp.AddMinutes(2);

                    Assert.Equal(expectedCommitTimeStamp, element.Key.ToUniversalTime());
                    Assert.Equal(expectedCommitTimeStamp, element.Value.CommitTimeStamp.ToUniversalTime());
                    Assert.Single(element.Value.BatchTasks);

                    var batchTask = element.Value.BatchTasks.Single();

                    Assert.Equal(commitBatch1.CommitTimeStamp, batchTask.MinCommitTimeStamp);
                    Assert.Equal(packageId1, batchTask.PackageId);
                });
        }

        [Fact]
        public void DequeueBatchesWhileMatches_WhenBatchesIsNull_Throws()
        {
            Queue<CatalogCommitBatchTask> batches = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.DequeueBatchesWhileMatches(batches, _ => true));

            Assert.Equal("batches", exception.ParamName);
        }

        [Fact]
        public void DequeueBatchesWhileMatches_WhenIsMatchIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.DequeueBatchesWhileMatches(
                    new Queue<CatalogCommitBatchTask>(),
                    isMatch: null));

            Assert.Equal("isMatch", exception.ParamName);
        }

        [Fact]
        public void DequeueBatchesWhileMatches_WhenQueueIsEmpty_NoOps()
        {
            var batches = new Queue<CatalogCommitBatchTask>();

            CatalogCommitUtilities.DequeueBatchesWhileMatches(batches, batch => true);

            Assert.Empty(batches);
        }

        [Fact]
        public void DequeueBatchesWhileMatches_WhenNoMatchIsFound_NoOps()
        {
            var now = DateTime.UtcNow;
            var id0 = "a";
            var id1 = "b";
            var commitBatchTask0 = new CatalogCommitBatchTask(now, id0);
            var commitBatchTask1 = new CatalogCommitBatchTask(now.AddMinutes(1), id1);
            var commitBatchTask2 = new CatalogCommitBatchTask(now.AddMinutes(2), id0);

            var batches = new Queue<CatalogCommitBatchTask>();

            batches.Enqueue(commitBatchTask0);
            batches.Enqueue(commitBatchTask1);
            batches.Enqueue(commitBatchTask2);

            CatalogCommitUtilities.DequeueBatchesWhileMatches(batches, batch => false);

            Assert.Equal(3, batches.Count);
        }

        [Fact]
        public void DequeueBatchesWhileMatches_WhenMatchIsFound_Dequeues()
        {
            var now = DateTime.UtcNow;
            var id0 = "a";
            var id1 = "b";
            var commitBatchTask0 = new CatalogCommitBatchTask(now, id0);
            var commitBatchTask1 = new CatalogCommitBatchTask(now.AddMinutes(1), id1);
            var commitBatchTask2 = new CatalogCommitBatchTask(now.AddMinutes(2), id0);

            var batches = new Queue<CatalogCommitBatchTask>();

            batches.Enqueue(commitBatchTask0);
            batches.Enqueue(commitBatchTask1);
            batches.Enqueue(commitBatchTask2);

            CatalogCommitUtilities.DequeueBatchesWhileMatches(batches, batch => batch.PackageId == id0);

            Assert.Equal(2, batches.Count);
            Assert.Same(commitBatchTask1, batches.Dequeue());
            Assert.Same(commitBatchTask2, batches.Dequeue());
        }

        [Fact]
        public void EnqueueBatchesIfNoFailures_WhenClientIsNull_Throws()
        {
            const CollectorHttpClient client = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.EnqueueBatchesIfNoFailures(
                    client,
                    new JObject(),
                    new SortedDictionary<DateTime, CatalogCommitBatchTasks>(),
                    new Queue<CatalogCommitItemBatch>(),
                    new Queue<CatalogCommitBatchTask>(),
                    _lastBatch,
                    _maxConcurrentBatches,
                    CatalogCommitUtilities.GetPackageIdKey,
                    NoOpProcessBatchAsync,
                    CancellationToken.None));

            Assert.Equal("client", exception.ParamName);
        }

        [Fact]
        public void EnqueueBatchesIfNoFailures_WhenContextIsNull_Throws()
        {
            const JToken context = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.EnqueueBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    context,
                    new SortedDictionary<DateTime, CatalogCommitBatchTasks>(),
                    new Queue<CatalogCommitItemBatch>(),
                    new Queue<CatalogCommitBatchTask>(),
                    _lastBatch,
                    _maxConcurrentBatches,
                    CatalogCommitUtilities.GetPackageIdKey,
                    NoOpProcessBatchAsync,
                    CancellationToken.None));

            Assert.Equal("context", exception.ParamName);
        }

        [Fact]
        public void EnqueueBatchesIfNoFailures_WhenCommitBatchTasksMapIsNull_Throws()
        {
            const SortedDictionary<DateTime, CatalogCommitBatchTasks> commitBatchTasksMap = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.EnqueueBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    new JObject(),
                    commitBatchTasksMap,
                    new Queue<CatalogCommitItemBatch>(),
                    new Queue<CatalogCommitBatchTask>(),
                    _lastBatch,
                    _maxConcurrentBatches,
                    CatalogCommitUtilities.GetPackageIdKey,
                    NoOpProcessBatchAsync,
                    CancellationToken.None));

            Assert.Equal("commitBatchTasksMap", exception.ParamName);
        }

        [Fact]
        public void EnqueueBatchesIfNoFailures_WhenUnprocessedBatchesIsNull_Throws()
        {
            const Queue<CatalogCommitItemBatch> unprocessedBatches = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.EnqueueBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    new JObject(),
                    new SortedDictionary<DateTime, CatalogCommitBatchTasks>(),
                    unprocessedBatches,
                    new Queue<CatalogCommitBatchTask>(),
                    _lastBatch,
                    _maxConcurrentBatches,
                    CatalogCommitUtilities.GetPackageIdKey,
                    NoOpProcessBatchAsync,
                    CancellationToken.None));

            Assert.Equal("unprocessedBatches", exception.ParamName);
        }

        [Fact]
        public void EnqueueBatchesIfNoFailures_WhenProcessingBatchesIsNull_Throws()
        {
            const Queue<CatalogCommitBatchTask> processingBatches = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.EnqueueBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    new JObject(),
                    new SortedDictionary<DateTime, CatalogCommitBatchTasks>(),
                    new Queue<CatalogCommitItemBatch>(),
                    processingBatches,
                    _lastBatch,
                    _maxConcurrentBatches,
                    CatalogCommitUtilities.GetPackageIdKey,
                    NoOpProcessBatchAsync,
                    CancellationToken.None));

            Assert.Equal("processingBatches", exception.ParamName);
        }

        [Fact]
        public void EnqueueBatchesIfNoFailures_WhenLastBatchIsNull_Throws()
        {
            const CatalogCommitItemBatch lastBatch = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.EnqueueBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    new JObject(),
                    new SortedDictionary<DateTime, CatalogCommitBatchTasks>(),
                    new Queue<CatalogCommitItemBatch>(),
                    new Queue<CatalogCommitBatchTask>(),
                    lastBatch,
                    _maxConcurrentBatches,
                    CatalogCommitUtilities.GetPackageIdKey,
                    NoOpProcessBatchAsync,
                    CancellationToken.None));

            Assert.Equal("lastBatch", exception.ParamName);
        }

        [Fact]
        public void EnqueueBatchesIfNoFailures_WhenMaxConcurrentBatchesIsLessThanOne_Throws()
        {
            const int maxConcurrentBatches = 0;

            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => CatalogCommitUtilities.EnqueueBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    new JObject(),
                    new SortedDictionary<DateTime, CatalogCommitBatchTasks>(),
                    new Queue<CatalogCommitItemBatch>(),
                    new Queue<CatalogCommitBatchTask>(),
                    _lastBatch,
                    maxConcurrentBatches,
                    CatalogCommitUtilities.GetPackageIdKey,
                    NoOpProcessBatchAsync,
                    CancellationToken.None));

            Assert.Equal("maxConcurrentBatches", exception.ParamName);
        }

        [Fact]
        public void EnqueueBatchesIfNoFailures_WhenGetKeyIsNull_Throws()
        {
            const CatalogCommitUtilities.GetKey getKey = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.EnqueueBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    new JObject(),
                    new SortedDictionary<DateTime, CatalogCommitBatchTasks>(),
                    new Queue<CatalogCommitItemBatch>(),
                    new Queue<CatalogCommitBatchTask>(),
                    _lastBatch,
                    _maxConcurrentBatches,
                    getKey,
                    NoOpProcessBatchAsync,
                    CancellationToken.None));

            Assert.Equal("getKey", exception.ParamName);
        }

        [Fact]
        public void EnqueueBatchesIfNoFailures_WhenProcessBatchAsyncIsNull_Throws()
        {
            const CatalogCommitUtilities.ProcessBatchAsync processBatchAsync = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.EnqueueBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    new JObject(),
                    new SortedDictionary<DateTime, CatalogCommitBatchTasks>(),
                    new Queue<CatalogCommitItemBatch>(),
                    new Queue<CatalogCommitBatchTask>(),
                    _lastBatch,
                    _maxConcurrentBatches,
                    CatalogCommitUtilities.GetPackageIdKey,
                    processBatchAsync,
                    CancellationToken.None));

            Assert.Equal("processBatchAsync", exception.ParamName);
        }

        public class EnqueueBatchesIfNoFailures
        {
            private const string _packageId0 = "a";
            private const string _packageId1 = "b";
            private const string _packageId2 = "c";
            private const string _packageId3 = "d";
            private readonly DateTime _now = DateTime.UtcNow;
            private readonly CatalogCommitItem _commitItem0;
            private readonly CatalogCommitItem _commitItem1;
            private readonly CatalogCommitItem _commitItem2;
            private readonly CatalogCommitItem _commitItem3;

            public EnqueueBatchesIfNoFailures()
            {
                _commitItem0 = TestHelper.CreateCatalogCommitItem(_now, _packageId0);
                _commitItem1 = TestHelper.CreateCatalogCommitItem(_now, _packageId1);
                _commitItem2 = TestHelper.CreateCatalogCommitItem(_now, _packageId2);
                _commitItem3 = TestHelper.CreateCatalogCommitItem(_now, _packageId3);
            }

            [Fact(Skip = "wip")]
            public void EnqueueBatchesIfNoFailures_WhenAnyBatchIsFailed_NoOps()
            {
                var commitItemBatch = new CatalogCommitItemBatch(_now, new[] { _commitItem0, _commitItem1 });
                var task0 = new CatalogCommitBatchTask(_now, packageId: _packageId0) { Task = FailedTask };
                var batchTasks0 = new CatalogCommitBatchTasks(_now);
                var commitBatchMap = CatalogCommitUtilities.CreateCommitBatchTasksMap(
                    new[] { commitItemBatch },
                    CatalogCommitUtilities.GetPackageIdKey);

                batchTasks0.BatchTasks.Add(task0);

                var unprocessedBatches = new Queue<CatalogCommitItemBatch>();

                unprocessedBatches.Enqueue(commitItemBatch);

                var processingBatches = new Queue<CatalogCommitBatchTask>();

                processingBatches.Enqueue(task0);

                CatalogCommitUtilities.EnqueueBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    new JObject(),
                    commitBatchMap,
                    unprocessedBatches,
                    processingBatches,
                    _lastBatch,
                    _maxConcurrentBatches,
                    CatalogCommitUtilities.GetPackageIdKey,
                    NoOpProcessBatchAsync,
                    CancellationToken.None);

                Assert.Equal(1, unprocessedBatches.Count);
                Assert.Equal(3, processingBatches.Count);
            }

            [Fact(Skip = "wip")]
            public void EnqueueBatchesIfNoFailures_WhenNoBatchIsFailedOrCancelled_NoOps()
            {
                var commitItemBatch = new CatalogCommitItemBatch(_now, new[] { _commitItem0, _commitItem1 });
                var task0 = new CatalogCommitBatchTask(_now, packageId: _packageId0) { Task = Task.CompletedTask };
                var batchTasks0 = new CatalogCommitBatchTasks(_now);
                var commitBatchMap = CatalogCommitUtilities.CreateCommitBatchTasksMap(
                    new[] { commitItemBatch },
                    CatalogCommitUtilities.GetPackageIdKey);

                batchTasks0.BatchTasks.Add(task0);

                var unprocessedBatches = new Queue<CatalogCommitItemBatch>();

                unprocessedBatches.Enqueue(commitItemBatch);

                var processingBatches = new Queue<CatalogCommitBatchTask>();

                processingBatches.Enqueue(task0);

                CatalogCommitUtilities.EnqueueBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    new JObject(),
                    commitBatchMap,
                    unprocessedBatches,
                    processingBatches,
                    _lastBatch,
                    _maxConcurrentBatches,
                    CatalogCommitUtilities.GetPackageIdKey,
                    NoOpProcessBatchAsync,
                    CancellationToken.None);

                Assert.Equal(1, unprocessedBatches.Count);
                Assert.Equal(3, processingBatches.Count);
            }
        }

        [Fact]
        public void GetPackageIdKey_WhenItemIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.GetPackageIdKey(item: null));

            Assert.Equal("item", exception.ParamName);
        }

        [Theory]
        [InlineData("a")]
        [InlineData("A")]
        public void GetPackageIdKey_WhenPackageIdVariesInCase_ReturnsLowerCase(string packageId)
        {
            var item = new JObject(new JProperty(CatalogConstants.NuGetId, packageId));
            var key = CatalogCommitUtilities.GetPackageIdKey(item);

            Assert.Equal(packageId.ToLowerInvariant(), key);
        }

        private static CatalogCommitItemBatch CreateCatalogCommitBatch(DateTime commitTimeStamp, string packageId)
        {
            var commit = TestHelper.CreateCatalogCommitItem(commitTimeStamp, packageId);

            return new CatalogCommitItemBatch(commitTimeStamp, new[] { commit });
        }

        private static Task NoOpProcessBatchAsync(
            CollectorHttpClient client,
            JToken context,
            string packageId,
            CatalogCommitItemBatch batch,
            CatalogCommitItemBatch lastBatch,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}