// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.FeatureFlags;

namespace NuGet.Services.AzureSearch
{
    public class FeatureFlagService : IFeatureFlagService
    {
        private const string ValidationPrefix = "Search.";

        private readonly IFeatureFlagClient _featureFlagClient;

        public FeatureFlagService(IFeatureFlagClient featureFlagClient)
        {
            _featureFlagClient = featureFlagClient ?? throw new ArgumentNullException(nameof(featureFlagClient));
        }

        public bool IsPopularityTransferEnabled()
        {
            return _featureFlagClient.IsEnabled(ValidationPrefix + "PopularityTransfer", defaultValue: true);
        }
    }
}
