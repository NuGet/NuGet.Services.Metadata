using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Search;
using NuGet.Indexing;

namespace NuGet.Services.AzureSearch.Controllers
{
    [Route("api/[controller]")]
    public class ValuesController : Controller
    {
        private readonly SearchIndexClient _searchClient;

        public ValuesController(SearchIndexClient searchClient)
        {
            _searchClient = searchClient ?? throw new ArgumentNullException(nameof(searchClient));
        }

        // GET api/values
        [HttpGet]
        public async Task<IEnumerable<object>> GetAsync()
        {
            var search = await _searchClient.Documents.SearchAsync<PackageDocument>("Newtonsoft.Json");

            return search.Results.Select(r => r.Document);
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/values
        [HttpPost]
        public void Post([FromBody]string value)
        {
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
