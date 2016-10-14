// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Lucene.Net.Search;
using Lucene.Net.Index;
using Lucene.Net.Util;

namespace Lucene.Net.Analysis
{

    ///<summary>
    ///* <p>
    /// * Allows multiple {@link Filter}s to be chained.
    /// * Logical operations such as <b>NOT</b> and <b>XOR</b>
    /// * are applied between filters. One operation can be used
    /// * for all filters, or a specific operation can be declared
    /// * for each filter.
    /// * </p>
    /// * <p>
    /// * Order in which filters are called depends on
    /// * the position of the filter in the chain. It's probably
    /// * more efficient to place the most restrictive filters
    /// * /least computationally-intensive filters first.
    /// * </p>
    ///</summary>
    public class ChainedFilter : Filter
    {
        public enum Logic
        {
            NONE = -1,
            OR = 0,
            AND = 1,
            ANDNOT = 2,
            XOR = 3
        };

        ///<summary>Logical operation when none is declared. Defaults to OR</summary>
        public const Logic DEFAULT = Logic.OR;

        /** The filter chain */
        private Filter[] chain = null;

        private Logic[] logicArray;

        private Logic logic = Logic.NONE;

        ///<summary>Ctor</summary><param name="chain">The chain of filters</param>
        public ChainedFilter(Filter[] chain)
        {
            this.chain = chain;
        }

        ///<summary>ctor</summary>
        ///<param name="chain">The chain of filters</param>
        ///<param name="logicArray">Logical operations to apply between filters</param>
        public ChainedFilter(Filter[] chain, Logic[] logicArray)
        {
            this.chain = chain;
            this.logicArray = logicArray;
        }

        ///<summary>ctor</summary>
        ///<param name="chain">The chain of filters</param>
        ///<param name="logic">Logical operation to apply to ALL filters</param>
        public ChainedFilter(Filter[] chain, Logic logic)
        {
            this.chain = chain;
            this.logic = logic;
        }

        ///<see cref="Filter#getDocIdSet"/>
        public override DocIdSet GetDocIdSet(AtomicReaderContext context, Bits acceptDocs)
        {
            int[] index = new int[1]; // use array as reference to modifiable int; 
            index[0] = 0;             // an object attribute would not be thread safe.
            if (logic != Logic.NONE)
                return GetDocIdSet(context, logic, index, acceptDocs);
            else if (logicArray != null)
                return GetDocIdSet(context, logicArray, index, acceptDocs);
            else
                return GetDocIdSet(context, DEFAULT, index, acceptDocs);
        }

        private DocIdSetIterator GetDISI(Filter filter, AtomicReaderContext context, Bits acceptDocs)
        {
            DocIdSet docIdSet = filter.GetDocIdSet(context, acceptDocs);
            if (docIdSet == null)
            {
                return DocIdSetIterator.Empty();
            }
            else
            {
                DocIdSetIterator iter = docIdSet.GetIterator();
                if (iter == null)
                {
                    return DocIdSetIterator.Empty();
                }
                else
                {
                    return iter;
                }
            }
        }

        private OpenBitSetDISI InitialResult(AtomicReaderContext context, Logic logic, int[] index, Bits acceptDocs)
        {
            OpenBitSetDISI result;
            /**
             * First AND operation takes place against a completely false
             * bitset and will always return zero results.
             */
            if (logic == Logic.AND)
            {
                result = new OpenBitSetDISI(GetDISI(chain[index[0]], context, acceptDocs), context.AtomicReader.MaxDoc);
                ++index[0];
            }
            else if (logic == Logic.ANDNOT)
            {
                result = new OpenBitSetDISI(GetDISI(chain[index[0]], context, acceptDocs), context.AtomicReader.MaxDoc);
                result.Flip(0, context.AtomicReader.MaxDoc); // NOTE: may set bits for deleted docs.
                ++index[0];
            }
            else
            {
                result = new OpenBitSetDISI(context.AtomicReader.MaxDoc);
            }
            return result;
        }


        ///<summary>
        ///  * Provide a SortedVIntList when it is definitely
        ///  * smaller than an OpenBitSet
        ///  * @deprecated Either use CachingWrapperFilter, or
        ///  * switch to a different DocIdSet implementation yourself.
        ///  * This method will be removed in Lucene 4.0 
        ///</summary>
        protected DocIdSet FinalResult(OpenBitSetDISI result, int maxDocs)
        {
            return result;
        }


        /**
         * Delegates to each filter in the chain.
         * @param reader IndexReader
         * @param logic Logical operation
         * @return DocIdSet
         */
        private DocIdSet GetDocIdSet(AtomicReaderContext context, Logic logic, int[] index, Bits acceptDocs)
        {
            OpenBitSetDISI result = InitialResult(context, logic, index, acceptDocs);
            for (; index[0] < chain.Length; index[0]++)
            {
                DoChain(result, logic, chain[index[0]].GetDocIdSet(context, acceptDocs));
            }
            return FinalResult(result, context.AtomicReader.MaxDoc);
        }

        /**
         * Delegates to each filter in the chain.
         * @param reader IndexReader
         * @param logic Logical operation
         * @return DocIdSet
         */
        private DocIdSet GetDocIdSet(AtomicReaderContext context, Logic[] logic, int[] index, Bits acceptDocs)
        {
            if (logic.Length != chain.Length)
                throw new ArgumentException("Invalid number of elements in logic array");

            OpenBitSetDISI result = InitialResult(context, logic[0], index, acceptDocs);
            for (; index[0] < chain.Length; index[0]++)
            {
                DoChain(result, logic[index[0]], chain[index[0]].GetDocIdSet(context, acceptDocs));
            }
            return FinalResult(result, context.AtomicReader.MaxDoc);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("ChainedFilter: [");
            for (int i = 0; i < chain.Length; i++)
            {
                sb.Append(chain[i]);
                sb.Append(' ');
            }
            sb.Append(']');
            return sb.ToString();
        }

        private void DoChain(OpenBitSetDISI result, Logic logic, DocIdSet dis)
        {

            if (dis is OpenBitSet)
            {
                // optimized case for OpenBitSets
                switch (logic)
                {
                    case Logic.OR:
                        result.Or((OpenBitSet)dis);
                        break;
                    case Logic.AND:
                        result.And((OpenBitSet)dis);
                        break;
                    case Logic.ANDNOT:
                        result.AndNot((OpenBitSet)dis);
                        break;
                    case Logic.XOR:
                        result.Xor((OpenBitSet)dis);
                        break;
                    default:
                        DoChain(result, DEFAULT, dis);
                        break;
                }
            }
            else
            {
                DocIdSetIterator disi;
                if (dis == null)
                {
                    disi = DocIdSetIterator.Empty();
                }
                else
                {
                    disi = dis.GetIterator();
                    if (disi == null)
                    {
                        disi = DocIdSetIterator.Empty();
                    }
                }

                switch (logic)
                {
                    case Logic.OR:
                        result.InPlaceOr(disi);
                        break;
                    case Logic.AND:
                        result.InPlaceAnd(disi);
                        break;
                    case Logic.ANDNOT:
                        result.InPlaceNot(disi);
                        break;
                    case Logic.XOR:
                        result.InPlaceXor(disi);
                        break;
                    default:
                        DoChain(result, DEFAULT, dis);
                        break;
                }
            }
        }
    }

}