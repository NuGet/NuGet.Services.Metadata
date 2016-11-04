// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using NuGet.Services.KeyVault;

namespace NuGet.Services.BasicSearch.SecretReader
{
    public class SecretReaderFactory : ISecretReaderFactory
    {
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

            var vaultName = settings.GetOrDefault<string>(BasicSearchSettings.VaultNameKey).Result;
            ISecretReader secretReader;

            // Is key vault configured?
            if (string.IsNullOrEmpty(vaultName))
            {
                secretReader = new EmptySecretReader();
            }
            else
            {
                var clientId = settings.GetOrThrow<string>(BasicSearchSettings.ClientIdKey).Result;
                var certificateThumbprint = settings.GetOrThrow<string>(BasicSearchSettings.CertificateThumbprintKey).Result;
                var storeName = settings.GetOrDefault(BasicSearchSettings.StoreNameKey, StoreName.My).Result;
                var storeLocation = settings.GetOrDefault(BasicSearchSettings.StoreLocationKey, StoreLocation.LocalMachine).Result;
                var validateCertificate = settings.GetOrDefault<bool>(BasicSearchSettings.ValidateCertificate).Result;

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

        public ISecretInjector CreateSecretInjector(ISecretReader secretReader)
        {
            return new SecretInjector(secretReader);
        }
    }
}