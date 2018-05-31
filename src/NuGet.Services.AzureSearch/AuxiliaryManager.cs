using System;
using System.Collections.Generic;
using NuGet.Indexing;
using FrameworkLogger = Microsoft.Extensions.Logging.ILogger;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// This is a hacky port of <see cref="NuGetSearcherManager"/>
    /// </summary>
    public class AuxiliaryManager
    {
        public static readonly TimeSpan AuxiliaryDataRefreshRate = TimeSpan.FromHours(1);

        public virtual AuxiliaryFiles AuxiliaryFiles { get; private set; }
        public virtual DateTime? LastAuxiliaryDataLoadTime { get; private set; } = null;

        private readonly FrameworkLogger _logger;
        private readonly ILoader _loader;
        private readonly TimeSpan _auxiliaryDataRefreshRate;
        private readonly IDictionary<string, HashSet<string>> _owners = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly Downloads _downloads = new Downloads();

        public AuxiliaryManager(
            FrameworkLogger logger,
            ILoader loader,
            int auxiliaryDataRefreshRateSec)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // TODO: DI StorageLoader
            _loader = loader;

            AuxiliaryFiles = new AuxiliaryFiles(_loader);

            _auxiliaryDataRefreshRate = TimeSpan.FromSeconds(auxiliaryDataRefreshRateSec);

            ReloadAuxiliaryDataIfExpired();
        }

        public AuxiliaryData this[string packageId]
        {
            get
            {
                //_owners.TryGetValue(packageId, out var owners);

                return new AuxiliaryData
                {
                    Downloads = _downloads[packageId],
                    //Owners = owners,
                };
            }
        }

        private void ReloadAuxiliaryDataIfExpired()
        {
            //if (LastAuxiliaryDataLoadTime == null || LastAuxiliaryDataLoadTime < DateTime.UtcNow - _auxiliaryDataRefreshRate)
            //{
                IndexingUtils.Load(AuxiliaryFiles.Owners, _loader, _logger, _owners);
                _downloads.Load(AuxiliaryFiles.DownloadsV1, _loader, _logger);

                LastAuxiliaryDataLoadTime = DateTime.UtcNow;
                AuxiliaryFiles.UpdateLastModifiedTime();
            //}
        }
    }

    public class AuxiliaryData
    {
        public DownloadsByVersion Downloads { get; set; }
        public HashSet<string> Owners { get; set; }
    }
}
