// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Icons;
using NuGet.Services.Metadata.Catalog.Persistence;
using Xunit;

namespace CatalogTests.Icons
{
    public class IconProcessorFacts
    {
        public class TheCopyIconFromExternalSourceMethod : TestBase
        {
            [Fact]
            public async Task ReadsAndUsesStreamData()
            {
                var data = new byte[] { 0xFF, 0xD8, 0xFF, 0xAA };

                using (var ms = new MemoryStream(data))
                {
                    await Target.CopyIconFromExternalSource(ms, DestinationStorageMock.Object, "somePath", CancellationToken.None, "theid", "1.2.3");
                }

                DestinationStorageMock.Verify(ds => ds.SaveAsync(
                    It.IsAny<Uri>(),
                    It.Is<StorageContent>(sc => SameData(data, sc)),
                    It.IsAny<CancellationToken>()));
            }

            public static IEnumerable<object[]> SvgData = new[] { 
                new object[] { Encoding.UTF8.GetBytes("<svg></svg>"), "image/svg+xml" }
            };

            [Theory]
            [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x42}, "image/png")]
            [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0x21, 0x17 }, "image/jpeg")]
            [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61, 0x34, 0x12 }, "image/gif")]
            [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x45, 0x98, 0x03 }, "image/gif")]
            [InlineData(new byte[] { 0x00, 0x00, 0x01, 0x00, 0x92 }, "image/x-icon")]
            [MemberData(nameof(SvgData))]
            public async Task DeterminesContentType(byte[] data, string expectedContentType)
            {
                using (var ms = new MemoryStream(data))
                {
                    await Target.CopyIconFromExternalSource(ms, DestinationStorageMock.Object, "somePath", CancellationToken.None, "theid", "1.2.3");
                }

                DestinationStorageMock.Verify(ds => ds.SaveAsync(
                    It.IsAny<Uri>(),
                    It.Is<StorageContent>(sc => expectedContentType == sc.ContentType),
                    It.IsAny<CancellationToken>()));
            }

            private static bool SameData(byte[] data, StorageContent storageContent)
            {
                using (var dataStream = storageContent.GetContentStream())
                using (var m = new MemoryStream())
                {
                    dataStream.CopyTo(m);
                    var submittedArray = m.ToArray();
                    return data.SequenceEqual(submittedArray);
                }
            }
        }

        public class TestBase
        {
            protected IconProcessor Target { get; set; }
            protected Mock<IStorage> DestinationStorageMock { get; private set; }
            protected Mock<ITelemetryService> TelemetryServiceMock { get; set; }
            protected Mock<ILogger<IconProcessor>> LoggerMock { get; set; }

            public TestBase()
            {
                TelemetryServiceMock = new Mock<ITelemetryService>();
                LoggerMock = new Mock<ILogger<IconProcessor>>();

                Target = new IconProcessor(TelemetryServiceMock.Object, LoggerMock.Object);

                DestinationStorageMock = new Mock<IStorage>();
            }
        }
    }
}
