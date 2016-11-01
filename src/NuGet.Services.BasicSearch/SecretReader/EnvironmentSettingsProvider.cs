// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;
using NuGet.Services.Configuration;
using NuGet.Services.KeyVault;

namespace NuGet.Services.BasicSearch.SecretReader
{
    public class EnvironmentSettingsProvider : IConfigurationProvider
    {
        private readonly ISecretInjector _secretInjector;

        private IDictionary<string, string> _unprocessedSettings;
        private IDictionary<string, string> _cachedSettings = new Dictionary<string, string>();
        
        public EnvironmentSettingsProvider(ISecretInjector secretInjector, IDictionary<string, string> unprocessedSettings = null)
        {
            _secretInjector = secretInjector;

            _unprocessedSettings = unprocessedSettings ?? new Dictionary<string, string>();
            // Initially cache all arguments so that GetOrThrowSync and GetOrDefaultSync will not be called before the argument is cached.
            Task.Run(async () => await CacheAllArguments()).Wait();
        }

        private async Task CacheAllArguments()
        {
            foreach (var key in _unprocessedSettings.Keys)
            {
                _cachedSettings[key] = await _secretInjector.InjectAsync(key);
            }
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

        private async Task<string> Get(string key)
        {
            return await _secretInjector.InjectAsync(GetEnvironmentSettingValue(key));
        }

        public async Task<T> GetOrThrow<T>(string key)
        {
            return ConfigurationUtility.ConvertFromString<T>(await Get(key));
        }

        public Task<T> GetOrDefault<T>(string key, T defaultValue = default(T))
        {
            throw new NotImplementedException();
        }

        public T GetOrThrowSync<T>(string key)
        {
            throw new NotImplementedException();
        }

        public T GetOrDefaultSync<T>(string key, T defaultValue = default(T))
        {
            throw new NotImplementedException();
        }
    }
}