// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NuGet.Services.KeyVault;

namespace NuGet.Services.BasicSearch.Configuration
{
    public class SecretReaderFactory : ISecretReaderFactory
    {
        private async Task<ISecretReader> CreateSecretReaderAsync()
        {
            var settings = new EnvironmentSettingsProvider(CreateSecretInjector(new EmptySecretReader()));

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var vaultName = await settings.GetOrDefault<string>(BasicSearchSettings.VaultNameKey);
            ISecretReader secretReader;

            // Is key vault configured?
            if (string.IsNullOrEmpty(vaultName))
            {
                secretReader = new EmptySecretReader();
            }
            else
            {
                var clientId = await settings.GetOrThrow<string>(BasicSearchSettings.ClientIdKey);
                var certificateThumbprint = await settings.GetOrThrow<string>(BasicSearchSettings.CertificateThumbprintKey);
                var storeName = await settings.GetOrDefault(BasicSearchSettings.StoreNameKey, StoreName.My);
                var storeLocation = await settings.GetOrDefault(BasicSearchSettings.StoreLocationKey, StoreLocation.LocalMachine);
                var validateCertificate = await settings.GetOrDefault<bool>(BasicSearchSettings.ValidateCertificate);

                // KeyVault is configured, but not all data is provided. Fail.
                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(certificateThumbprint))
                {
                    throw new ArgumentException("Not all KeyVault configuration provided. " +
                                                $"Parameter: {BasicSearchSettings.VaultNameKey} Value: {vaultName}, " +
                                                $"Parameter: {BasicSearchSettings.ClientIdKey} Value: {clientId}, " +
                                                $"Parameter: {BasicSearchSettings.CertificateThumbprintKey} Value: {certificateThumbprint}");
                }

                secretReader =
                    new KeyVaultReader(new KeyVaultConfiguration(vaultName, clientId, certificateThumbprint, storeName,
                        storeLocation, validateCertificate));
            }

            return secretReader;
        }

        public ISecretReader CreateSecretReader()
        {
            // NOTE: In this method we are using ".Result" on a function that makes KeyVault calls.
            // You should NEVER do this!
            // We can do it here because this code executes during startup, when it is not a problem.
            return CreateSecretReaderAsync().Result;
        }

        public ISecretInjector CreateSecretInjector(ISecretReader secretReader)
        {
            return new SecretInjector(secretReader);
        }
    }
}