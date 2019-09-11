// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Icons
{
    public class HttpResponseMessageProvider : IHttpResponseMessageProvider
    {
        private readonly HttpClient _httpClient;

        public HttpResponseMessageProvider(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<HttpResponseMessage> GetAsync(Uri uri, CancellationToken cancellationToken)
        {
            return await _httpClient.GetAsync(uri, cancellationToken);
        }
    }
}
