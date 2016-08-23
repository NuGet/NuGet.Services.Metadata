// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Indexing
{
    public class PackageTypesResult
    {
        public PackageTypesResult(
            HashSet<string> knownPackageTypes,
            IDictionary<string, HashSet<string>> packagesWithPackageTypes,
            IDictionary<string, IDictionary<string, DynamicDocIdSet>> mappings)
        {
            KnownPackageTypes = knownPackageTypes;
            PackagesWithPackageTypes = packagesWithPackageTypes;
            Mappings = mappings;
        }

        public HashSet<string> KnownPackageTypes { get; }
        public IDictionary<string, HashSet<string>> PackagesWithPackageTypes { get; }
        public IDictionary<string, IDictionary<string, DynamicDocIdSet>> Mappings { get; }
    }
}
