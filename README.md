# OsLib

Handling of files, paths, temp/backup directories, and system calls.

_formerly_ __OsLibCore__

## 3.7.6

- Documents the current `RAIkeep.json5` contract and lazy `Os.Config` behavior.
- Adds `RaiFile.BackdateCreationTime(...)` and `RaiFile.DefaultSyncPropagationDelayMs` to the current public surface.
- Clarifies the propagation-delay precedence: explicit parameter, then `Os.Config.SyncPropagationDelayMs`, then the static default.
- Documents the delegate firewall between `Os` and `RaiPath`: `CloudPathWiring`, `RaiPath.CloudEvaluator`, and buffered `RaiPath.Cloud`.
- Documents cloud-aware wait loops by responsibility: `RaiPath` owns directory waits and `RaiFile` owns file waits.

## namespace

OsLib

## classes

<details>
<summary>RaiSystem: Run external processes with structured output capture.</summary>

- RaiSystem: `Exec`, `ExecResult`, `Start`, `CreateScript`
</details>

<details>
<summary>Script: Executable script file backed by TextFile and RaiSystem.</summary>

- Script: create a script file from content, save/update it, and apply Unix executable mode automatically.
</details>

<details>
<summary>EscapeMode: Defines escape modes for path and parameter handling.</summary>

- EscapeMode
</details>

<details>
<summary>OsType: Identifies the OS type.</summary>

- OsType
</details>

<details>
<summary>Os: Platform helpers, lazy config access, diagnostics, and path normalization.</summary>

- Os: `UserHomeDir`, `AppRootDir`, `TempDir`, `LocalBackupDir`, `Config`, `IsConfigLoaded`, `DefaultConfigFileLocation`, `Escape`, `NormPath`, `NormSeperator`
</details>

<details>
<summary>CloudPathWiring: Initializes the `RaiPath.CloudEvaluator` delegate from `Os.Config`.</summary>

- CloudPathWiring: `Initialize`
</details>

<details>
<summary>RaiPath: Directory path type with buffered cloud classification.</summary>

- RaiPath: `Path`, `Cloud`, `CloudEvaluator`, `/` operator, `Parent`, `mkdir`, `rmdir`, `EnumerateFiles`
</details>

<details>
<summary>RaiFile: File utility with cloud-aware wait behavior.</summary>

- RaiFile: `Exists`, `rm`, `mv`, `cp`, `mkdir`, `rmdir`, `AwaitVanishing`, `AwaitMaterializing`, `BackdateCreationTime`, `DefaultSyncPropagationDelayMs`, `Zip`, `backup`
</details>

<details>
<summary>RaiFileExtensions: Convenience extensions for string and CSV handling.</summary>

- RaiFileExtensions: `MakePolicyCompliant`, `Singularize`, `CreateDictionariesFromCsvLines`
</details>

<details>
<summary>TextFile, CsvFile, TmpFile: Text/data-file helpers built on RaiFile.</summary>

- TextFile: `Read`, `Save`, `Append`, `Delete`
- CsvFile: `Read`, `Objects`, `ToJsonFile`
- TmpFile: `create`
</details>

<details>
<summary>CanonicalPath, CanonicalFile, and path conventions: retained compatibility helpers.</summary>

- CanonicalPath: deprecated legacy type retained for compatibility; prefer direct `RaiPath` composition.
- PathConventionType / IPathConventionFile: convention-aware file contracts.
</details>

<details>
<summary>SshSystem and CLI wrappers: remote shell execution and typed command launchers.</summary>

- SshSystem: `ExecuteRemoteCommand`, `ExecuteScript`, `ReadRemoteConfigJson5`
- CliCommand: `IsAvailable`, `TryResolveExecutable`, `Run`, `RunAsync`, `GetInstallCommand`, `GetUpdateCommand`
- Built-in wrappers: `CurlCommand`, `ZipCommand`, `SevenZipCommand`, `RCloneCommand`
</details>

## nuget

https://www.nuget.org/packages/OsLibCore/

## diagram

- Source: [RaiFile-Hierarchy.puml](RaiFile-Hierarchy.puml)
- CLI render (if PlantUML is installed): `plantuml RaiFile-Hierarchy.puml`
- VS Code: open the `.puml` file and use a PlantUML preview/render extension.

## detailed api

- Foldable class and method-level documentation: [API.md](API.md)
- Current cloud configuration and buffered cloud-path behavior: [CLOUD_STORAGE_DISCOVERY.md](CLOUD_STORAGE_DISCOVERY.md)
- Historical path/config/logging design note, now marked with 3.7.6 caveats: [PATH_CONFIG_LOGGING_REFACTOR.md](PATH_CONFIG_LOGGING_REFACTOR.md)
- CLI command hierarchy and external tool wrappers: [../CliCommand-Hierarchy.puml](../CliCommand-Hierarchy.puml)
- Local backup placement: `Os.LocalBackupDir` is optional; when absent, backup features are disabled instead of falling back.
- Structured logging: OsLib diagnostics use `ILogger<T>` templates. The current config path falls back to a baseline `TempDir` model rather than treating missing config as a startup-fatal public API contract.
- Cloud config guidance: prefer explicit `Cloud.*` entries in `RAIkeep.json5` when you want stable cloud-backed path classification.
- Metadata propagation guidance: `RaiFile.BackdateCreationTime(...)` uses `SyncPropagationDelayMs` from config when no explicit delay is passed.
- Script helper: use `RaiSystem.CreateScript(path, name, content)` or `new Script(path, name, content)` when tests or tools need an executable script file.

## unit tests

- Local unit tests are in [OsLib.Tests](OsLib.Tests).
- Run from repository root: `dotnet test OsLib/OsLib.Tests/OsLib.Tests.csproj --nologo -v minimal`

## release notes

- Current release notes: [RELEASE_NOTES_3.7.6.md](RELEASE_NOTES_3.7.6.md)

## nuget publish automation

- GitHub Actions workflow: `.github/workflows/publish-nuget.yml`
- Trigger: push a version tag in format `v*` (example: `v3.7.6`)
- Safety check: workflow validates tag version equals `<Version>` in `OsLib.csproj`
- Required GitHub repository secret: `NUGET_API_KEY`
- Typical release command:
	- `git tag -a v3.7.6 -m "v3.7.6" && git push origin v3.7.6`