// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Metadata.Catalog;
using Xunit;

namespace CatalogTests
{
    public class CatalogCommitBatchTasksTests
    {
        private readonly DateTime _commitTimeStamp = DateTime.UtcNow;

        [Fact]
        public void Constructor_Always_ReturnsInstance()
        {
            var commitBatchTasks = new CatalogCommitBatchTasks(_commitTimeStamp);

            Assert.Equal(_commitTimeStamp, commitBatchTasks.CommitTimeStamp);
            Assert.Empty(commitBatchTasks.BatchTasks);
        }
    }
}