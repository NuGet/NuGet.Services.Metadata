// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.BasicSearch
{
    /// <summary>
    /// Provides names of configuration settings for BasicSearch.
    /// </summary>
    public static class BasicSearchSettings
    {
        public const string ApplicationInsightsInstrumentationKey = "serilog:ApplicationInsightsInstrumentationKey";

        public const string SearchRefreshSec = "Search.IndexRefresh";
        public const int SearchRefreshSecDefault = 300;

        // KeyVault
        public const string VaultNameKey = "keyVault:VaultName";
        public const string ClientIdKey = "keyVault:ClientId";
        public const string StoreNameKey = "keyVault:StoreName";
        public const string StoreLocationKey = "keyVault:StoreLocation";
        public const string CertificateThumbprintKey = "keyVault:CertificateThumbprint";
        public const string ValidateCertificate = "keyVault:ValidateCertificate";
    }
}