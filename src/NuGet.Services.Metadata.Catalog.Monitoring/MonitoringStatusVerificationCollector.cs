using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Storage;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class MonitoringStatusVerificationCollector : SortingIdVersionCollector
    {
        private readonly IPackageMonitoringStatusService _statusService;
        private readonly IStorageQueue<PackageValidatorContext> _queue;

        public MonitoringStatusVerificationCollector(
            IPackageMonitoringStatusService statusService,
            IStorageQueue<PackageValidatorContext> queue,
            Uri index,
            Func<HttpMessageHandler> handlerFunc = null)
            : base(index, handlerFunc)
        {
            _statusService = statusService;
            _queue = queue;
        }

        protected override async Task ProcessSortedBatch(CollectorHttpClient client, KeyValuePair<FeedPackageIdentity, IList<JObject>> sortedBatch, JToken context, CancellationToken cancellationToken)
        {
            var packageId = sortedBatch.Key.Id;
            var packageVersion = sortedBatch.Key.Version;
            var feedPackage = new FeedPackageIdentity(packageId, packageVersion);

            var existingStatus = await _statusService.GetAsync(feedPackage, cancellationToken);
            if (existingStatus == null)
            {
                var catalogEntries = sortedBatch.Value.Select(c => new CatalogIndexEntry(c));

                await _queue.Add(
                    new PackageValidatorContext(feedPackage, catalogEntries),
                    cancellationToken);
            }
        }
    }
}