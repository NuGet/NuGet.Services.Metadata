using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;


namespace CatalogReader
{
    //collects the packages that pass a specific filter
    public class PackageFilterCollector : CommitCollector
    {
        Func<JToken, bool> _filterPredicate;
        string _dirLogPathPrefix = ""; //@"F:\NuGet\Temp\CatalogReader";
        string _dirLogPath = "";
        int _index = 0;

        public PackageFilterCollector(Uri index, string logDir,  Func<HttpMessageHandler> handlerFunc = null, Func<JToken, bool> filterPredicate = null) : base(index, handlerFunc)
        {
            _filterPredicate = filterPredicate;
            _dirLogPathPrefix = logDir;
            _dirLogPath = Path.Combine(_dirLogPathPrefix, Guid.NewGuid().ToString());
            Directory.CreateDirectory(_dirLogPath);

        }

        protected override Task<bool> OnProcessBatch(CollectorHttpClient client, IEnumerable<JToken> items, JToken context, DateTime commitTimeStamp, bool isLastBatch, CancellationToken cancellationToken)
        {
            if(cancellationToken.IsCancellationRequested)
            {
                Task.FromResult(true);
            }
            string newFile = Path.Combine(_dirLogPath, $"{Interlocked.Increment(ref _index)}.txt");
            List<string> data = new List<string>();
            data.Add($"id,version,type,commitTimeStamp,isLastBatch");
            foreach (JToken item in items)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                Console.WriteLine(item["nuget:id"].ToString().ToLowerInvariant());
                if ( (_filterPredicate != null && _filterPredicate(item)) || _filterPredicate == null)
                {
                    string id = item["nuget:id"].ToString().ToLowerInvariant();
                    string version = item["nuget:version"].ToString().ToLowerInvariant();
                    string type = item["@type"].ToString().Replace("nuget:", Schema.Prefixes.NuGet);

                    data.Add($"{id},{version},{type},{commitTimeStamp.ToString()},{isLastBatch}");
                    //if (type == Schema.DataTypes.PackageDetails.ToString())
                    //{

                    //}
                    //else if (type == Schema.DataTypes.PackageDelete.ToString())
                    //{
                    //    await _dnxMaker.DeletePackage(id, version, cancellationToken);

                    //    Trace.TraceInformation("commit delete: {0}/{1}", id, version);
                    //}
                }
              
            }
            if (data.Count > 1)
            {
                File.AppendAllLines(newFile, data);
            }
            return Task.FromResult(true); 
        }
    }
}
