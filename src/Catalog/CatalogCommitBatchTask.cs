// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public sealed class CatalogCommitBatchTask
    {
        public CatalogCommitBatchTask(DateTime minCommitTimeStamp, string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullEmptyOrWhitespace, nameof(packageId));
            }

            MinCommitTimeStamp = minCommitTimeStamp;
            PackageId = packageId;
        }

        public DateTime MinCommitTimeStamp { get; }
        public string PackageId { get; }
        public Task Task { get; set; }

        public override int GetHashCode()
        {
            return PackageId.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var other = obj as CatalogCommitBatchTask;

            if (ReferenceEquals(other, null))
            {
                return false;
            }

            return GetHashCode() == other.GetHashCode();
        }
    }
}