// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public class SimpleTaskCache<TKey, TValue> where TValue : Task
    {
        private readonly IDictionary<TKey, TValue> _cachedTasks = new Dictionary<TKey, TValue>();

        [MethodImpl(MethodImplOptions.Synchronized)]
        public TValue Get(TKey key, Func<TValue> getTask)
        {
            return _cachedTasks.TryGetValue(key, out var value)
                ? value
                : _cachedTasks[key] = getTask();
        }
    }
}
