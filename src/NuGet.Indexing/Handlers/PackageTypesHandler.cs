// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using NuGet.Versioning;

namespace NuGet.Indexing
{
    public class PackageTypesHandler : IIndexReaderProcessorHandler
    {
        private readonly IDictionary<string, HashSet<string>> _packageTypes;

        private HashSet<string> _knownPackageTypes;
        private IDictionary<string, IDictionary<string, DynamicDocIdSet>> _packageTypeTuples;

        public PackageTypesResult Result { get; private set; }

        public PackageTypesHandler(IDictionary<string, HashSet<string>> packageTypes)
        {
            _packageTypes = packageTypes;
        }

        public bool SkipDeletes => true;

        public void Begin(IndexReader indexReader)
        {
            _knownPackageTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _packageTypeTuples = new Dictionary<string, IDictionary<string, DynamicDocIdSet>>();

            if (indexReader.GetSequentialSubReaders() != null)
            {
                foreach (SegmentReader segmentReader in indexReader.GetSequentialSubReaders())
                {
                    _packageTypeTuples.Add(segmentReader.SegmentName, new Dictionary<string, DynamicDocIdSet>(StringComparer.OrdinalIgnoreCase));
                }
            }
            else
            {
                _packageTypeTuples.Add(string.Empty, new Dictionary<string, DynamicDocIdSet>(StringComparer.OrdinalIgnoreCase));
            }
        }

        public void End(IndexReader indexReader)
        {
            Result = new PackageTypesResult(_knownPackageTypes, _packageTypes, _packageTypeTuples);
        }

        public void Process(IndexReader indexReader,
            string readerName,
            int perSegmentDocumentNumber,
            int perIndexDocumentNumber,
            Document document,
            string id,
            NuGetVersion version)
        {
            HashSet<string> registrationPackageTypes;

            if (id != null && _packageTypes.TryGetValue(id, out registrationPackageTypes))
            {
                foreach (string registrationPackageType in registrationPackageTypes)
                {
                    _knownPackageTypes.Add(registrationPackageType);

                    DynamicDocIdSet packageTypeDocIdSet;
                    if (_packageTypeTuples[readerName].TryGetValue(registrationPackageType, out packageTypeDocIdSet))
                    {
                        packageTypeDocIdSet.DocIds.Add(perSegmentDocumentNumber);
                    }
                    else
                    {
                        packageTypeDocIdSet = new DynamicDocIdSet();
                        packageTypeDocIdSet.DocIds.Add(perSegmentDocumentNumber);

                        _packageTypeTuples[readerName].Add(registrationPackageType, packageTypeDocIdSet);
                    }
                }
            }
        }
    }
}