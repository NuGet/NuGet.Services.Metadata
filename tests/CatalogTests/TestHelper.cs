// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NgTests;
using NuGet.Services.Metadata.Catalog;
using Xunit;

namespace CatalogTests
{
    internal static class TestHelper
    {
        internal static MemoryStream GetStream(string fileName)
        {
            var path = Path.GetFullPath(Path.Combine("TestData", fileName));

            // Multiple tests may try reading the file concurrently.
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var bytes = new byte[stream.Length];

                Assert.Equal(bytes.Length, stream.Read(bytes, offset: 0, count: bytes.Length));

                return new MemoryStream(bytes, index: 0, count: bytes.Length, writable: false);
            }
        }

        internal static CatalogCommitItem CreateCatalogCommitItem(DateTime commitTimeStamp, string packageId)
        {
            var jObject = new JObject(
                new JProperty(CatalogConstants.CommitTimeStamp, commitTimeStamp.ToString("O")),
                new JProperty(CatalogConstants.IdKeyword, $"https://nuget.test/{packageId}"),
                new JProperty(CatalogConstants.NuGetId, packageId));

            return new CatalogCommitItem(jObject);
        }
    }
}