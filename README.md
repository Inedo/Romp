# Romp

This is the repository for Romp, a command-line OtterScript execution engine. Romp is
used to deploy Romp Packages, which are Universal Packages containing additional metadata.

## Installing Romp

If you'd just like to install the latest version of Romp, you can download it from
[here](https://inedo.com/romp/download).

## Building Romp

Romp can be built using Visual Studio 2017 (either the IDE or build tools), but it requires
some NuGet packages that are not generally available from NuGet.org. Note that most of these
packages are not yet open-sourced; we are gradually in the process of making the source code
for everything available, but we're not quite there yet :) As such, please only use these
dependencies for building this project, as we cannot support any other usage of them and they
currently have no public API surface.

That said, you just need to configure an NuGet package source to build this project:
https://proget.inedo.com/nuget/ExternalBuild/

To add this package source to Visual Studio, follow these steps:

1. Click Tools->NuGet Package Manager->Package Manager Settings
2. Click Package Sources on the tree control
3. Click the Add button and paste in the [package source URL](https://proget.inedo.com/nuget/ExternalBuild/), and supply a name
4. Click the Update button to save these changes
5. Click the OK button to close the dialog

Now all the packages needed for building Romp will be downloaded as necessary when you build
the project from Visual Studio.

If you'd like to build via command-line or restore packages manually with nuget.exe, just pass
the same package source URL above in via the appropriate command line arguments.

## Pull Requests

We are happy to consider pull requests. We'll have more information available as we open up
more of our projects. But for now, here's a few guidelines for what we'll _consider_:
 - Bug fixes of any kind (obviously the more trivial the code change the more likely we are to accept quickly)
 - _Minor_ features or usability tweaks that have little or no impact on documentation
 - Must **not** contain large non-functional changes (i.e. please don't rewrite the entire project just because you don't like the coding style)
   - We'll consider refactoring changes so long as they provide some tangible benefit in maintainability, but it would be best to start a discussion with us before starting something like this
 - Must **not** add major new features, change current behaviors, or otherwise render our current documentation totally incorrect
