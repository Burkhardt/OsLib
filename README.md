# OsLib

	Handling of files and system calls.

_formerly_ __OsLibCore__

## 3.7.0

- Patch: corrects NuGet publish order so OsLibCore lands on NuGet before downstream packages (RaiUtils, RaiImage, JsonPit).
- Documents the supported cloud-backed provider claim as `OneDrive`, `GoogleDrive`, and `Dropbox`.
- Keeps `osconfig.json` and `defaultCloudOrder` as the shared machine-local contract used across the `RAIkeep` package stack.
- Separates intrinsic OS/runtime directories from config-driven directories: `UserHomeDir` and `AppRootDir` are intrinsic, while `TempDir`, `LocalBackupDir`, and `CloudStorageRootDir` are resolved through config plus safe fallbacks.
- Moves `*Dir` properties to `RaiPath` return values to encourage `RaiPath` and `RaiFile` composition instead of `Path.Combine` call-site sprawl.
- Treats missing or invalid `osconfig.json` as a startup-critical degraded-mode condition with structured logging plus explicit console startup diagnostics.
- Aligns OsLib documentation with JsonPit's `Id`-based identifier contract and legacy `Name` normalization policy.
- Refreshes the packaged NuGet icon asset to a square `128x128` PNG.
- Documents `CanonicalPath` as deprecated legacy API surface; prefer direct `RaiPath` composition.
- Adds `PathConventionsTests` constructor tests for directory-style cloud-storage paths.

## namespace 

OsLib

## classes

<details>
<summary>RaiSystem: Run external processes with optional output capture.</summary>

- RaiSystem: `Exec`, `Start`, `CreateScript`
</details>

<details>
<summary>Script: Executable script file backed by TextFile and RaiSystem.</summary>

- Script: create a script file from content, save/update it, and apply Unix executable mode automatically.
</details>

<details>
<summary>RaiNetDrive: Windows network drive mount helper.</summary>

- RaiNetDrive: `Mount`, `Unmount`
</details>

<details>
<summary>EscapeMode: Defines escape modes for path and parameter handling.</summary>

- EscapeMode
</details>

<details>
<summary>OsType: Identifies the OS type (UNIX or Windows).</summary>

- OsType
</details>

<details>
<summary>Os: Platform helpers for paths, escaping, provider-based cloud storage discovery, and local backup placement.</summary>

- Os: `UserHomeDir`, `AppRootDir`, `TempDir`, `LocalBackupDir`, `CloudStorageRootDir`, `GetCloudStorageRoots`, `GetCloudStorageRoot`, `GetCloudStorageRootDir`, `GetPreferredCloudStorageRoot`, `GetPreferredCloudStorageRootDir`, `ResetCloudStorageCache`, `GetCloudDiscoveryReport`, `Escape`, `NormSeperator`
</details>

<details>
<summary>RaiFile: File and directory utility with path parsing and cloud-aware behaviors.</summary>

- RaiFile: `Exists`, `rm`, `mv`, `cp`, `mkdir`, `rmdir`, `Zip`, `backup`
</details>

<details>
<summary>RaiFileExtensions: Convenience extensions for string and CSV handling.</summary>

- RaiFileExtensions: `MakePolicyCompliant`, `Singularize`, `CreateDictionariesFromCsvLines`
</details>

<details>
<summary>RaiPath: Represents a directory path and enforces a trailing directory separator.</summary>

- RaiPath: `Path`, `/` operator, `mkdir`
</details>

<details>
<summary>CanonicalPath: deprecated legacy RaiPath convention where folder name equals file stem.</summary>

- CanonicalPath: `RootPath`, `FileStem`, `Apply`
- Status: deprecated legacy type retained for compatibility; prefer direct `RaiPath` composition.
</details>

<details>
<summary>PathConventionType and IPathConventionFile: Shared path-convention contract for convention-aware files.</summary>

- PathConventionType: `CanonicalByName`, `ItemIdTree3x3`, `ItemIdTree8x2`
- IPathConventionFile: `ConventionName`, `ApplyPathConvention`
</details>

<details>
<summary>TextFile: Text file with cached line operations and save support.</summary>

- TextFile: `Read`, `Save`, `Append`, `Delete`
</details>

<details>
<summary>CsvFile: Tab-delimited CSV reader with object conversion.</summary>

- CsvFile: `Read`, `Objects`, `ToJsonFile`
</details>

<details>
<summary>TmpFile: Temporary file wrapper.</summary>

- TmpFile: `create` (creates missing parent directories via `TextFile.Save`/`RaiFile.mkdir`)
</details>

<details>
<summary>CanonicalFile: Enforces canonical file-in-folder convention.</summary>

- CanonicalFile
</details>

<details>
<summary>ShellHelper: Helpers for running shell commands.</summary>

- ShellHelper: `Bash`
</details>

<details>
<summary>CliCommand and tool wrappers: executable discovery, install hints, and process execution.</summary>

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
- Cloud root discovery setup, provider precedence, and cloud-aware IO behavior: [CLOUD_STORAGE_DISCOVERY.md](CLOUD_STORAGE_DISCOVERY.md)
- Path/config/logging design note for the 2026 housekeeping pass: [PATH_CONFIG_LOGGING_REFACTOR.md](PATH_CONFIG_LOGGING_REFACTOR.md)
- CLI command hierarchy and external tool wrappers: [../CliCommand-Hierarchy.puml](../CliCommand-Hierarchy.puml)
- Local backup placement: `Os.LocalBackupDir` resolves an OS-local, non-cloud directory as `RaiPath` and can be configured in `osconfig.json`.
- Structured logging: OsLib diagnostics use `ILogger<T>` templates; only startup-critical configuration failures emit console diagnostics.
- Ubuntu/Mzansi guidance: prefer explicit `cloud.*` entries in `osconfig.json` over probe-only discovery for stable Google Drive roots across machines.
- Script helper: use `RaiSystem.CreateScript(path, name, content)` or `new Script(path, name, content)` when tests or tools need an executable script file.

## unit tests

- Local unit tests are in [OsLib.Tests](OsLib.Tests).
- Run from repository root: `dotnet test`
- Additional integration/usage tests still exist across JsonPitSolution.

## release notes

- Current release notes: [RELEASE_NOTES_3.7.0.md](RELEASE_NOTES_3.7.0.md)

## nuget publish automation

- GitHub Actions workflow: `.github/workflows/publish-nuget.yml`
- Trigger: push a version tag in format `v*` (example: `v3.7.0`)
- Safety check: workflow validates tag version equals `<Version>` in `OsLib.csproj`
- Required GitHub repository secret: `NUGET_API_KEY`
- Typical release command:
	- `git tag -a v3.7.0 -m "v3.7.0" && git push origin v3.7.0`