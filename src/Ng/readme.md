# Ng

The `Ng` job generates the static resources that power the [NuGet V3 APIs](https://docs.microsoft.com/en-us/nuget/api/overview). Each resource is generated using a subcommand:

* `ng feed2catalog` - Generates the [Catalog](https://docs.microsoft.com/en-us/nuget/api/catalog-resource) resource using the NuGet V2 APIs.
* `ng catalog2registration` - Generates the [Package Metadata](https://docs.microsoft.com/en-us/nuget/api/registration-base-url-resource) resource using the [Catalog](https://docs.microsoft.com/en-us/nuget/api/catalog-resource).
* `ng catalog2dnx` - Generates the [Package Content](https://docs.microsoft.com/en-us/nuget/api/package-base-address-resource) resource using the [Catalog](https://docs.microsoft.com/en-us/nuget/api/catalog-resource).