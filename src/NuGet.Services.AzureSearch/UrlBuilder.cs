using System;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch
{
    public class UrlBuilder
    {
        private readonly string _registrationBase;

        public UrlBuilder(Uri registrationBase)
        {
            registrationBase = registrationBase ?? throw new ArgumentNullException(nameof(registrationBase));

            _registrationBase = registrationBase.AbsoluteUri.TrimEnd('/');
        }

        public string Registration(string packageId) => $"{_registrationBase}/{packageId.ToLowerInvariant()}/index.json";

        public string Registration(string packageId, NuGetVersion version) => $"{_registrationBase}/{packageId.ToLowerInvariant()}/{version.ToNormalizedString()}.json";
    }
}
