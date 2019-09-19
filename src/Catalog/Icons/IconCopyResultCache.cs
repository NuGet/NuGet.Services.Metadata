// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.Icons
{
    public class IconCopyResultCache : IIconCopyResultCache, IIconCopyResultCachePersistence
    {
        private const string CacheFilename = "c2i_cache.json";

        private static ConcurrentDictionary<Uri, ExternalIconCopyResult> ExternalIconCopyResults = null;

        private readonly IStorage _auxStorage;

        public IconCopyResultCache(
            IStorage auxStorage)
        {
            _auxStorage = auxStorage ?? throw new ArgumentNullException(nameof(auxStorage));
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            if (ExternalIconCopyResults != null)
            {
                return;
            }

            var cacheUrl = _auxStorage.ResolveUri(CacheFilename);
            var content = await _auxStorage.LoadAsync(cacheUrl, cancellationToken);
            if (content == null)
            {
                ExternalIconCopyResults = new ConcurrentDictionary<Uri, ExternalIconCopyResult>();
                return;
            }
            using (var contentStream = content.GetContentStream())
            using (var reader = new StreamReader(contentStream))
            {
                var serializer = new JsonSerializer();
                var dictionary = (Dictionary<Uri, ExternalIconCopyResult>)serializer.Deserialize(reader, typeof(Dictionary<Uri, ExternalIconCopyResult>));
                ExternalIconCopyResults = new ConcurrentDictionary<Uri, ExternalIconCopyResult>(dictionary);
            }
        }

        public async Task SaveAsync(CancellationToken cancellationToken)
        {
            var cacheUrl = _auxStorage.ResolveUri(CacheFilename);
            var serialized = JsonConvert.SerializeObject(ExternalIconCopyResults);
            var content = new StringStorageContent(serialized, contentType: "text/json");
            await _auxStorage.SaveAsync(cacheUrl, content, cancellationToken);
        }

        public ExternalIconCopyResult Get(Uri iconUrl)
        {
            if (ExternalIconCopyResults.TryGetValue(iconUrl, out var result))
            {
                return result;
            }

            return null;
        }

        public void Set(Uri iconUrl, ExternalIconCopyResult newItem)
        {
            ExternalIconCopyResults.AddOrUpdate(iconUrl, newItem, (_, v) => v); // will not overwrite existing entries
        }

        public void Clear(Uri externalIconUrl, Uri targetStorageUrl)
        {
            if (ExternalIconCopyResults.TryRemove(externalIconUrl, out var removedValue))
            {
                if (removedValue.StorageUrl != targetStorageUrl)
                {
                    // if we removed item that does not match the target URL, put it back.
                    Set(externalIconUrl, removedValue);
                }
            }
        }
    }
}
