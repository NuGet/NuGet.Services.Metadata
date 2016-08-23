// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Indexing;
using NuGet.IndexingTests.TestSupport;
using Xunit;

namespace NuGet.IndexingTests
{
    public class PackageTypesAnalyzerTests
    {
        [Theory]
        [MemberData(nameof(TokenizerLowercasesInputData))]
        public void TokenizerLowercasesAndSplitsInput(string text, TokenAttributes[] expected)
        {
            // arrange, act
            var actual = new PackageTypesAnalyzer().Tokenize(text);

            // assert
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> TokenizerLowercasesInputData
        {
            get
            {
                // simple sentence
                yield return new object[]
                {
                    "Split sentence.",
                    new[]
                    {
                        new TokenAttributes("split sentence.", 0, 15)
                    }
                };

                // lower case 
                yield return new object[]
                {
                    "D",
                    new[]
                    {
                        new TokenAttributes("d", 0, 1)
                    }
                };

                // Ignores special characters
                yield return new object[]
                {
                    "This! is@ a# sentence$ full% of^ special& characters* ().",
                    new[]
                    {
                        new TokenAttributes("this! is@ a# sentence$ full% of^ special& characters* ().", 0, 57)
                    }
                };
            }
        }
    }
}
