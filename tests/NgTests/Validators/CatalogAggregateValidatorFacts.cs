// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Services.Metadata.Catalog.Monitoring.Validation.Test.Catalog;
using Xunit;

namespace NgTests.Validators
{
    public class CatalogAggregateValidatorFacts
    {
        [Fact]
        public async Task ValidatesSignature()
        {
            var feedToSource = new Mock<IDictionary<FeedType, SourceRepository>>();
            var config = ValidatorTestUtility.CreateValidatorConfig();
            var loggerFactory = Mock.Of<ILoggerFactory>();
            var factory = new ValidatorFactory(feedToSource.Object, config, loggerFactory);

            feedToSource.Setup(x => x[It.IsAny<FeedType>()]).Returns(new Mock<SourceRepository>().Object);

            var target = new CatalogAggregateValidator(factory);

            var result = await target.ValidateAsync(new ValidationContext());

            Assert.Equal(1, result.ValidationResults.Count());
        }
    }
}
