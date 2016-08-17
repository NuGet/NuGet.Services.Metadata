using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Metadata.Catalog.RawJsonRegistration
{
    public abstract class TypedSortingCollector 
        : SortingCollector
    {
        private readonly Uri[] _types;

        public TypedSortingCollector(Uri index, Uri[] types, Func<HttpMessageHandler> handlerFunc = null)
            : base(index, handlerFunc)
        {
            _types = types;
        }

        protected override async Task ProcessSortedBatch(
            CollectorHttpClient client,
            KeyValuePair<string, IList<JObject>> sortedBatch,
            JToken context,
            CancellationToken cancellationToken)
        {
            var graphs = new Dictionary<string, JObject>();
            var graphTasks = new Dictionary<string, Task<JObject>>();

            foreach (var item in sortedBatch.Value)
            {
                if (Utils.IsType((JObject)context, item, _types))
                {
                    var itemUri = item["@id"].ToString();
                    var task = client.GetJObjectAsync(new Uri(itemUri), cancellationToken);

                    graphTasks.Add(itemUri, task);

                    if (!Concurrent)
                    {
                        task.Wait(cancellationToken);
                    }
                }
            }

            await Task.WhenAll(graphTasks.Values.ToArray());

            foreach (var task in graphTasks)
            {
                graphs.Add(task.Key, task.Value.Result);
            }

            if (graphs.Count > 0)
            {
                await ProcessTypedBatch(new KeyValuePair<string, IDictionary<string, JObject>>(sortedBatch.Key, graphs), cancellationToken);
            }
        }

        protected abstract Task ProcessTypedBatch(KeyValuePair<string, IDictionary<string, JObject>> sortedGraphs, CancellationToken cancellationToken);
    }
}