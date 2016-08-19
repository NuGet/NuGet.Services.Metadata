// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NgTests.Data;
using NgTests.Infrastructure;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.RawJsonRegistration;
using NuGet.Services.Metadata.Catalog.Registration;
using Xunit;

namespace NgTests
{
    public class RawJsonRegistrationCollectorConsistencyTests
    {
        [Fact]
        public async Task RawJsonRegistrationCollectorProducesTheSameOutputAsRegistrationCollector()
        {
            // Arrange
            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackagesAndDelete();

            var mockServer = new MockServerHttpClientHandler();
            mockServer.SetAction("/", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            await mockServer.AddStorage(catalogStorage);

            // Setup RegistrationCollector
            var registrationCollectorStorage = new MemoryStorage();
            var registrationCollectorStorageFactory = new TestStorageFactory(name => registrationCollectorStorage.WithName(name));
            
            var registrationCollectorFront = new DurableCursor(registrationCollectorStorage.ResolveUri("cursor.json"), registrationCollectorStorage, MemoryCursor.MinValue);
            var registrationCollectorBack = MemoryCursor.CreateMax();

            var registrationCollector = new RegistrationCollector(new Uri("http://tempuri.org/index.json"), registrationCollectorStorageFactory, () => mockServer)
            {
                ContentBaseAddress = new Uri("http://tempuri.org/packages")
            };

            RegistrationMakerCatalogItem.PackagePathProvider = new PackagesFolderPackagePathProvider();

            // Setup RawJsonRegistrationCollector
            var rawJsonRegistrationCollectorStorage = new MemoryStorage();
            var rawJsonRegistrationCollectorStorageFactory = new TestStorageFactory(name => rawJsonRegistrationCollectorStorage.WithName(name));

            var rawJsonRegistrationCollectorFront = new DurableCursor(rawJsonRegistrationCollectorStorage.ResolveUri("cursor.json"), rawJsonRegistrationCollectorStorage, MemoryCursor.MinValue);
            var rawJsonRegistrationCollectorBack = MemoryCursor.CreateMax();

            var rawJsonRegistrationCollector = new RawJsonRegistrationCollector(new Uri("http://tempuri.org/index.json"), rawJsonRegistrationCollectorStorageFactory, () => mockServer)
            {
                ContentBaseAddress = new Uri("http://tempuri.org/packages"),
                PackagePathProvider = new PackagesFolderPackagePathProvider(prefix: null)
            };
            
            // Act
            await registrationCollector.Run(registrationCollectorFront, registrationCollectorBack, CancellationToken.None);
            await rawJsonRegistrationCollector.Run(rawJsonRegistrationCollectorFront, rawJsonRegistrationCollectorBack, CancellationToken.None);

            // Compare output
            Assert.Equal(registrationCollectorStorage.Content.Count, rawJsonRegistrationCollectorStorage.Content.Count);

            foreach (var registrationCollectorStorageContent in registrationCollectorStorage.Content)
            {
                // Check if file exists on the other side
                var rawJsonRegistrationCollectorStorageContent = rawJsonRegistrationCollectorStorage.Content
                    .FirstOrDefault(pair => pair.Key.PathAndQuery == registrationCollectorStorageContent.Key.PathAndQuery);

                Assert.NotNull(rawJsonRegistrationCollectorStorageContent);

                // Check if content is the same
                JObjectAssert.AreEqual(
                    JObject.Parse(registrationCollectorStorageContent.Value.GetContentString()), 
                    JObject.Parse(rawJsonRegistrationCollectorStorageContent.Value.GetContentString()),
                    new[] { "commitId", "commitTimeStamp" },
                    registrationCollectorStorageContent.Key.PathAndQuery + " content is not equal");
            }
        }
    }
}