// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Services.Metadata.Catalog;
using Xunit;

namespace CatalogTests
{
    public class CatalogCommitItemBatchTests
    {
        private const string _packageId = "a";

        [Fact]
        public void Constructor_WhenItemsIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new CatalogCommitItemBatch(DateTime.UtcNow, items: null));

            Assert.Equal("items", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenItemsIsEmpty_Throws()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new CatalogCommitItemBatch(DateTime.UtcNow, Enumerable.Empty<CatalogCommitItem>()));

            Assert.Equal("items", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenArgumentsAreValid_ReturnsInstance()
        {
            var commitTimeStamp = DateTime.UtcNow;
            var commitItem0 = TestHelper.CreateCatalogCommitItem(commitTimeStamp, _packageId);
            var commitItem1 = TestHelper.CreateCatalogCommitItem(commitTimeStamp.AddMinutes(1), _packageId);
            var commitItem2 = TestHelper.CreateCatalogCommitItem(commitTimeStamp.AddMinutes(2), _packageId);

            var commitBatch = new CatalogCommitItemBatch(commitTimeStamp, new[] { commitItem1, commitItem0, commitItem2 });

            Assert.Equal(commitTimeStamp, commitBatch.CommitTimeStamp);
            Assert.Equal(3, commitBatch.Items.Count);
            Assert.Same(commitItem0, commitBatch.Items[0]);
            Assert.Same(commitItem1, commitBatch.Items[1]);
            Assert.Same(commitItem2, commitBatch.Items[2]);
        }
    }
}