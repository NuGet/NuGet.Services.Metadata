// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public class PackagesFolderPackagePathProvider : IPackagePathProvider
    {
        private readonly string _prefix;

        public PackagesFolderPackagePathProvider(string prefix = "packages")
        {
            _prefix = prefix;
        }

        public string GetPackagePath(string id, string version)
        {
            if (!string.IsNullOrEmpty(_prefix))
            {
                return $"{_prefix}/{id.ToLowerInvariant()}.{version.ToLowerInvariant()}.nupkg";
            }

            return $"{id.ToLowerInvariant()}.{version.ToLowerInvariant()}.nupkg";
        }
    }
}