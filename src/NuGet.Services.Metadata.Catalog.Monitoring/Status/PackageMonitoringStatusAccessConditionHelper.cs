using Microsoft.WindowsAzure.Storage;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public static class PackageMonitoringStatusAccessConditionHelper
    {
        public static AccessCondition FromContent(StorageContent content)
        {
            var eTag = (content as StringStorageContentWithETag)?.ETag;
            if (eTag == null)
            {
                return AccessCondition.GenerateIfNotExistsCondition();
            }
            else
            {
                return AccessCondition.GenerateIfMatchCondition(eTag);
            }
        }

        public static AccessCondition FromExisting(PackageMonitoringStatus status)
        {
            return status?.AccessCondition ?? AccessCondition.GenerateIfNotExistsCondition();
        }

        public static AccessCondition FromUnknown()
        {
            return AccessCondition.GenerateEmptyCondition();
        }
    }
}
