// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;
using System.IO;

namespace Ng
{
    public class BenchmarkC2R
    {
        public static string WaveCursorTimeValue = "2016-07-15T08:22:40.463";
        public static string EndCursorArgument = "endCursor";
        private static string BaseDirectoryAddress;
        public BenchmarkC2R(ILoggerFactory loggerFactory, IDictionary<string, string> arguments, CancellationToken token) {
            CommandHelpers.TryGetArgument(arguments, Constants.StorageBaseAddress, out BaseDirectoryAddress);

            TextWriterTraceListener myListener = new TextWriterTraceListener("E:\\Nuget\\Assets\\benchmark_output.log", "myListener");
            myListener.WriteLine($"Window(1 day/period)\tGraph");
            var catalog2Registration = new Catalog2Registration(loggerFactory);
            var runC2R = new Action<IDictionary<string, string>, CancellationToken, bool>(catalog2Registration.Run);
            DateTime WaveCursorTime = DateTime.Parse(WaveCursorTimeValue);
            DateTime EndCursorTime = WaveCursorTime;
            ResetFrontCursor(arguments);
            for (var commitPeriod = 1; commitPeriod <= 7; commitPeriod++)
            {
                //ResetFrontCursor(arguments);
                //CleanUp();
                EndCursorTime = EndCursorTime.AddDays(1);
                UpdateEndCursorTime(arguments, EndCursorTime.ToString());
                var timeWithRawJson = Time(runC2R, arguments, token, isGraph: false);
                myListener.WriteLine($"{commitPeriod}\t\t\t\t\t\t{timeWithRawJson}");
                //ResetFrontCursor(arguments);
                //CleanUp();
                //var timeWithGraph = Time(runC2R, arguments, token, isGraph: true);
                //myListener.WriteLine($"{commitPeriod}\t\t\t\t\t\t{timeWithGraph}");
                //myListener.WriteLine($"{commitPeriod}\t\t\t\t{timeWithConcurrentProcessing}\t{timeWithNonConcurrentProcessing}");

                myListener.Flush();
            }
        }

        public static TimeSpan Time(Action<IDictionary<string, string>, CancellationToken, bool> action, IDictionary<string, string> arguments, CancellationToken token, bool isGraph = true)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            action.DynamicInvoke(arguments, token, isGraph);
            stopwatch.Stop();
            return stopwatch.Elapsed;
        }

        private static void UpdateEndCursorTime(IDictionary<string, string> args, string endTime)
        {
            var prefixedCursorArgument = "-" + EndCursorArgument;
            if (args.ContainsKey(prefixedCursorArgument))
            {
                args[prefixedCursorArgument] = endTime;
            } else
            {
                args.Add(prefixedCursorArgument, endTime);
            }
        }

        private void ResetFrontCursor(IDictionary<string, string> args) {
            string cursorFilePath = Path.Combine(BaseDirectoryAddress, "cursor.json");
            string contents = $"{{ \"value\": \"{WaveCursorTimeValue}\" }}";
            File.WriteAllText(cursorFilePath, contents);
        }

        private void CleanUp() {
            System.IO.DirectoryInfo di = new DirectoryInfo(BaseDirectoryAddress);

            foreach (FileInfo file in di.GetFiles())
            {
                if (!file.Name.Equals("cursor.json", StringComparison.OrdinalIgnoreCase))
                {
                    file.Delete();
                }
            }

            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                dir.Delete(true);
            }
        }
    }
}