// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Icons;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using Serilog.Core;
using Xunit;

namespace CatalogTests.Icons
{
    public class CatalogLeafDataProcessorFacts
    {
        public class TheGetTargetStorageIconPathMethod
        {
            [Theory]
            [InlineData("Package", "1.2.3-Preview", "package/1.2.3-preview/icon")]
            [InlineData("PpPpPPpP", "3.2.1+metaData", "pppppppp/3.2.1/icon")]
            [InlineData("Package", "1.2.3.4", "package/1.2.3.4/icon")]
            public void GeneratesProperTargetPath(string packageId, string packageVersion, string expectedPath)
            {
                var leaf = new CatalogCommitItem(
                    new Uri("https://nuget.test/something"),
                    "somecommitid",
                    DateTime.UtcNow,
                    new string[0],
                    new Uri[0],
                    new PackageIdentity(packageId, new NuGetVersion(packageVersion)));

                var path = CatalogLeafDataProcessor.GetTargetStorageIconPath(leaf);

                Assert.Equal(expectedPath, path);
            }
        }

        public class TestBase
        {
            public Mock<IAzureStorage> PackageStorageMock { get; set; }
            public Mock<IIconProcessor> IconProcessorMock { get; set; }
            public Mock<IExternalIconContentProvider> ExternalIconContentProviderMock { get; set; }
            public Mock<IIconCopyResultCache> IconCopyResultCacheMock { get; set; }
            public Mock<ITelemetryService> TelemetryServiceMock { get; set; }
            public Mock<ILogger<CatalogLeafDataProcessor>> LoggerMock { get; set; }
            public CatalogLeafDataProcessor Target { get; set; }

            public TestBase()
            {
                PackageStorageMock = new Mock<IAzureStorage>();
                IconProcessorMock = new Mock<IIconProcessor>();
                ExternalIconContentProviderMock = new Mock<IExternalIconContentProvider>();
                IconCopyResultCacheMock = new Mock<IIconCopyResultCache>();
                TelemetryServiceMock = new Mock<ITelemetryService>();
                LoggerMock = new Mock<ILogger<CatalogLeafDataProcessor>>();

                Target = new CatalogLeafDataProcessor(
                    PackageStorageMock.Object,
                    IconProcessorMock.Object,
                    ExternalIconContentProviderMock.Object,
                    IconCopyResultCacheMock.Object,
                    TelemetryServiceMock.Object,
                    LoggerMock.Object);
            }
        }
    }
}
