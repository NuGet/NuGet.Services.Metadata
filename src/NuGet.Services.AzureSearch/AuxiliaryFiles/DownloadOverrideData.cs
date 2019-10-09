using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public class DownloadOverrideData
    {
        private readonly Dictionary<string, long> _downloadOverrides;

        public DownloadOverrideData(Dictionary<string, long> downloadOverrides)
        {
            _downloadOverrides = downloadOverrides ?? throw new ArgumentNullException(nameof(downloadOverrides));
        }

        public void Update(DownloadData downloadData)
        {
            foreach (var downloadOverride in _downloadOverrides)
            {
                var packageId = downloadOverride.Key;
                var overrideValue = downloadOverride.Value;

                // Only apply the downloads override if the package has at least one version with downloads.
                if (!downloadData.TryGetValue(packageId, out var versionData) || !versionData.Any())
                {
                    continue;
                }

                // Only apply the downloads override if the package has fewer total downloads.
                // In effect, this removes a package's manual boost once its total downloads exceed the override.
                if (versionData.Total >= overrideValue)
                {
                    continue;
                }

                // Apply the downloads override to the package's first version with downloads.
                var versions = versionData
                    .Keys
                    .Select(NuGetVersion.Parse)
                    .OrderBy(v => v)
                    .Select(v => v.OriginalVersion);
                var first = versions.First();
                var remaining = versions.Skip(1).ToList();

                versionData.SetDownloadCount(first, overrideValue);

                foreach (var version in remaining)
                {
                    versionData.SetDownloadCount(version, 0);
                }
            }
        }
    }
}
