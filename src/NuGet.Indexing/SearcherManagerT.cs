// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using System.Threading.Tasks;

namespace NuGet.Indexing
{
    public abstract class SearcherManager<TIndexSearcher>
        where TIndexSearcher : IndexSearcher
    {
        private readonly object _sync = new object();
        private bool _reopening;
        private TIndexSearcher _currentSearcher;

        protected abstract Directory GetDirectory();

        public async Task Open()
        {
            if (_currentSearcher == null)
            {
                var tempSearcher = await CreateSearcher(IndexReader.Open(GetDirectory(), true));
                lock (_sync)
                {
                    if (_currentSearcher == null)
                    {
                        _currentSearcher = tempSearcher;
                        if (_currentSearcher == null)
                        {
                            throw new Exception("Unable to create IndexSearcher");
                        }
                    }
                }
            }

            Warm(_currentSearcher);
        }

        protected abstract Task<IndexReader> Reopen(IndexSearcher searcher);

        protected virtual bool RequiresNewSearcher(IndexReader newReader, IndexSearcher currentSearcher)
        {
            return newReader != currentSearcher.IndexReader;
        }

        protected abstract Task<TIndexSearcher> CreateSearcher(IndexReader reader);

        protected virtual void Warm(TIndexSearcher searcher)
        {
        }

        private void StartReopen()
        {
            lock (_sync)
            {
                while (_reopening)
                {
                    Monitor.Wait(_sync);
                }
                _reopening = true;
            }
        }

        private void DoneReopen()
        {
            lock (_sync)
            {
                _reopening = false;
                Monitor.PulseAll(_sync);
            }
        }
        public async Task MaybeReopen()
        {
            StartReopen();

            try
            {
                TIndexSearcher searcher = Get();
                try
                {
                    var newReader = await Reopen(_currentSearcher);

                    if (RequiresNewSearcher(newReader, _currentSearcher))
                    {
                        var newSearcher = await CreateSearcher(newReader);
                        if (newSearcher != null)
                        {
                            Warm(newSearcher);
                            SwapSearcher(newSearcher);
                        }
                    }
                }
                finally
                {
                    Release(searcher);
                }
            }
            finally
            {
                DoneReopen();
            }
        }

        public TIndexSearcher Get()
        {
            lock (_sync)
            {
                _currentSearcher.IndexReader.IncRef();
                return _currentSearcher;
            }
        }

        public void Release(TIndexSearcher searcher)
        {
            lock (_sync)
            {
                searcher.IndexReader.DecRef();
            }
        }

        private void SwapSearcher(TIndexSearcher newSearcher)
        {
            lock (_sync)
            {
                Release(_currentSearcher);
                _currentSearcher = newSearcher;
            }
        }

        public void Close()
        {
            SwapSearcher(null);
        }
    }
}