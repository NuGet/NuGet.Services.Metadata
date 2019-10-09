using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGet.Indexing;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public static class DownloadOverrides
    {
        private static readonly JsonSerializer Serializer = new JsonSerializer();

        public static DownloadOverrideData Load(string fileName, ILoader loader, ILogger logger)
        {
            try
            {
                using (var reader = loader.GetReader(fileName))
                {
                    var downloadOverrides = Serializer.Deserialize<Dictionary<string, long>>(reader);

                    return new DownloadOverrideData(downloadOverrides);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(0, ex, "Unable to load download overrides {FileName} due to exception", fileName);
                throw;
            }
        }
    }
}
