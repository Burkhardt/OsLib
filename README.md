# OsLib

Handling of files, paths, temp/backup directories, and system calls.

OsLib change requests and release notes are centralized in the RAIkeep [`doc/`](https://github.com/Burkhardt/RAIkeep/tree/main/doc) directory under `OsLib_...` filenames; they are not stored separately in this child repository.

_formerly_ __OsLibCore__

## 4.2.0

- Current prepared release line for `OsLibCore` is `4.2.0`.
- This coordinated release preserves the CR008 runtime behavior introduced in 4.1.0 while bringing OsLibCore into the seven-package line that introduces RaiDiagram.
- `Os.TempDir` remains sourced from immutable runtime configuration and is now validated once at first Os initialization with an OsLib `TmpFile` write/remove probe.
- Startup fails fast when the configured temp directory is not writable; `Os.Config` is neither mutated nor bypassed with a fallback.
- `RaiPathException` and `RaiPathNotFoundException` provide path-specific failures.
- `RaiFile.WriteFromAsync(IAsyncEnumerable<byte[]>, CancellationToken)` provides stream-free chunk ingestion.
- `TextFile.SaveInPlace()` writes a small coordination file without a preceding delete or rename, while retaining cloud materialization checks.
- Configured cloud-path classification recognizes `Dropbox`, `OneDrive`, `GoogleDrive`, and `ICloudDrive` roots.
- The `RaiFile.mkdir()` virtual dispatch, UTC timestamp handling, and async `RaiFile` APIs remain current.
- See [OsLib_RELEASE_NOTES_4.2.0.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/OsLib_RELEASE_NOTES_4.2.0.md) for details.

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

### RaiFile: File utility with cloud-aware wait behavior.

- RaiFile: `Exists`, `LastWriteTimeUtc`, `rm`, `mv`, `cp`, `mkdir`, `rmdir`, `WriteFromAsync`, `ReadAllBytesAsync`, `AwaitVanishing`, `AwaitMaterializing`, `BackdateCreationTime`, `DefaultSyncPropagationDelayMs`, `Zip`, `backup`

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
- CliCommand: `IsAvailable`, `TryResolveExecutable`, `Run`, `RunAsync`, `GetInstallCommand`, `GetUpdateCommand`
- Built-in wrappers: `CurlCommand`, `ZipCommand`, `SevenZipCommand`, `RCloneCommand`

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

- Current release notes: [OsLib_RELEASE_NOTES_4.2.0.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/OsLib_RELEASE_NOTES_4.2.0.md)

## nuget publish automation

- GitHub Actions workflow: `.github/workflows/publish-nuget.yml`
- Trigger: push a version tag in format `v*` (example: `v3.8.0`)
- Safety check: workflow validates tag version equals `<Version>` in `OsLib.csproj`
- Required GitHub repository secret: `NUGET_API_KEY`
- Typical release command:
	- `git tag -a v4.2.0 -m "v4.2.0" && git push origin v4.2.0`
