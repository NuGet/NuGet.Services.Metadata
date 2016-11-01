// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Indexing;
using NuGet.Services.KeyVault;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;

namespace NuGet.Services.BasicSearch
{
    public class SecretReaderFactory : ISecretReaderFactory
    {
        public const string VaultNameKey = "keyVault:VaultName";
        public const string ClientIdKey = "keyVault:ClientId";
        public const string StoreNameKey = "keyVault:StoreName";
        public const string StoreLocationKey = "keyVault:StoreLocation";
        public const string CertificateThumbprintKey = "keyVault:CertificateThumbprint";

        public async ISecretReader CreateSecretReader()
        {
            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            var vaultName = await arguments.GetOrDefault<string>(VaultNameKey);
            ISecretReader secretReader;

            // Is key vault configured?
            if (string.IsNullOrEmpty(vaultName))
            {
                secretReader = new EmptySecretReader();
            }
            else
            {
                var clientId = await arguments.GetOrThrow<string>(ClientIdKey);
                var certificateThumbprint = await arguments.GetOrThrow<string>(CertificateThumbprintKey);
                var storeName = await arguments.GetOrThrow<StoreName>(StoreNameKey);
                var storeLocation = await arguments.GetOrThrow<StoreLocation>(StoreLocationKey);

                // KeyVault is configured, but not all data is provided. Fail.
                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(certificateThumbprint))
                {
                    throw new ArgumentException("Not all KeyVault configuration provided. " +
                                                $"Parameter: {VaultNameKey} Value: {vaultName}, " +
                                                $"Parameter: {ClientIdKey} Value: {clientId}, " +
                                                $"Parameter: {CertificateThumbprintKey} Value: {certificateThumbprint}");
                }
               
                secretReader = new KeyVaultReader(new KeyVaultConfiguration(vaultName, clientId, certificateThumbprint, storeName, storeLocation, validateCertificate: true));
            }

            return secretReader;
        }

        public ISecretInjector CreateSecretInjector(ISecretReader secretReader)
        {
            return new SecretInjector(secretReader);
        }
    }
}