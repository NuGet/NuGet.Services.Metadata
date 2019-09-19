// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Collections.Generic;
using Moq;
using Xunit;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace CatalogMetadataTests
{
    public class AzureStorageFacts : BaseFacts
    {
        public AzureStorageFacts() : base()
        {
        }

        [Theory]
        [InlineData(true, true, "SHA512Value1", true, true, "SHA512Value1", true)]
        [InlineData(true, true, "SHA512Value1", true, true, "SHA512Value2", false)]
        [InlineData(false, false, null, true, true, "SHA512Value1", true)]
        [InlineData(true, true, "SHA512Value1", false, false, null, false)]
        [InlineData(false, false, null, false, false, null, true)]
        [InlineData(true, false, null, true, true, "SHA512Value1", false)]
        [InlineData(true, true, "SHA512Value1", true, false, null, false)]
        [InlineData(true, false, null, true, false, null, false)]
        public async void ValidateAreSynchronizedmethod(bool isSourceBlobExisted,
            bool hasSourceBlobSHA512Value,
            string sourceBlobSHA512Value,
            bool isDestinationBlobExisted,
            bool hasDestinationBlobSHA512Value,
            string destinationBlobSHA512Value,
            bool expected)
        {
            // Arrange
            var sourceBlob = GetMockedBlockBlob(isSourceBlobExisted, hasSourceBlobSHA512Value, sourceBlobSHA512Value, new Uri("https://blockBlob1"));
            var destinationBlob = GetMockedBlockBlob(isDestinationBlobExisted, hasDestinationBlobSHA512Value, destinationBlobSHA512Value, new Uri("https://blockBlob2"));

            // Act
            var isSynchronized = await _storage.AreSynchronized(sourceBlob.Object, destinationBlob.Object);

            // Assert
            sourceBlob.Verify(x => x.ExistsAsync(CancellationToken.None), Times.Once);
            destinationBlob.Verify(x => x.ExistsAsync(CancellationToken.None), Times.Once);

            if (isSourceBlobExisted && isDestinationBlobExisted)
            {
                sourceBlob.Verify(x => x.GetMetadataAsync(CancellationToken.None), Times.Once);
                destinationBlob.Verify(x => x.GetMetadataAsync(CancellationToken.None), Times.Once);

                if (hasSourceBlobSHA512Value && hasDestinationBlobSHA512Value)
                {
                    sourceBlob.Verify(x => x.Uri, Times.Once);
                    destinationBlob.Verify(x => x.Uri, Times.Once);
                }
            }

            Assert.Equal(expected, isSynchronized);
        }

        private Mock<ICloudBlockBlob> GetMockedBlockBlob(bool isExisted, bool hasSHA512Value, string SHA512Value, Uri blockBlobUri)
        {
            var mockBlob = new Mock<ICloudBlockBlob>();

            mockBlob.Setup(x => x.ExistsAsync(CancellationToken.None))
                .ReturnsAsync(isExisted);

            if (isExisted)
            {
                mockBlob.Setup(x => x.Uri).Returns(blockBlobUri);

                if (hasSHA512Value)
                {
                    mockBlob.Setup(x => x.GetMetadataAsync(CancellationToken.None))
                        .ReturnsAsync(new Dictionary<string, string>()
                        {
                            { "SHA512", SHA512Value }
                        });
                }
                else
                {
                    mockBlob.Setup(x => x.GetMetadataAsync(CancellationToken.None))
                        .ReturnsAsync(new Dictionary<string, string>());
                }
            }

            return mockBlob;
        }
    }

    public abstract class BaseFacts
    {
        protected readonly Uri _baseAddress = new Uri("https://test");
        protected readonly AzureStorage _storage;

        public BaseFacts()
        {
            var directory = new Mock<ICloudBlobDirectory>();

            var client = new Mock<ICloudBlockBlobClient>();
            client.SetupProperty(x => x.DefaultRequestOptions);

            directory.Setup(x => x.ServiceClient).Returns(client.Object);

            _storage = new AzureStorage(directory.Object,
                _baseAddress,
                AzureStorage.DefaultMaxExecutionTime,
                AzureStorage.DefaultServerTimeout,
                false);
        }
    }
}