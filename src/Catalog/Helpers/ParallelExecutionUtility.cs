// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    public static class ParallelExecutionUtility
    {
        public const int DefaultDegreeOfParallelism = 32;

        /// <summary>
        /// Runs a task created by <paramref name="taskFactory"/> on a number of threads specified by <paramref name="degreeOfParallelism"/> and waits for all tasks to complete.
        /// </summary>
        /// <param name="taskFactory">Creates tasks for each thread to run.</param>
        /// <param name="degreeOfParallelism">The number of threads to run tasks on.</param>
        /// <returns>A task that completes when all threads have completed their tasks.</returns>
        public static Task ProcessInParallel(Func<Task> taskFactory, int degreeOfParallelism = DefaultDegreeOfParallelism)
        {
            var bagTakers = new List<Task>();
            for (int i = 0; i < degreeOfParallelism; i++)
            {
                bagTakers.Add(taskFactory());
            }

            return Task.WhenAll(bagTakers);
        }
    }
}