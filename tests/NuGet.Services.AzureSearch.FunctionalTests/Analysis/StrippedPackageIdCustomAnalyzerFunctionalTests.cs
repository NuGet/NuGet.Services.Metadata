// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.AzureSearch.FunctionalTests.Support;
using Xunit;

namespace NuGet.Services.AzureSearch.FunctionalTests
{
    public class StrippedPackageIdCustomerAnalyzerFunctionalTests : AzureIndexFunctionalTests
    {
        private const string AnalyzerName = "nuget_stripped_package_id_analyzer";

        public StrippedPackageIdCustomerAnalyzerFunctionalTests(CommonFixture fixture)
            : base(fixture)
        {
        }

        [AnalysisTheory]
        [InlineData("Newtonsoft.Json", new[] { "newtonsoftjson" })]
        public async Task ProducesExpectedTokens(string input, string[] expectedTokens)
        {
            var actualTokens = new HashSet<string>(await AnalyzeAsync(AnalyzerName, input));

            foreach (var expectedToken in expectedTokens)
            {
                Assert.Contains(expectedToken, actualTokens);
            }

            Assert.Equal(expectedTokens.Length, actualTokens.Count);
        }
    }
}
