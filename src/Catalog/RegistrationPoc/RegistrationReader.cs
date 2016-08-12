using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog.Registration;

namespace CollectorSample.RegistrationPoc
{
    public class RegistrationReader
        : IDisposable
    {
        private bool _open;

        public RegistrationReader(IStorage storage)
        {
            Storage = storage;
            
            _open = true;

            RootUri = Storage.ResolveUri("index.json");
        }

        public void Dispose()
        {
            _open = false;
        }

        public IStorage Storage { get; }
        public Uri RootUri { get; }

        public async Task<IDictionary<RegistrationEntryKey, RegistrationCatalogEntry2>> Load(CancellationToken cancellationToken)
        {
            var graph = await LoadRegistration(Storage, RootUri, cancellationToken);

            var resources = GetResources(graph);

            return resources;
        }

        private static IDictionary<RegistrationEntryKey, RegistrationCatalogEntry2> GetResources(JArray registrationEntries)
        {
            var resources = new Dictionary<RegistrationEntryKey, RegistrationCatalogEntry2>();

            if (registrationEntries != null && registrationEntries.Count > 0)
            {
                foreach (var registrationEntry in registrationEntries)
                {
                    var resourceUri = registrationEntry[PropertyNames.SchemaId].ToString().ToLowerInvariant();

                    var id = registrationEntry[PropertyNames.CatalogEntry][PropertyNames.Id].ToString().ToLowerInvariant();
                    var version = registrationEntry[PropertyNames.CatalogEntry][PropertyNames.Version].ToString().ToLowerInvariant();

                    resources.Add(
                        new RegistrationEntryKey(new RegistrationKey(id), version),
                        new RegistrationCatalogEntry2(id, version, resourceUri, (JObject)registrationEntry[PropertyNames.CatalogEntry], true));
                }
            }

            return resources;
        }

        private static async Task<JArray> LoadRegistration(IStorage storage, Uri resourceUri, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("RegistrationReader.LoadRegistration: resourceUri = {0}", resourceUri);

            var result = new JArray();

            var json = await storage.LoadString(resourceUri, cancellationToken);
            if (string.IsNullOrEmpty(json))
            {
                return result;
            }

            var graph = JObject.Parse(json);

            var tasks = new List<Task<JArray>>();

            // Load items
            var items = graph[PropertyNames.Items].Children<JObject>();
            foreach (var item in items)
            {
                if (CompareSchemaType(item, Schema.DataTypes.CatalogPage, prefix: Schema.Prefixes.Catalog))
                {
                    tasks.Add(LoadRegistrationPage(storage, new Uri(item[PropertyNames.SchemaId].ToString()), item, cancellationToken));
                }
            }

            // Load enqueued pages
            await Task.WhenAll(tasks.ToArray());
            foreach (var task in tasks)
            {
                var itemsOnPage = task.Result;
                if (itemsOnPage != null && itemsOnPage.Count > 0)
                {
                    foreach (var item in itemsOnPage)
                    {
                        result.Add((JObject)item);
                    }
                }
            }

            return result;
        }

        static async Task<JArray> LoadRegistrationPage(IStorage storage, Uri pageUri, JObject page, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("RegistrationReader.LoadRegistrationPage: pageUri = {0}", pageUri);

            var result = new JArray();

            JObject graph;
            if (page[PropertyNames.Items] != null)
            {
                // Load embedded page (inline packages on small registration)
                graph = page;
            }
            else
            {
                // Load remote page (typically on large registration)
                var json = await storage.LoadString(pageUri, cancellationToken);
                if (string.IsNullOrEmpty(json))
                {
                    return result;
                }

                graph = JObject.Parse(json);
            }

            // Load items
            var items = graph[PropertyNames.Items].Children<JObject>();
            foreach (var item in items)
            {
                if (CompareSchemaType(item, Schema.DataTypes.Package, prefix: Schema.Prefixes.NuGet))
                {
                    result.Add(item);
                }
                else
                {
                    throw new InvalidDataException(
                        $"The registration page at {pageUri} contains an invalid data type. Item: {item}");
                }
            }

            return result;
        }
        
        private static bool CompareSchemaType(JObject subject, Uri schemaType, string prefix = null)
        {
            var subjectType = subject[PropertyNames.SchemaType].ToString()
                .Replace("catalog:", string.Empty)
                .Replace("nuget:", string.Empty);

            if (!string.IsNullOrEmpty(prefix))
            {
                return string.Equals(prefix + subjectType, schemaType.ToString(), StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(subjectType, schemaType.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }
}