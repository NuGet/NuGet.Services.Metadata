using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Ng;
using NuGet.Services.Metadata.Catalog;
using Newtonsoft.Json.Linq;

namespace CatalogReader
{
    class Program
    {
        static void Main(string[] args)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            Task main = MainAsync(cts.Token);
            //main.ContinueWith((t) => { Console.WriteLine("Run ended."); cts.Cancel(); });

            Console.WriteLine("Press any task to cancel");
            Task waitT = Task.Run(() =>
            {
                Console.ReadLine();
                if (!cts.IsCancellationRequested)
                {
                    cts.Cancel();
                }
                Console.WriteLine("The cancellation was initiated by the client. The execution will end soon.");
            });

            //Console.WriteLine("Execution ended.");
            main.Wait();
        }

        static async Task MainAsync(CancellationToken token)
        {
            //dev flat containeer : account name nugetdev0
            //dev catalog : https://az635243.vo.msecnd.net/v3-catalog0/index.json

            //string source = "http://localhost/nugetvd/catalog0/index.json";
            //string idToFilter = "mapsurfer.net.core";
            string source = "http://localhost/nugetvd/catalogdev/index.json";
            string idToFilter = "cmanupacktest";
            string logDir = @"F:\NuGet\Temp\CatalogReader";

            Func<JToken,bool> filter = (t) => {
                string id = t["nuget:id"].ToString().ToLowerInvariant(); 
                return id.Contains(idToFilter);
            };
            PackageFilterCollector collector = new PackageFilterCollector(new Uri(source), logDir, CommandHelpers.GetHttpMessageHandlerFactory(true), filter);
            //var back = new MemoryCursor(new DateTime(2017, 02, 15).ToUniversalTime());
            //var front = new MemoryCursor(new DateTime(2017, 02, 13).ToUniversalTime());
            var back = MemoryCursor.CreateMax(); //new MemoryCursor(new DateTime(2017, 03, 17).ToUniversalTime());
            var front = new MemoryCursor(new DateTime(2017, 03, 20).ToUniversalTime());
            try
            {
                await collector.Run(front, back, token);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
