// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Lucene.Net.Index;
using System;
using System.Collections.Generic;

namespace Ng
{
    static class CheckLucene
    {
        public static void PrintUsage()
        {
            Console.WriteLine("Usage: ng checklucene "
                + $"-{Constants.LuceneDirectoryType} file|azure "
                + $"[-{Constants.LucenePath} <file-path>]"
                + $"|"
                + $"[-{Constants.LuceneStorageAccountName} <azure-acc> "
                    + $"-{Constants.LuceneStorageKeyValue} <azure-key> "
                    + $"-{Constants.LuceneStorageContainer} <azure-container>]");
        }

        public static void Run(IDictionary<string, string> arguments)
        {
            Lucene.Net.Store.Directory directory = CommandHelpers.GetLuceneDirectory(arguments);

            using (var reader = DirectoryReader.Open(directory))
            {
                Console.WriteLine("Lucene index contains: {0} documents", reader.NumDocs);

                // TODO: Fix this null. Ignoring commit user data for now
                IDictionary<string, string> commitUserData = null;

                if (commitUserData == null)
                {
                    Console.WriteLine("commitUserData is null");
                }
                else
                {
                    Console.WriteLine("commitUserData:");
                    foreach (var entry in commitUserData)
                    {
                        Console.WriteLine("  {0} = {1}", entry.Key, entry.Value);
                    }
                }
            }
        }
    }
}
