// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Indexing
{
    /// <summary>
    /// Provides names of configuration settings for Indexing.
    /// </summary>
    public static class IndexingSettings
    {
        public const string AuxiliaryDataRefreshRateSec = "Search.AuxiliaryDataRefreshRateSec";
        public const int AuxiliaryDataRefreshRateSecDefault = 60*60; // One hour.

        public const string IndexReloadRateSec = "Search.IndexReloadRateSec";
        public const int IndexReloadRateSecDefault = 60*60; // One hour.

        public const string DataContainer = "Search.DataContainer";
        public const string DataContainerDefault = "ng-search-data";

        public const string IndexContainer = "Search.IndexContainer";
        public const string IndexContainerDefault = "ng-search-index";

        public const string LocalDataDirectory = "Local.Data.Directory";
        public const string LocalLuceneDirectory = "Local.Lucene.Directory";

        public const string RegistrationBaseAddress = "Search.RegistrationBaseAddress";

        public const string StoragePrimary = "Storage.Primary";
    }
}
