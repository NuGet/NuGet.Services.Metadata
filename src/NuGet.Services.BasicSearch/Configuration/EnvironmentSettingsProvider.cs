// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;
using NuGet.Services.KeyVault;
using SettingsProvider = NuGet.Services.Configuration.SettingsProvider;

namespace NuGet.Services.BasicSearch.Configuration
{
    public class EnvironmentSettingsProvider : SettingsProvider
    {
        private readonly ISecretInjector _secretInjector;
        
        public EnvironmentSettingsProvider(ISecretInjector secretInjector)
        {
            _secretInjector = secretInjector;
        }

        private static string GetEnvironmentSettingValue(string key)
        {
            // Get value from Cloud Services (if it throws, just ignore)
            try
            {
                if (SafeRoleEnvironment.IsAvailable)
                {
                    var cloudKey = key.Replace(':', '-'); // no ':' supported in cloud services
                    return SafeRoleEnvironment.GetConfigurationSettingValue(cloudKey);
                }
            }
            catch
            {
            }

            // Get value from environment/appsettings
            var value = Environment.GetEnvironmentVariable(key) ?? ConfigurationManager.AppSettings[key];

            if (value != null)
            {
                return value;
            }

            throw new KeyNotFoundException($"{key} was not found in the environment settings.");
        }

        protected override Task<string> Get(string key)
        {
            var setting = GetEnvironmentSettingValue(key);

            if (string.IsNullOrEmpty(setting))
            {
                throw new ArgumentNullException($"Value for {key} is null or empty!");
            }

            return _secretInjector.InjectAsync(setting);
        }
    }
}