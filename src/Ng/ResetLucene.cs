// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using Lucene.Net.Index;
using Lucene.Net.Util;
using NuGet.Indexing;

namespace Ng
{
    public static class ResetLucene
    {
        public static void PrintUsage()
        {
            Console.WriteLine("Usage: ng clearlucene "
                + $"-{Constants.LuceneDirectoryType} file|azure "
                + $"[-{Constants.LucenePath} <file-path>]"
                + "|"
                + $"[-{Constants.LuceneStorageAccountName} <azure-acc> "
                    + $"-{Constants.LuceneStorageKeyValue} <azure-key> "
                    + $"-{Constants.LuceneStorageContainer} <azure-container>]");
        }

        public static void Run(IDictionary<string, string> arguments)
        {
            Lucene.Net.Store.Directory directory = CommandHelpers.GetLuceneDirectory(arguments);

            if (DirectoryReader.IndexExists(directory))
            {
                var writerConfig = new IndexWriterConfig(LuceneVersion.LUCENE_48, new PackageAnalyzer());
                using (IndexWriter writer = new IndexWriter(directory, writerConfig))
                {
                    writer.DeleteAll();
                    writer.CommitData = new Dictionary<string, string>();
                    writer.Commit();
                }
            }

            Console.WriteLine("All Done");
        }
    }
}
