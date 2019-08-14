using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// Splits tokens on a set of symbols, whitespace, and initialisms.
    /// For example, "FOOBar.Baz Qux" becomes "FOO", "Bar", "Baz", and "Qux".
    /// </summary>
    public static class PackageIdCustomTokenizer
    {
        public const string Name = "nuget_package_id_tokenizer";

        public static readonly PatternTokenizer Instance = new PatternTokenizer(
            Name,
            @"((?<=[A-Z])(?=[A-Z][a-z]))|([.\-_,;:'*#!~+()\[\]{}\s])");
    }
}
