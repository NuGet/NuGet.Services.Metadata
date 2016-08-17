// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Xunit.Abstractions;

namespace NgTests.Infrastructure
{
    public class StopwatchMeasurement
        : IDisposable
    {
        private readonly string _subject;
        private readonly ITestOutputHelper _output;
        private readonly Stopwatch _stopwatch;

        public StopwatchMeasurement(string subject, ITestOutputHelper output)
        {
            _subject = subject;
            _output = output;

            _stopwatch = new Stopwatch();
            _stopwatch.Start();
        }

        public void Dispose()
        {
            _stopwatch.Stop();

            _output.WriteLine("{0} - execution time: {1}", _subject, _stopwatch.Elapsed.ToString("g"));
        }
    }
}