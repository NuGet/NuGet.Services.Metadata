// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Indexing;
using NuGet.Services.KeyVault;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using NuGet.Services.BasicSearch.SecretReader;

namespace NuGet.Services.BasicSearch
{
    public class SecretReaderFactory : ISecretReaderFactory
    {
        public const string VaultNameKey = "keyVault:VaultName";
        public const string ClientIdKey = "keyVault:ClientId";
        public const string StoreNameKey = "keyVault:StoreName";
        public const string StoreLocationKey = "keyVault:StoreLocation";
        public const string CertificateThumbprintKey = "keyVault:CertificateThumbprint";

        public ISecretReader CreateSecretReader()
        {
            // NOTE: In this method we are using ".Result" on the settings calls.
            // You should NEVER do this!
            // We can do it here because this code executes during startup, when it is not a problem.
            var settings = new EnvironmentSettingsProvider(CreateSecretInjector(new EmptySecretReader()));

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var vaultName = settings.GetOrDefault<string>(VaultNameKey).Result;
            ISecretReader secretReader;

            // Is key vault configured?
            if (string.IsNullOrEmpty(vaultName))
            {
                secretReader = new EmptySecretReader();
            }
            else
            {
                var clientId = settings.GetOrThrow<string>(ClientIdKey).Result;
                var certificateThumbprint = settings.GetOrThrow<string>(CertificateThumbprintKey).Result;
                var storeName = settings.GetOrThrow<StoreName>(StoreNameKey).Result;
                var storeLocation = settings.GetOrThrow<StoreLocation>(StoreLocationKey).Result;

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