// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    public class Retry
    {
        public static async Task IncrementalAsync(
            Func<Task> runLogicAsync,
            Func<Exception, bool> shouldRetryOnException,
            int maxRetries,
            TimeSpan initialWaitInterval,
            TimeSpan waitIncrement)
        {
            for (int currentRetry = 0; currentRetry < maxRetries; ++currentRetry)
            {
                try
                {
                    await runLogicAsync();
                    return;
                }
                catch (Exception e) when (currentRetry < maxRetries - 1 && shouldRetryOnException(e))
                {
                    await Task.Delay(initialWaitInterval + TimeSpan.FromSeconds(waitIncrement.TotalSeconds * currentRetry));
                }
            }
        }

        public static async Task<T> IncrementalAsync<T>(
            Func<Task<T>> runLogicAsync,
            Func<Exception, bool> shouldRetryOnException,
            Func<T, bool> shouldRetry,
            int maxRetries,
            TimeSpan initialWaitInterval,
            TimeSpan waitIncrement)
        {
            var result = default(T);
            for (int currentRetry = 0; currentRetry < maxRetries; ++currentRetry)
            {
                try
                {
                    result = await runLogicAsync();
                    if (!shouldRetry(result))
                    {
                        return result;
                    }
                }
                catch (Exception e) when (currentRetry < maxRetries - 1 && shouldRetryOnException(e))
                {
                    await Task.Delay(initialWaitInterval + TimeSpan.FromSeconds(waitIncrement.TotalSeconds * currentRetry));
                }
            }

            return result;
        }
    }
}
