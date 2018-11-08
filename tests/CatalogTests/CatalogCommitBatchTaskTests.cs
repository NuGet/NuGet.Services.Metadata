// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Metadata.Catalog;
using Xunit;

namespace CatalogTests
{
    public class CatalogCommitBatchTaskTests
    {
        private readonly DateTime _minCommitTimeStamp = DateTime.UtcNow;
        private const string _packageId = "a";

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Constructor_WhenPackageIdIsNullEmptyOrWhitespace_Throws(string packageId)
        {
            var exception = Assert.Throws<ArgumentException>(() => new CatalogCommitBatchTask(_minCommitTimeStamp, packageId));

            Assert.Equal("packageId", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenArgumentsAreValid_ReturnsInstance()
        {
            var commitBatchTask = new CatalogCommitBatchTask(_minCommitTimeStamp, _packageId);

            Assert.Equal(_minCommitTimeStamp, commitBatchTask.MinCommitTimeStamp);
            Assert.Equal(_packageId, commitBatchTask.PackageId);
            Assert.Null(commitBatchTask.Task);
        }

        [Fact]
        public void GetHashCode_Always_ReturnsPackageIdHashCode()
        {
            var commitBatchTask = new CatalogCommitBatchTask(_minCommitTimeStamp, _packageId);

            Assert.Equal(_packageId.GetHashCode(), commitBatchTask.GetHashCode());
        }

        [Fact]
        public void Equals_WhenObjectIsNull_ReturnsFalse()
        {
            var commitBatchTask = new CatalogCommitBatchTask(_minCommitTimeStamp, _packageId);

            Assert.False(commitBatchTask.Equals(obj: null));
        }

        [Fact]
        public void Equals_WhenObjectIsNotCatalogCommitBatchTask_ReturnsFalse()
        {
            var commitBatchTask = new CatalogCommitBatchTask(_minCommitTimeStamp, _packageId);

            Assert.False(commitBatchTask.Equals(new object()));
        }

        [Fact]
        public void Equals_WhenObjectIsSameInstance_ReturnsTrue()
        {
            var commitBatchTask = new CatalogCommitBatchTask(_minCommitTimeStamp, _packageId);

            Assert.True(commitBatchTask.Equals(commitBatchTask));
        }

        [Fact]
        public void Equals_WhenObjectHasSamePackageId_ReturnsTrue()
        {
            var commitBatchTask0 = new CatalogCommitBatchTask(_minCommitTimeStamp, _packageId);
            var commitBatchTask1 = new CatalogCommitBatchTask(_minCommitTimeStamp.AddMinutes(1), _packageId);

            Assert.True(commitBatchTask0.Equals(commitBatchTask1));
        }
    }
}