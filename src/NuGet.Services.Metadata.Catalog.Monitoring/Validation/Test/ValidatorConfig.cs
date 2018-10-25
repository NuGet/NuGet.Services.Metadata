// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// The config passed to <see cref="IValidator"/>s.
    /// </summary>
    public class ValidatorConfig
    {
        public ValidatorConfig(string packageBaseAddress, bool requireSignature)
        {
            if (string.IsNullOrEmpty(packageBaseAddress))
            {
                throw new ArgumentException("Package base address is required", nameof(packageBaseAddress));
            }

            PackageBaseAddress = packageBaseAddress.TrimEnd('/');
            RequireSignature = requireSignature;
        }

        /// <summary>
        /// The base URL for the Package Content resource.
        /// See: https://docs.microsoft.com/en-us/nuget/api/package-base-address-resource
        /// </summary>
        public string PackageBaseAddress { get; }

        /// <summary>
        /// Whether signature validations are required.
        /// </summary>
        public bool RequireSignature { get; }
    }
}
