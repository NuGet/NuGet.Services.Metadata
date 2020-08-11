# This repository has moved!

This repository has been merged into https://github.com/NuGet/NuGet.Jobs. All changes to the nuget.org V3 jobs and
Search Service should be made to the NuGet.Jobs repository.

As of commit NuGet.Jobs commit
[`dd26676619c5901c9924fc9b3286ba263c41a446`](https://github.com/NuGet/NuGet.Jobs/commit/dd26676619c5901c9924fc9b3286ba263c41a446)
all projects, files, and Git history of the NuGet.Services.Metadata repository have been merged into the NuGet.Jobs
repository. This merge was done to reduce the amount of overhead needed to maintain the back-end of nuget.org.

If you have a broken link found in documentation, please let us know by opening a bug on that documentation page.

If you'd like to try to work around the broken link, try changing the "NuGet.Services.Metadata" part of the URL to
"NuGet.Jobs". This is not guaranteed to work as the code changes, but it may help. For example:

<pre>
BEFORE: https://github.com/NuGet/<b>NuGet.Services.Metadata</b>/blob/master/build.ps1
 AFTER: https://github.com/NuGet/<b>NuGet.Jobs</b>/blob/master/build.ps1
</pre>

## Perhaps you're looking for...

- [NuGet/NuGet.Jobs](https://github.com/NuGet/NuGet.Jobs) - the destination for this repository move
- [NuGet/NuGetGallery](https://github.com/NuGet/NuGetGallery) - the code that runs the www.nuget.org website
  and the issue tracker for all nuget.org issues
- [NuGet/Home](https://github.com/NuGet/Home) - the issue tracker for NuGet client
- [NuGet/NuGet.Client](https://github.com/NuGet/NuGet.Client) - the code for NuGet client, i.e. Visual Studio
  integration, .NET CLI integration, MSBuild integration, nuget.exe, etc. 

## Still confused?

Feel free to open an issue at [NuGet/NuGetGallery](https://github.com/NuGet/NuGetGallery/issues) and someone will help
you out.
