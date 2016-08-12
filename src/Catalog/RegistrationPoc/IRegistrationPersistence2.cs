using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog.Registration;

namespace CollectorSample.RegistrationPoc
{
    public interface IRegistrationPersistence2
    {
        Task<IDictionary<RegistrationEntryKey, RegistrationCatalogEntry2>> Load(CancellationToken cancellationToken);
        Task Save(IDictionary<RegistrationEntryKey, RegistrationCatalogEntry2> registration, CancellationToken cancellationToken);
    }
}