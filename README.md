# OsLib

## 4.5.15

Adds the shared CliHelpStyle formatter for aligned help, glyph colors, syntax
emphasis, provider badges, and terminal-aware plain output.

Latest release notes: [OsLib 4.5.15 release notes](https://github.com/Burkhardt/RAIkeep/blob/main/doc/releases/v4.x/OsLib_RELEASE_NOTES_4.x.md#4515).

## Previous Versions

For release notes and changelogs of earlier releases, see the [OsLib 4.x Release Notes](https://github.com/Burkhardt/RAIkeep/blob/main/doc/releases/v4.x/OsLib_RELEASE_NOTES_4.x.md).

## Installation

### .NET CLI

```bash
dotnet add package OsLibCore --version 4.5.15
```

### PackageReference

```xml
<PackageReference Include="OsLibCore" Version="4.5.15" />
```

OsLib handles files, paths, temp and backup directories, and system calls. It was formerly named OsLibCore. Change requests and release notes are kept in the RAIkeep [documentation archive](https://github.com/Burkhardt/RAIkeep/tree/main/doc).

## namespace

OsLib

## classes

### RaiSystem: Run external processes with structured output capture.

- RaiSystem: `Exec`, `ExecResult`, `Start`, `CreateScript`

### Script: Executable script file backed by TextFile and RaiSystem.

- Script: create a script file from content, save/update it, and apply Unix executable mode automatically.

### EscapeMode: Defines escape modes for path and parameter handling.

- EscapeMode

### OsType: Identifies the OS type.

- OsType

### Os: Platform helpers, immutable runtime config snapshot, diagnostics, and path normalization.

- Os: `UserHomeDir`, `AppRootDir`, `TempDir`, `LocalBackupDir`, `Config`, `IsConfigLoaded`, `ConfigFileFullName`, `DefaultConfigFileLocation`, `Escape`, `NormPath`, `NormSeperator`

### CloudPathWiring: Compatibility initializer for older callers.

- CloudPathWiring: `Initialize`

### RaiPath: Directory path type with buffered cloud classification.

- RaiPath: `Path`, `Cloud`, `/` operator, `Parent`, `mkdir`, `rmdir`, `mv`, `cp`, `backup`, `EnumerateFiles`, `EnumerateDirectories`
- TempDir-to-cloud moves and replacement of existing cloud directories fail before mutation.

### RaiFile: File utility with cloud-aware wait behavior.

- RaiFile: `Exists`, `LastWriteTimeUtc`, `rm`, `mv`, `cp`, `mkdir`, `rmdir`, `WriteFromAsync`, `ReadAllBytesAsync`, `AwaitVanishing`, `AwaitMaterializing`, `BackdateCreationTime`, `DefaultSyncPropagationDelayMs`, `Zip`, `backup`
- `ReadAllBytesAsync` preserves cancellation and wraps operating-system file I/O failures in `RaiFileIOException`, which retains `IOException` compatibility and exposes the affected `FileName`.
- `cp` updates an existing cloud pathname in place; `mv` rejects TempDir-to-cloud relocation before mutation.
- `RaiCloudStorageException` exposes `Operation`, `SourcePath`, and `DestinationPath` for prohibited cloud operations.

### RaiZipFile: Immutable collection ZIP storage at the final pathname.

- RaiZipFile: `CreateImmutable`, `TryReadEntries`
- RaiZipEntry: `Name`, `Content`, `FromFile`, `FromText`
- Existing same-name archives are reused only after complete filename/byte validation; different or corrupt archives are preserved and reported.
- The parent must already exist. No TempDir staging, directory creation, extraction, or archive replacement occurs.

### RaiFileExtensions: Convenience extensions for string and CSV handling.

- RaiFileExtensions: `MakePolicyCompliant`, `Singularize`, `CreateDictionariesFromCsvLines`

### TextFile, CsvFile, TmpFile: Text/data-file helpers built on RaiFile.

- TextFile: `Read`, `Save`, `SaveInPlace`, `Append`, `Delete`
- CsvFile: `Read`, `Objects`, `ToJsonFile`
- TmpFile: `create`

### CanonicalPath, CanonicalFile, and path conventions: retained compatibility helpers.

- CanonicalPath: deprecated legacy type retained for compatibility; prefer direct `RaiPath` composition.
- PathConventionType / IPathConventionFile: convention-aware file contracts.

### SshSystem and CLI wrappers: remote shell execution and typed command launchers.

- SshSystem: `ExecuteRemoteCommand`, `ExecuteScript`, `ReadRemoteConfigJson5`
- CliCommand: `IsAvailable`, `TryResolveExecutable`, tokenized/string `Run` and `RunAsync`, timeout-aware token execution, `BuildPosixShellCommand`, `GetInstallCommand`, `GetUpdateCommand`
- RaiSystemResult: exact `ArgumentList`, `StandardOutput`, `StandardError`, `ExitCode`, `Succeeded`, and `TimedOut` process metadata
- PitsCommand: `BuildSeedArguments`, `BuildExportArguments`, `BuildAuditArguments`, `BuildDeletePropertyArguments`, `BuildDeleteItemArguments`, typed sync/async command methods, CR047 `PitsSeedRequest.RequireExisting`, `PitsMaintainRequest.ArchiveEvents`, and `ForManagedAssembly`
- IorgCommand: `BuildOrganizeArguments`, `BuildCleanArguments`, `BuildListArguments`, `BuildMoveArguments`, sync/async counterparts, `IorgCommandOptions.Tenant`, `RootIsApplicationRoot`, and `ForManagedAssembly`
- Built-in wrappers: `CurlCommand`, `ZipCommand`, `SevenZipCommand`, `RCloneCommand`, `PitsCommand`, `IorgCommand`

### Seed a cloud tenant through pits

Use a bare tenant name for cloud addressing. `sourceFile` is a `RaiFile` (or a
`TextFile`) containing the input payload:

```csharp
var request = PitsSeedRequest.ForPit("Person", sourceFile) with
{
    Options = new PitsCommandOptions
    {
        Tenant = "SampleTenant",
        CloudProvider = "OneDrive",
        NoLogo = true
    }
};
var result = new PitsCommand().Seed(request);
if (result.ExitCode != 0)
    throw new InvalidOperationException(result.StandardError);
```

`Tenant` is passed unchanged as `-r SampleTenant`. For a local filesystem root,
use `PitRoot = localRoot` instead and omit `CloudProvider`. `Tenant` and `PitRoot`
are mutually exclusive. Same-target calls are serialized within the calling
process; different tenants can run independently.

## nuget

https://www.nuget.org/packages/OsLibCore/

## diagram

- Source: [RaiFile-Hierarchy.puml](https://github.com/Burkhardt/OsLib/blob/main/RaiFile-Hierarchy.puml)
- CLI render (if PlantUML is installed): `plantuml RaiFile-Hierarchy.puml`
- VS Code: open the `.puml` file and use a PlantUML preview/render extension.

## detailed api

- Foldable class and method-level documentation: [API.md](https://github.com/Burkhardt/OsLib/blob/main/API.md)
- Current cloud configuration and buffered cloud-path behavior: [CLOUD_STORAGE_DISCOVERY.md](https://github.com/Burkhardt/OsLib/blob/main/CLOUD_STORAGE_DISCOVERY.md)
- Historical path/config/logging design note, now marked with 3.7.7 caveats: [PATH_CONFIG_LOGGING_REFACTOR.md](https://github.com/Burkhardt/OsLib/blob/main/PATH_CONFIG_LOGGING_REFACTOR.md)
- CLI command hierarchy and external tool wrappers: [CliCommand-Hierarchy.puml](https://github.com/Burkhardt/RAIkeep/blob/main/CliCommand-Hierarchy.puml)
- Local backup placement: `Os.LocalBackupDir` is optional; when absent, backup features are disabled instead of falling back.
- Structured logging: OsLib diagnostics use `ILogger<T>` templates. TempDir initialization validates the configured path once and fails fast when its OsLib tempfile probe cannot write; it does not mutate or bypass `Os.Config`.
- Cloud config guidance: prefer explicit `Cloud.*` entries in `RAIkeep.json5` when you want stable cloud-backed path classification.
- Metadata propagation guidance: `RaiFile.BackdateCreationTime(...)` uses `SyncPropagationDelayMs` from config when no explicit delay is passed.
- Script helper: use `RaiSystem.CreateScript(path, name, content)` or `new Script(path, name, content)` when tests or tools need an executable script file.

## unit tests

- Local unit tests are in [OsLib.Tests](https://github.com/Burkhardt/OsLib/tree/main/OsLib.Tests).
- Run from repository root: `dotnet test OsLib/OsLib.Tests/OsLib.Tests.csproj --nologo -v minimal`

## nuget publish automation

- GitHub Actions workflow: `.github/workflows/publish-nuget.yml`
- Trigger: push a version tag in format `v*` (example: `v3.8.0`)
- Safety check: workflow validates tag version equals `<Version>` in `OsLib.csproj`
- Required GitHub repository secret: `NUGET_API_KEY`
- Typical release command:
	- The coordinated release is started only through the umbrella `scripts/release-chain.sh 4.5.15` command after RAI approval.
