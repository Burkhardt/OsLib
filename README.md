# OsLib

Handling of files, paths, temp/backup directories, and system calls.

OsLib change requests and release notes are centralized in the RAIkeep [`doc/`](https://github.com/Burkhardt/RAIkeep/tree/main/doc) directory under `OsLib_...` filenames; they are not stored separately in this child repository.

_formerly_ __OsLibCore__

## 4.2.10

- Adds `RaiZipFile`, a collection-capable immutable ZIP boundary that creates an archive directly at its final path in an existing directory and never overwrites a same-name archive.
- Adds `RaiZipEntry` plus semantic retry validation and read-without-extraction support.
- `EventDirectory.Inspect(...)` reads loose `.event` files and `Events_*.zip` archives together while reporting invalid or conflicting evidence.
- `PitsMaintainRequest.ArchiveEvents` emits the preferred `--archive-events` token.
- Typed `IorgCommand` requests can emit `--app` through `RootIsApplicationRoot`; `Tenant` emits `--tenant`, while `Subscriber` remains a source-compatible alias.
- Current release notes: [OsLib_RELEASE_NOTES_4.2.10.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/OsLib_RELEASE_NOTES_4.2.10.md)

## 4.2.9

- Implements accepted incident corrective action CR022 across the reusable filesystem boundary.
- `RaiFile.cp` keeps an existing cloud pathname continuously present while overwriting its content in place.
- `RaiFile.mv` and `RaiPath.mv` reject TempDir-to-CloudDrive moves before either path changes; replacement of an existing cloud directory is rejected.
- Cloud backups copy the live file/directory instead of relocating it.
- `RaiCloudStorageException` reports rejected operations with operation, source, and destination context.
- Current release notes: [OsLib_RELEASE_NOTES_4.2.9.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/OsLib_RELEASE_NOTES_4.2.9.md)
- Mandatory storage contract: [Cloud-Storage-In-Place-Invariant.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/Cloud-Storage-In-Place-Invariant.md)

## 4.2.8

- Implements accepted CR021 with dotted logical-stem preservation for explicit `TextFile` extensions.
- Adds `PitsMaintainRequest` plus typed sync/async maintenance execution.
- Serializes same-target typed `PitsCommand` calls process-locally while allowing unrelated pits to proceed concurrently; queued cancellation starts no child process.
- Current release notes: [OsLib_RELEASE_NOTES_4.2.8.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/OsLib_RELEASE_NOTES_4.2.8.md)

## 4.2.7

- Implements accepted CR020 with typed `IorgListRequest` and `IorgMoveRequest` execution through `IorgCommand`.
- Adds the compatibility-safe fourth `PathConventionType` value, `Flat`; existing numeric values remain unchanged.
- Current release notes: [OsLib_RELEASE_NOTES_4.2.7.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/OsLib_RELEASE_NOTES_4.2.7.md)

## 4.2.6

- Aligns OsLibCore with the coordinated seven-package RAIkeep 4.2.6 release implementing accepted CR019.
- Runtime and public API behavior are unchanged from 4.2.5.
- Current release notes: [OsLib_RELEASE_NOTES_4.2.6.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/OsLib_RELEASE_NOTES_4.2.6.md)

## 4.2.5

- Implements the accepted CR017 typed boundary by adding optional `PitsExportRequest.At`.
- `PitsCommand` emits `--at` and a canonical UTC timestamp as separate argument tokens for both sync and async execution.
- Requests without `At` preserve their exact established argument list.
- 4.2.5 release notes: [OsLib_RELEASE_NOTES_4.2.5.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/OsLib_RELEASE_NOTES_4.2.5.md)

## 4.2.4

- Aligns OsLibCore with the coordinated seven-package RAIkeep 4.2.4 release implementing accepted CR016.
- RaiImage performs normalization-resilient traversal exclusively through the existing `RaiPath.EnumerateDirectories(...)` and `RaiPath.EnumerateFiles(...)` boundary.
- No new direct filesystem surface or OsLib runtime behavior is introduced.
- 4.2.4 release notes: [OsLib_RELEASE_NOTES_4.2.4.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/OsLib_RELEASE_NOTES_4.2.4.md)

## 4.2.3

- The `4.2.3` release added the accepted CR015 typed delete requests.
- CR015 adds typed `PitsDeletePropertyRequest` and `PitsDeleteItemRequest` forms, exact token builders, and sync/async execution through `PitsCommand`.
- This coordinated release preserves the CR008 runtime behavior introduced in 4.1.0 and supplies the remaining asynchronous file-read exception boundary requested by AIA.
- `Os.TempDir` remains sourced from immutable runtime configuration and is now validated once at first Os initialization with an OsLib `TmpFile` write/remove probe.
- Startup fails fast when the configured temp directory is not writable; `Os.Config` is neither mutated nor bypassed with a fallback.
- `RaiPathException` and `RaiPathNotFoundException` provide path-specific failures.
- `RaiFile.WriteFromAsync(IAsyncEnumerable<byte[]>, CancellationToken)` provides stream-free chunk ingestion.
- `RaiFile.ReadAllBytesAsync(...)` wraps operating-system file failures in `RaiFileIOException` while preserving cancellation and `IOException` catch compatibility.
- CR014 adds typed `PitsCommand` and `IorgCommand` wrappers for server/agent invocation of the installed RAIkeep CLIs, including validated preferred-command arguments and exact tokenized process execution.
- `TextFile.SaveInPlace()` writes a small coordination file without a preceding delete or rename, while retaining cloud materialization checks.
- Configured cloud-path classification recognizes `Dropbox`, `OneDrive`, `GoogleDrive`, and `ICloudDrive` roots.
- The `RaiFile.mkdir()` virtual dispatch, UTC timestamp handling, and async `RaiFile` APIs remain current.
- See [OsLib_RELEASE_NOTES_4.2.3.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/OsLib_RELEASE_NOTES_4.2.3.md) for details.

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
- PitsCommand: `BuildSeedArguments`, `BuildExportArguments`, `BuildAuditArguments`, `BuildDeletePropertyArguments`, `BuildDeleteItemArguments`, typed sync/async command methods, `PitsMaintainRequest.ArchiveEvents`, and `ForManagedAssembly`
- IorgCommand: `BuildOrganizeArguments`, `BuildCleanArguments`, `BuildListArguments`, `BuildMoveArguments`, sync/async counterparts, `IorgCommandOptions.Tenant`, `RootIsApplicationRoot`, and `ForManagedAssembly`
- Built-in wrappers: `CurlCommand`, `ZipCommand`, `SevenZipCommand`, `RCloneCommand`, `PitsCommand`, `IorgCommand`

## nuget

https://www.nuget.org/packages/OsLibCore/

## diagram

- Source: [RaiFile-Hierarchy.puml](RaiFile-Hierarchy.puml)
- CLI render (if PlantUML is installed): `plantuml RaiFile-Hierarchy.puml`
- VS Code: open the `.puml` file and use a PlantUML preview/render extension.

## detailed api

- Foldable class and method-level documentation: [API.md](https://github.com/Burkhardt/OsLib/blob/main/API.md)
- Current cloud configuration and buffered cloud-path behavior: [CLOUD_STORAGE_DISCOVERY.md](https://github.com/Burkhardt/OsLib/blob/main/CLOUD_STORAGE_DISCOVERY.md)
- Historical path/config/logging design note, now marked with 3.7.7 caveats: [PATH_CONFIG_LOGGING_REFACTOR.md](https://github.com/Burkhardt/OsLib/blob/main/PATH_CONFIG_LOGGING_REFACTOR.md)
- CLI command hierarchy and external tool wrappers: [../CliCommand-Hierarchy.puml](../CliCommand-Hierarchy.puml)
- Local backup placement: `Os.LocalBackupDir` is optional; when absent, backup features are disabled instead of falling back.
- Structured logging: OsLib diagnostics use `ILogger<T>` templates. TempDir initialization validates the configured path once and fails fast when its OsLib tempfile probe cannot write; it does not mutate or bypass `Os.Config`.
- Cloud config guidance: prefer explicit `Cloud.*` entries in `RAIkeep.json5` when you want stable cloud-backed path classification.
- Metadata propagation guidance: `RaiFile.BackdateCreationTime(...)` uses `SyncPropagationDelayMs` from config when no explicit delay is passed.
- Script helper: use `RaiSystem.CreateScript(path, name, content)` or `new Script(path, name, content)` when tests or tools need an executable script file.

## unit tests

- Local unit tests are in [OsLib.Tests](OsLib.Tests).
- Run from repository root: `dotnet test OsLib/OsLib.Tests/OsLib.Tests.csproj --nologo -v minimal`

## release notes

- Latest release notes: [OsLib_RELEASE_NOTES_4.2.10.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/OsLib_RELEASE_NOTES_4.2.10.md)

## nuget publish automation

- GitHub Actions workflow: `.github/workflows/publish-nuget.yml`
- Trigger: push a version tag in format `v*` (example: `v3.8.0`)
- Safety check: workflow validates tag version equals `<Version>` in `OsLib.csproj`
- Required GitHub repository secret: `NUGET_API_KEY`
- Typical release command:
	- The coordinated release is started only through the umbrella `scripts/release-chain.sh 4.2.10` command after RAI approval.
