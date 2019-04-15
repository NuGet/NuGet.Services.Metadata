# NuGet.Services.Metadata

This repo contains the services that power [NuGet.org's V3 APIs](https://docs.microsoft.com/en-us/nuget/api/overview), including:

* [Catalog](https://docs.microsoft.com/en-us/nuget/api/catalog-resource) - The resource that records all package operations, such as creations and deletions.
* [Package Metadata (aka "Registration")](https://docs.microsoft.com/en-us/nuget/api/registration-base-url-resource) - The resource that contains the metadata for each available package.
* [Package Content (aka "Flat container")](https://docs.microsoft.com/en-us/nuget/api/package-base-address-resource) - The resource used to fetch package content like the .nupkg file.
* [Search](https://docs.microsoft.com/en-us/nuget/api/search-query-service-resource) - The service used by clients to discover packages.

The catalog, package metadata, and package content resources are all statically hosted on Azure Blob Storage. The [`Ng` job](src/Ng/readme.md) updates these resources whenever packages are uploaded or deleted.

The search service, [`NuGet.Services.SearchService`](src/NuGet.Services.SearchService/readme.md), is backed by Azure Search. Its index is created by the [`NuGet.Jobs.Db2AzureSearch`](src/NuGet.Jobs.Db2AzureSearch/readme.md) and [`NuGet.Jobs.Catalog2AzureSearch`](src/NuGet.Jobs.Catalog2AzureSearch/readme.md) jobs.

## Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Feedback

If you're having trouble with the NuGet.org Website, file a bug on the [NuGet Gallery Issue Tracker](https://github.com/nuget/NuGetGallery/issues). 

If you're having trouble with the NuGet client tools (the Visual Studio extension, NuGet.exe command line tool, etc.), file a bug on [NuGet Home](https://github.com/nuget/home/issues).

Check out the [contributing](http://docs.nuget.org/contribute) page to see the best places to log issues and start discussions. The [NuGet Home](https://github.com/NuGet/Home) repo provides an overview of the different NuGet projects available.
