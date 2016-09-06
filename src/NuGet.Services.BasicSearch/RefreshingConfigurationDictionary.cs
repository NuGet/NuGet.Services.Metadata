// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.KeyVault;
using System.Threading.Tasks;
using System;
using System.Configuration;

namespace NuGet.Services.BasicSearch
{
    public class RefreshingConfigurationDictionary : RefreshingArgumentsDictionary
    {
        // Constructor for testing.
        // Supply config values through unprocessedArguments.
        public RefreshingConfigurationDictionary(ISecretInjector secretInjector, IDictionary<string, string> unprocessedArguments) : base(secretInjector, CreateUnprocessedArgumentsDictionary(unprocessedArguments))
        {
        }

        public RefreshingConfigurationDictionary(ISecretInjector secretInjector) : base(secretInjector, CreateUnprocessedArgumentsDictionary())
        {
        }

        private static IDictionary<string, string> CreateUnprocessedArgumentsDictionary(IDictionary<string, string> unprocessedArguments = null)
        {
            unprocessedArguments = unprocessedArguments ?? new Dictionary<string, string>();

            string refreshArgsIntervalSec;
            var hasInterval = FindConfigValue(RefreshingArgumentsDictionary.RefreshArgsIntervalSec, out refreshArgsIntervalSec);
            if (hasInterval) unprocessedArguments.Add(RefreshingArgumentsDictionary.RefreshArgsIntervalSec, refreshArgsIntervalSec);

            return unprocessedArguments;
        }

        protected override async Task<string> Get(string key)
        {
            string value;
            if (!ContainsKey(key) && FindConfigValue(key, out value)) Add(key, value);
            return await base.Get(key);
        }

        private static bool FindConfigValue(string key, out string value)
        {
            // Get value from Cloud Services (if it throws, just ignore)
            try
            {
                if (SafeRoleEnvironment.IsAvailable)
                {
                    var cloudKey = key.Replace(':', '-'); // no ':' supported in cloud services
                    value = SafeRoleEnvironment.GetConfigurationSettingValue(cloudKey);
                    return true;
                }
            }
            catch
            {
            }

            // Get value from environment/appsettings
            value = Environment.GetEnvironmentVariable(key);
            value = value ?? ConfigurationManager.AppSettings[key];
            
            return value != null;
        }
    }
}