using System;

namespace NuGet.Services.AzureSearch
{
    public class SearchOptions
    {
        public string AccountName { get; set; }
        public string ApiKey { get; set; }
        public string IndexName { get; set; }

        public Uri RegistrationBase { get; set; }
    }
}