// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;

namespace NuGet.Services.AzureSearch.FunctionalTests
{
    public class SearchAvailabilityTests : NuGetSearchFunctionalTestBase
    {
        public SearchAvailabilityTests(CommonFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task TheSearchEndpointReturnsResults()
        {
            var results = await SearchAsync("");

            Assert.NotNull(results);
            Assert.True(results.Count > 1);
        }
    }
}
