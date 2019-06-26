using Microsoft.Extensions.Logging;
using NuGet.Indexing;
using System;

namespace DownloadCountAnalysis
{
    class Program
    {
        static void Main(string[] args)
        {
            var downloads = new Downloads();
            var logger = new LoggerFactory().CreateLogger<NuGetSearcherManager>();

            downloads.Load("file name", logger);

        }
    }
}
