// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Metadata.Catalog
{
    public sealed class CatalogCommitItem : IComparable
    {
        public CatalogCommitItem(JObject value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            CommitTimeStamp = value["commitTimeStamp"].ToObject<DateTime>();
            Uri = value["@id"].ToObject<Uri>();
            Value = value;
        }

        public DateTime CommitTimeStamp { get; }
        public Uri Uri { get; }
        public JObject Value { get; }

        public int CompareTo(object obj)
        {
            return CommitTimeStamp.CompareTo(((CatalogCommitItem)obj).CommitTimeStamp);
        }
    }
}