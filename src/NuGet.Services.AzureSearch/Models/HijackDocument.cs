// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// The different models for reading from and writing to the hijack index.
    /// </summary>
    public static class HijackDocument
    {
        /// <summary>
        /// All fields available in the hijack index. Used for reading the index and updating a document when
        /// <see cref="HijackDocumentChanges.UpdateMetadata"/> is <c>true</c>.
        /// </summary>
        [SerializePropertyNamesAsCamelCase]
        public class Full : BaseMetadataDocument, ILatest, IBaseMetadataDocument
        {
            /// <summary>
            /// This does not need to be filterable because the hijack document is only every used when both listed and
            /// unlisted versions can be returned. Essentially, this is when the gallery is making a V2 search query
            /// with the "ignoreFilter=true" query parameter set.
            /// </summary>
            public bool? Listed { get; set; }

            /// <summary>
            /// This field must be the lowercase since Azure Search filters are case sensitive.
            /// </summary>
            [IsFilterable]
            public string LowerPackageId { get; set; }

            public bool? IsLatestStableSemVer1 { get; set; }
            public bool? IsLatestSemVer1 { get; set; }
            public bool? IsLatestStableSemVer2 { get; set; }
            public bool? IsLatestSemVer2 { get; set; }
        }

        /// <summary>
        /// Used for updating a document when <see cref="HijackDocumentChanges.UpdateMetadata"/> is <c>false</c>
        /// and <see cref="HijackDocumentChanges.Delete"/> is <c>false</c>.
        /// </summary>
        [SerializePropertyNamesAsCamelCase]
        public class Latest : CommittedDocument, ILatest
        {
            public bool? IsLatestStableSemVer1 { get; set; }
            public bool? IsLatestSemVer1 { get; set; }
            public bool? IsLatestStableSemVer2 { get; set; }
            public bool? IsLatestSemVer2 { get; set; }
        }

        /// <summary>
        /// Allows index updating code to update the latest booleans.
        /// </summary>
        public interface ILatest : ICommittedDocument
        {
            bool? IsLatestStableSemVer1 { get; set; }
            bool? IsLatestSemVer1 { get; set; }
            bool? IsLatestStableSemVer2 { get; set; }
            bool? IsLatestSemVer2 { get; set; }
        }
    }
}
