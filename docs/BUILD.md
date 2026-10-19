# Reproducible build

The build uses .NET SDK **8.0.426**, pinned by global.json with roll-forward disabled. SkyCoop still targets **net6.0** for MelonLoader. SDK 8 is a build tool, not a new game runtime requirement. C# language version is fixed at 12.0. NuGet.Config restricts restore to nuget.org; packages.lock.json pins the resolved dependency graph and content hashes. The TLD interop package is 2.55.0, Steamworks.NET is 20.2.0, and the resolved MelonLoader reference is 0.7.1. README's claim of testing 0.7.3 is not independently verified.

Linux x64 bootstrap (Python 3.12+):

```
python3 tools/bootstrap_dotnet.py
export DOTNET_ROOT="$(pwd)/../.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_HOME="$(pwd)/../.dotnet-home"
export NUGET_PACKAGES="$(pwd)/../.nuget/packages"
```

The bootstrap downloads the official SDK archive and verifies the committed SHA-256 before extracting. SDK source, SHA-256 and official SHA-512 are in tools/sdk-linux-x64.json. Existing SDK installations can be used if `dotnet --version` matches global.json. On Windows use Microsoft's installer for this exact SDK; a working MelonLoader installation supplies the game's .NET runtime separately.

From the repository root:

```
dotnet restore SkyCoop.csproj --locked-mode
dotnet build SkyCoop.csproj -c Release --no-restore -p:ContinuousIntegrationBuild=true
python3 tools/prepare_regression.py
python3 tools/run_tests.py --mutation-check
python3 tools/check_reproducible.py
python3 tools/check_package.py
python3 tools/package_test_build.py
```

The managed regression executable uses net8.0 so it runs on the build SDK's runtime; it is never shipped with the mod. It compiles the actual BundleAsset method body against a controlled bundle facade and links the original ThreadManager.cs. This covers managed behavior, not Unity/IL2CPP native runtime compatibility. The mutation check restores the original recursion in a disposable generated test source and requires the child process to fail; production source stays unchanged.

Output: bin/Release/SkyCoop.dll plus the Windows x64 managed Steamworks.NET.dll. No game installation is needed to compile against the NuGet interop references. The output directory deliberately excludes game/interop DLLs. Required Unity assets are not in this repository, so the ZIP is an incomplete test kit until those externally sourced assets are supplied.

CI uses the same SDK, locked restore, managed regressions, and test-kit packaging. It does not install or launch the game. In this managed cloud environment .NET named mutexes require an approved command outside the filesystem sandbox; ordinary sandbox execution fails at NuGet-Migrations. Network access also requires the configured network grant. No game or save files are modified by the build.
