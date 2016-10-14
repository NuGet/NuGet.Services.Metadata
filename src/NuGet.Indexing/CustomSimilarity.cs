// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Lucene.Net.Index;
using Lucene.Net.Search.Similarities;

namespace NuGet.Indexing
{
    public class CustomSimilarity : DefaultSimilarity
    {
        public override float LengthNorm(FieldInvertState state)
        {
            var fieldName = state.Name;
            var numTerms = state.UniqueTermCount;

            if (fieldName == "TokenizedId" || fieldName == "ShingledId" || fieldName == "Title")
            {
                return 1;
            }
            else if (fieldName == "Tags" && numTerms <= 15)
            {
                return 1;
            }
            else
            {
                return base.LengthNorm(state);
            }
        }
    }
}
