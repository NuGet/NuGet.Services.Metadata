// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using Xunit;

namespace CatalogTests
{
    public class CatalogCommitItemTests
    {
        private const string _packageId = "a";
        private readonly DateTime _now = DateTime.UtcNow;
        private readonly JObject _jObject;

        public CatalogCommitItemTests()
        {
            var commit = TestHelper.CreateCatalogCommitItem(_now, _packageId);

            _jObject = commit.Value;
        }

        [Fact]
        public void Constructor_WhenValueIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new CatalogCommitItem(value: null));

            Assert.Equal("value", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenArgumentIsValid_ReturnsInstance()
        {
            var commit = new CatalogCommitItem(_jObject);

            Assert.Equal($"https://nuget.test/{_packageId}", commit.Uri.AbsoluteUri);
            Assert.Equal(_now, commit.CommitTimeStamp.ToUniversalTime());
            Assert.Same(_jObject, commit.Value);
        }

        [Fact]
        public void CompareTo_WhenArgumentIsValid_ReturnsValue()
        {
            var commitTimeStamp1 = DateTime.UtcNow;
            var commitTimeStamp2 = DateTime.UtcNow.AddMinutes(1);
            var commit0 = TestHelper.CreateCatalogCommitItem(commitTimeStamp1, _packageId);
            var commit1 = TestHelper.CreateCatalogCommitItem(commitTimeStamp2, _packageId);

            Assert.Equal(0, commit0.CompareTo(commit0));
            Assert.Equal(-1, commit0.CompareTo(commit1));
            Assert.Equal(1, commit1.CompareTo(commit0));
        }
    }
}