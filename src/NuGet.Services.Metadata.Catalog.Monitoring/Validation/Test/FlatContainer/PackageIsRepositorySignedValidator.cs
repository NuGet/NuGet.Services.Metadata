// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Packaging;
using NuGet.Packaging.Signing;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Validates that the package is repository signed.
    /// </summary>
    public class PackageIsRepositorySignedValidator : FlatContainerValidator
    {
        public PackageIsRepositorySignedValidator(
            IDictionary<FeedType, SourceRepository> feedToSource,
            ValidatorConfig config,
            ILogger<PackageIsRepositorySignedValidator> logger)
            : base(feedToSource, config, logger)
        {
        }

        protected async override Task RunInternalAsync(ValidationContext context)
        {
            var signature = await GetPrimarySignatureOrNullAsync(context);
            var validationException = ValidateSignature(context, signature);

            if (validationException != null)
            {
                Logger.LogWarning(
                    "Package {PackageId} {Package) has an invalid repository signature. {Reason}: {Message}",
                    context.Package.Id,
                    context.Package.Version,
                    validationException.Reason,
                    validationException.Message);

                // Only report the validation exception if the "RequireSignature" config is enabled.
                if (Config.RequireSignature)
                {
                    throw validationException;
                }
            }
        }

        private async Task<PrimarySignature> GetPrimarySignatureOrNullAsync(ValidationContext context)
        {
            var downloader = new PackageDownloader(context.Client, Logger);
            var uri = GetV3PackageUri(context);

            using (var packageStream = await downloader.DownloadAsync(uri, context.CancellationToken))
            {
                if (packageStream == null)
                {
                    throw new InvalidOperationException($"Package {context.Package.Id} {context.Package.Version} couldn't be downloaded at {uri}");
                }

                using (var package = new PackageArchiveReader(packageStream))
                {
                    return await package.GetPrimarySignatureAsync(context.CancellationToken);
                }
            }
        }

        /// <summary>
        /// Validate the package's signature.
        /// </summary>
        /// <param name="context">The package's validation context.</param>
        /// <param name="signature">The package's signature.</param>
        /// <returns>Null if the signature is valid, otherwise an exception that can be thrown.</returns>
        private MissingRepositorySignatureException ValidateSignature(ValidationContext context, PrimarySignature signature)
        {
            if (signature == null)
            {
                return new MissingRepositorySignatureException(
                    $"Package {context.Package.Id} {context.Package.Version} is unsigned",
                    MissingRepositorySignatureReason.Unsigned);
            }

            // The repository signature can be the primary signature or the author signature's countersignature.
            IRepositorySignature repositorySignature = null;

            switch (signature.Type)
            {
                case SignatureType.Repository:
                    repositorySignature = (RepositoryPrimarySignature)signature;
                    break;

                case SignatureType.Author:
                    repositorySignature = RepositoryCountersignature.GetRepositoryCountersignature(signature);

                    if (repositorySignature == null)
                    {
                        return new MissingRepositorySignatureException(
                            $"Package {context.Package.Id} {context.Package.Version} is author signed but not repository signed",
                            MissingRepositorySignatureReason.AuthorSignedNoRepositoryCountersignature);
                    }

                    break;

                default:
                case SignatureType.Unknown:
                    return new MissingRepositorySignatureException(
                        $"Package {context.Package.Id} {context.Package.Version} has an unknown signature type '{signature.Type}'",
                        MissingRepositorySignatureReason.UnknownSignature);
            }

            Logger.LogInformation(
                "Package {PackageId} {PackageVersion} has a repository signature with service index {ServiceIndex} and owners {Owners}",
                context.Package.Id,
                context.Package.Version,
                repositorySignature.V3ServiceIndexUrl,
                repositorySignature.PackageOwners);

            return null;
        }
    }
}