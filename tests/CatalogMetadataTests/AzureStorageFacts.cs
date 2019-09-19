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
    public class AzureStorageFacts
    {
        private Uri _baseAddress = new Uri("https://test");

        [Theory]
        [InlineData(true, "SHA512Value1", true, "SHA512Value1", true)]
        [InlineData(true, "SHA512Value1", true, "SHA512Value2", false)]
        [InlineData(false, "SHA512Value1", true, "SHA512Value1", true)]
        [InlineData(true, "SHA512Value1", false, "SHA512Value1", false)]
        [InlineData(false, "SHA512Value1", false, "SHA512Value1", true)]
        public async void VerifyAreSynchronizedmethod(bool isSourceBlobExisted,
            string sourceBlobSHA512Value,
            bool isDestinationBlobExisted,
            string destinationBlobSHA512Value,
            bool expected)
        {
            // Arrange
            var directory = new Mock<ICloudBlobDirectory>();

            var client = new Mock<ICloudBlockBlobClient>();
            client.SetupProperty(x => x.DefaultRequestOptions);

            directory.Setup(x => x.ServiceClient).Returns(client.Object);

            var storage = new AzureStorage(directory.Object,
                _baseAddress,
                AzureStorage.DefaultMaxExecutionTime,
                AzureStorage.DefaultServerTimeout,
                false);

            var sourceBlob = GetMockedBlockBlob(isSourceBlobExisted, sourceBlobSHA512Value, new Uri("https://blockBlob1"));
            var destinationBlob = GetMockedBlockBlob(isDestinationBlobExisted, destinationBlobSHA512Value, new Uri("https://blockBlob2"));

            // Act
            var isSynchronized = await storage.AreSynchronized(sourceBlob.Object, destinationBlob.Object);

            // Assert
            if (isSourceBlobExisted && isDestinationBlobExisted)
            {
                sourceBlob.Verify();
                destinationBlob.Verify();
            }

            Assert.Equal(expected, isSynchronized);
        }

        private Mock<ICloudBlockBlob> GetMockedBlockBlob(bool isExisted, string hashValue, Uri blockBlobUri)
        {
            var mockBlob = new Mock<ICloudBlockBlob>();
            mockBlob.Setup(x => x.GetMetadataAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, string>()
                {
                    { "SHA512", hashValue }
                })
                .Verifiable();
            mockBlob.Setup(x => x.ExistsAsync(CancellationToken.None))
                .ReturnsAsync(isExisted)
                .Verifiable();
            mockBlob.Setup(x => x.Uri)
                .Returns(blockBlobUri)
                .Verifiable();

            return mockBlob;
        }
    }
}