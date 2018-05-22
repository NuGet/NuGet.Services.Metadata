using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace NuGet.Services.AzureSearch
{
    public class Startup
    {
        public const string SearchOptionsSection = "search";

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.Configure<SearchOptions>(Configuration.GetSection(SearchOptionsSection));

            services.AddSingleton<UrlBuilder>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<SearchOptions>>().Value;

                return new UrlBuilder(options.RegistrationBase);
            });

            services.AddSingleton(provider =>
            {
                var options = provider.GetRequiredService<IOptions<SearchOptions>>().Value;
                var credentials = new SearchCredentials(options.ApiKey);

                return new SearchIndexClient(options.AccountName, options.IndexName, credentials);
            });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseStatusCodePages();
            }

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    "v3-search",
                    "search",
                    defaults: new { controller = "V3Search", action = "Get" });
            });
        }
    }
}
