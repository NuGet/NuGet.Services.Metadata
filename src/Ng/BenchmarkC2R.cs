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

        public BenchmarkC2R(ILoggerFactory loggerFactory, IDictionary<string, string> arguments, CancellationToken token) {
            TextWriterTraceListener myListener = new TextWriterTraceListener("E:\\Nuget\\Assets\\benchmark_output.log", "myListener");
            myListener.WriteLine($"Period(in mins)\tGraph\t\t\t\tJSON");
            var catalog2Registration = new Catalog2Registration(loggerFactory);
            var runC2RWithGraph = new Action<IDictionary<string, string>, CancellationToken>(catalog2Registration.Run);

            for (var commitPeriod = 1; commitPeriod <= 10; commitPeriod++)
            {

            }

            resetCursor(arguments);
            DateTime WaveCursorTime = DateTime.Parse(WaveCursorTimeValue);
            DateTime EndCursorTime = WaveCursorTime.AddMinutes(1);
            UpdateEndCursorTime(arguments, EndCursorTime.ToString());
            var timeWithGraph = Time(runC2RWithGraph, arguments, token);

            //resetCursor(arguments);
            //var timeWithJSON = Time(runC2RWithGraph, arguments, token);
            myListener.WriteLine($"{1}\t\t\t\t{timeWithGraph}\t");
            myListener.Flush();
        }

        public static TimeSpan Time(Action<IDictionary<string, string>, CancellationToken> action, IDictionary<string, string> arguments, CancellationToken token)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            action.Invoke(arguments, token);
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

        private void resetCursor(IDictionary<string, string> args) {
            string baseAddress;
            CommandHelpers.TryGetArgument(args, Constants.StorageBaseAddress, out baseAddress);
            string cursorFilePath = Path.Combine(baseAddress, "cursor.json");
            string contents = $"{{ \"value\": \"{WaveCursorTimeValue}\"}}";
            File.WriteAllText(cursorFilePath, contents);
        }

        private void after() {
            // cleanup work here
        }
    }
}