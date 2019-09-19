// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Icons;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using Xunit;

namespace CatalogTests.Icons
{
    public class CatalogLeafDataProcessorFacts
    {
        public class TheProcessPackageDeleteLeafAsyncMethod : TestBase
        {
            [Theory]
            [InlineData("Package", "1.2.3-Preview", "package/1.2.3-preview/icon")]
            [InlineData("PpPpPPpP", "3.2.1+metaData", "pppppppp/3.2.1/icon")]
            [InlineData("Package", "01.2.03.4", "package/1.2.3.4/icon")]
            public async Task CallsDeleteIconProperly(string packageId, string packageVersion, string expectedPath)
            {
                var leaf = new CatalogCommitItem(
                    new Uri("https://nuget.test/something"),
                    "somecommitid",
                    DateTime.UtcNow,
                    new string[0],
                    new Uri[0],
                    new PackageIdentity(packageId, new NuGetVersion(packageVersion)));

                await Target.ProcessPackageDeleteLeafAsync(DestinationStorageMock.Object, leaf, CancellationToken.None);

                IconProcessorMock
                    .Verify(ip => ip.DeleteIcon(DestinationStorageMock.Object, expectedPath, CancellationToken.None, It.IsAny<string>(), It.IsAny<string>()));
            }
        }

        public class TheProcessPackageDetailsLeafAsyncMethod : TestBase
        {
            [Theory]
            [InlineData("Package", "1.2.3-Preview", false, "package/1.2.3-preview/icon")]
            [InlineData("PpPpPPpP", "3.2.1+metaData", false, "pppppppp/3.2.1/icon")]
            [InlineData("Package", "01.2.03.4", false, "package/1.2.3.4/icon")]
            [InlineData("Package", "1.2.3-Preview", true, "package/1.2.3-preview/icon")]
            [InlineData("PpPpPPpP", "3.2.1+metaData", true, "pppppppp/3.2.1/icon")]
            [InlineData("Package", "01.2.03.4", true, "package/1.2.3.4/icon")]
            public async Task CallsCopyEmbeddedIconFromPackageProperly(string packageId, string packageVersion, bool hasIconUrl, string expectedPath)
            {
                var leaf = new CatalogCommitItem(
                    new Uri("https://nuget.test/something"),
                    "somecommitid",
                    DateTime.UtcNow,
                    new string[0],
                    new Uri[0],
                    new PackageIdentity(packageId, new NuGetVersion(packageVersion)));

                const string iconFilename = "iconFilename";

                await Target.ProcessPackageDetailsLeafAsync(
                    DestinationStorageMock.Object,
                    leaf,
                    hasIconUrl ? "https://icon/url" : null,
                    iconFilename,
                    CancellationToken.None);

                IconProcessorMock
                    .Verify(
                        ip => ip.CopyEmbeddedIconFromPackage(
                            It.IsAny<Stream>(),
                            iconFilename,
                            DestinationStorageMock.Object,
                            expectedPath,
                            CancellationToken.None,
                            It.Is<string>(p => leaf.PackageIdentity.Id.Equals(p, StringComparison.OrdinalIgnoreCase)),
                            It.Is<string>(v => leaf.PackageIdentity.Version.ToNormalizedString().Equals(v, StringComparison.OrdinalIgnoreCase))),
                        Times.Once);
            }

            [Theory]
            [InlineData("Package", "1.2.3-Preview", "package/1.2.3-preview/icon")]
            [InlineData("PpPpPPpP", "3.2.1+metaData", "pppppppp/3.2.1/icon")]
            [InlineData("Package", "01.2.03.4", "package/1.2.3.4/icon")]
            public async Task CopiesIconFromExternalLocation(string packageId, string packageVersion, string expectedPath)
            {
                var leaf = new CatalogCommitItem(
                    new Uri("https://nuget.test/something"),
                    "somecommitid",
                    DateTime.UtcNow,
                    new string[0],
                    new Uri[0],
                    new PackageIdentity(packageId, new NuGetVersion(packageVersion)));

                const string iconUrlString = "https://icon/url";

                ExternalIconContentProviderMock
                    .Setup(cp => cp.TryGetResponseAsync(
                        It.Is<Uri>(u => u.AbsoluteUri == iconUrlString),
                        CancellationToken.None))
                    .ReturnsAsync(
                        TryGetResponseResult.Success(
                            new HttpResponseMessage(HttpStatusCode.OK)
                            {
                                Content = ExternalIconContentMock.Object
                            }));

                await Target.ProcessPackageDetailsLeafAsync(
                    DestinationStorageMock.Object,
                    leaf,
                    iconUrlString,
                    null,
                    CancellationToken.None);

                IconProcessorMock
                    .Verify(
                        ip => ip.CopyIconFromExternalSource(
                            ExternalIconStream,
                            DestinationStorageMock.Object,
                            expectedPath,
                            CancellationToken.None,
                            packageId,
                            leaf.PackageIdentity.Version.ToNormalizedString()),
                        Times.Once);
            }

            [Fact]
            public async Task RetriesExternalLocationFailures()
            {
                var leaf = new CatalogCommitItem(
                    new Uri("https://nuget.test/something"),
                    "somecommitid",
                    DateTime.UtcNow,
                    new string[0],
                    new Uri[0],
                    new PackageIdentity("theid", new NuGetVersion("3.4.2")));

                const string iconUrlString = "https://icon/url";

                ExternalIconContentProviderMock
                    .SetupSequence(cp => cp.TryGetResponseAsync(
                        It.Is<Uri>(u => u.AbsoluteUri == iconUrlString),
                        CancellationToken.None))
                    .ReturnsAsync(TryGetResponseResult.FailCanRetry())
                    .ReturnsAsync(
                        TryGetResponseResult.Success(
                            new HttpResponseMessage(HttpStatusCode.OK)
                            {
                                Content = ExternalIconContentMock.Object
                            }));

                await Target.ProcessPackageDetailsLeafAsync(
                    DestinationStorageMock.Object,
                    leaf,
                    iconUrlString,
                    null,
                    CancellationToken.None);

                ExternalIconContentProviderMock
                    .Verify(
                        cp => cp.TryGetResponseAsync(
                            It.Is<Uri>(u => u.AbsoluteUri == iconUrlString),
                            CancellationToken.None),
                        Times.AtLeast(2));

                IconProcessorMock
                    .Verify(
                        ip => ip.CopyIconFromExternalSource(
                            ExternalIconStream,
                            DestinationStorageMock.Object,
                            "theid/3.4.2/icon",
                            CancellationToken.None,
                            "theid",
                            leaf.PackageIdentity.Version.ToNormalizedString()),
                        Times.Once);
            }

            [Fact]
            public async Task DoesNotRetrySeriousFailures()
            {
                var leaf = new CatalogCommitItem(
                    new Uri("https://nuget.test/something"),
                    "somecommitid",
                    DateTime.UtcNow,
                    new string[0],
                    new Uri[0],
                    new PackageIdentity("theid", new NuGetVersion("3.4.2")));

                const string iconUrlString = "https://icon/url";

                ExternalIconContentProviderMock
                    .SetupSequence(cp => cp.TryGetResponseAsync(
                        It.Is<Uri>(u => u.AbsoluteUri == iconUrlString),
                        CancellationToken.None))
                    .ReturnsAsync(TryGetResponseResult.FailCannotRetry())
                    .ReturnsAsync(
                        TryGetResponseResult.Success(
                            new HttpResponseMessage(HttpStatusCode.OK)
                            {
                                Content = ExternalIconContentMock.Object
                            }));

                await Target.ProcessPackageDetailsLeafAsync(
                    DestinationStorageMock.Object,
                    leaf,
                    iconUrlString,
                    null,
                    CancellationToken.None);

                ExternalIconContentProviderMock
                    .Verify(
                        cp => cp.TryGetResponseAsync(
                            It.Is<Uri>(u => u.AbsoluteUri == iconUrlString),
                            CancellationToken.None),
                        Times.AtMostOnce());

                IconProcessorMock
                    .Verify(
                        ip => ip.CopyIconFromExternalSource(
                            It.IsAny<Stream>(),
                            It.IsAny<IStorage>(),
                            It.IsAny<string>(),
                            It.IsAny<CancellationToken>(),
                            It.IsAny<string>(),
                            It.IsAny<string>()),
                        Times.Never);
            }

            private Mock<ICloudBlockBlob> PackageBlobRerenceMock { get; set; }
            private Stream ExternalIconStream { get; set; }
            private Mock<HttpContent> ExternalIconContentMock { get; set; }

            public TheProcessPackageDetailsLeafAsyncMethod()
            {
                PackageBlobRerenceMock = new Mock<ICloudBlockBlob>();

                PackageStorageMock
                    .Setup(ps => ps.GetCloudBlockBlobReferenceAsync(It.IsAny<Uri>()))
                    .ReturnsAsync(PackageBlobRerenceMock.Object);

                PackageBlobRerenceMock
                    .Setup(pbr => pbr.GetStreamAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Mock.Of<Stream>());

                ExternalIconContentMock = new Mock<HttpContent>();
                ExternalIconContentMock
                    .Protected()
                    .Setup<Task<Stream>>("CreateContentReadStreamAsync")
                    .ReturnsAsync(() => ExternalIconStream);

                ExternalIconStream = new MemoryStream();
            }
        }

        public class TestBase
        {
            protected Mock<IAzureStorage> PackageStorageMock { get; set; }
            protected Mock<IIconProcessor> IconProcessorMock { get; set; }
            protected Mock<IExternalIconContentProvider> ExternalIconContentProviderMock { get; set; }
            protected Mock<IIconCopyResultCache> IconCopyResultCacheMock { get; set; }
            protected Mock<ITelemetryService> TelemetryServiceMock { get; set; }
            protected Mock<ILogger<CatalogLeafDataProcessor>> LoggerMock { get; set; }
            protected Mock<Storage> DestinationStorageMock { get; set; }
            protected CatalogLeafDataProcessor Target { get; set; }

            public TestBase()
            {
                PackageStorageMock = new Mock<IAzureStorage>();
                IconProcessorMock = new Mock<IIconProcessor>();
                ExternalIconContentProviderMock = new Mock<IExternalIconContentProvider>();
                IconCopyResultCacheMock = new Mock<IIconCopyResultCache>();
                TelemetryServiceMock = new Mock<ITelemetryService>();
                LoggerMock = new Mock<ILogger<CatalogLeafDataProcessor>>();
                DestinationStorageMock = new Mock<Storage>(new Uri("https://base/storage"));

                TelemetryServiceMock
                    .Setup(ts => ts.TrackEmbeddedIconProcessingDuration(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(Mock.Of<IDisposable>());
                TelemetryServiceMock
                    .Setup(ts => ts.TrackExternalIconProcessingDuration(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(Mock.Of<IDisposable>());

                Target = new CatalogLeafDataProcessor(
                    PackageStorageMock.Object,
                    IconProcessorMock.Object,
                    ExternalIconContentProviderMock.Object,
                    IconCopyResultCacheMock.Object,
                    TelemetryServiceMock.Object,
                    LoggerMock.Object);
            }
        }

        private class TestStorage : Storage
        {
            private Func<Uri, IStorage, Uri, IReadOnlyDictionary<string, string>, CancellationToken, Task> _onCopy;
            private Func<Uri, DeleteRequestOptions, CancellationToken, Task> _onDelete;
            private Func<Uri, CancellationToken, Task<StorageContent>> _onLoad;

            public TestStorage()
                : base(new Uri("https://base/container"))
            {

            }

            public override bool Exists(string fileName)
            {
                throw new NotImplementedException();
            }

            public override Task<IEnumerable<StorageListItem>> ListAsync(CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public void OnCopy(Func<Uri, IStorage, Uri, IReadOnlyDictionary<string, string>, CancellationToken, Task> callback)
            {
                _onCopy = callback;
            }

            protected override async Task OnCopyAsync(Uri sourceUri, IStorage destinationStorage, Uri destinationUri, IReadOnlyDictionary<string, string> destinationProperties, CancellationToken cancellationToken)
            {
                if (_onCopy != null)
                {
                    await _onCopy(sourceUri, destinationStorage, destinationUri, destinationProperties, cancellationToken);
                }
            }

            public void OnDelete(Func<Uri, DeleteRequestOptions, CancellationToken, Task> callback)
            {
                _onDelete = callback;
            }

            protected override async Task OnDeleteAsync(Uri resourceUri, DeleteRequestOptions deleteRequestOptions, CancellationToken cancellationToken)
            {
                if (_onDelete != null)
                {
                    await _onDelete(resourceUri, deleteRequestOptions, cancellationToken);
                }
            }

            public void onLoad(Func<Uri, CancellationToken, Task<StorageContent>> callback)
            {
                _onLoad = callback;
            }

            protected override async Task<StorageContent> OnLoadAsync(Uri resourceUri, CancellationToken cancellationToken)
            {
                if (_onLoad != null)
                {
                    return await _onLoad(resourceUri, cancellationToken);
                }

                return null;
            }

            public void OnSave(Func<Uri, StorageContent, CancellationToken, Task> callback)
            {

            }

            protected override Task OnSaveAsync(Uri resourceUri, StorageContent content, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }
    }
}
