// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Tokenattributes;

namespace NuGet.Indexing
{
    public class ExpandAcronymsFilter : TokenFilter
    {
        private readonly IAcronymExpansionProvider _acronymExpansionProvider;

        private readonly ICharTermAttribute _termAttribute;
        private readonly IPositionIncrementAttribute _positionIncrementAttribute;
        private readonly Queue<string> _tokenSet;
        private readonly HashSet<string> _recognizedTokens;
        private State _currentState;

        public ExpandAcronymsFilter(TokenStream input, IAcronymExpansionProvider acronymExpansionProvider)
            : base(input)
        {
            _acronymExpansionProvider = acronymExpansionProvider;

            _termAttribute = AddAttribute<ICharTermAttribute>();
            _positionIncrementAttribute = AddAttribute<IPositionIncrementAttribute>();
            _tokenSet = new Queue<string>();
            _recognizedTokens = new HashSet<string>();
        }

        public sealed override bool IncrementToken()
        {
            if (_tokenSet.Count > 0)
            {
                var nextToken = _tokenSet.Dequeue();
                RestoreState(_currentState);
                _termAttribute.CopyBuffer(nextToken.ToCharArray(), 0, nextToken.Length);
                _positionIncrementAttribute.PositionIncrement = 0;

                return true;
            }

            try
            {
                if (!input.IncrementToken()) // end of stream; no more tokens on input stream
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }

            var term = _termAttribute.ToString();
            if (!string.IsNullOrEmpty(term))
            {
                var acronyms = _acronymExpansionProvider.GetKnownAcronyms()
                    .Where(a => term.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0);

                foreach (var acronym in acronyms)
                {
                    // Add expanded acronym (ef => entity;framework)
                    foreach (var expansion in _acronymExpansionProvider.Expand(acronym))
                    {
                        if (_recognizedTokens.Add(expansion))
                        {
                            _tokenSet.Enqueue(expansion);
                        }
                    }

                    // Add original term without the acronym (xamlbehaviors with xaml acronym => behaviors)
                    var termWithoutAcronym = RemoveSubstring(term, acronym);
                    if (!string.IsNullOrEmpty(termWithoutAcronym))
                    {
                        if (_recognizedTokens.Add(termWithoutAcronym))
                        {
                            _tokenSet.Enqueue(termWithoutAcronym);
                        }
                    }
                }
            }

            _currentState = CaptureState();
            return true;
        }

        /// <summary>
        /// This method removes a substring from a given string. For example given "foobar", "foo", it will return "bar".
        /// It ignores case, so "FOOba", "foo" will also return "bar".
        /// </summary>
        /// <param name="original">Original string</param>
        /// <param name="substring">Substring to reove from original string</param>
        /// <returns>Original string with occurrences of substring removed</returns>
        internal static string RemoveSubstring(string original, string substring)
        {
            if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(substring))
            {
                return original;
            }

            var result = new StringBuilder(original.Length);

            int substringLength = substring.Length;
            int substringStartIndex = -1;
            int lastCharacterIndex = 0;

            do
            {
                substringStartIndex = original.IndexOf(substring, substringStartIndex + 1, StringComparison.OrdinalIgnoreCase);

                if (substringStartIndex >= 0)
                {
                    result.Append(original, lastCharacterIndex, substringStartIndex - lastCharacterIndex);

                    lastCharacterIndex = substringStartIndex + substringLength;
                }
            }
            while (substringStartIndex >= 0);

            result.Append(original, lastCharacterIndex, original.Length - lastCharacterIndex);

            return result.ToString();
        }
    }
}