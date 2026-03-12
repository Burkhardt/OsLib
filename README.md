# OsLib

	Handling of files and system calls.

_formerly_ __OsLibCore__

## 3.2.1

- Strengthens generic path-convention contracts and canonical file organization support.
- Keeps OsLib focused on reusable OS/file foundations for public package consumers.

## namespace 

OsLib

## classes

<details>
<summary>RaiSystem: Run external processes with optional output capture.</summary>

- RaiSystem: `Exec`, `Start`
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
<summary>Os: Platform helpers for paths, escaping, and provider-based cloud storage discovery.</summary>

- Os: `CloudStorageRoot`, `GetCloudStorageRoots`, `GetCloudStorageRoot`, `GetPreferredCloudStorageRoot`, `ResetCloudStorageCache`, `GetCloudDiscoveryReport`, `HomeDir`, `TempDir`, `Escape`, `NormSeperator`
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
<summary>CanonicalPath: RaiPath convention where folder name equals file stem.</summary>

- CanonicalPath: `RootPath`, `FileStem`, `Apply`
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

## nuget

https://www.nuget.org/packages/OsLibCore/

## diagram

- Source: [RaiFile-Hierarchy.puml](RaiFile-Hierarchy.puml)
- CLI render (if PlantUML is installed): `plantuml RaiFile-Hierarchy.puml`
- VS Code: open the `.puml` file and use a PlantUML preview/render extension.

## detailed api

- Foldable class and method-level documentation: [API.md](API.md)
- Cloud root discovery setup, provider precedence, and cloud-aware IO behavior: [CLOUD_STORAGE_DISCOVERY.md](CLOUD_STORAGE_DISCOVERY.md)
- Ubuntu/Mzansi guidance: prefer `OSLIB_CLOUD_ROOT_GOOGLEDRIVE` or `OSLIB_CLOUD_CONFIG` over probe-only discovery for stable Google Drive roots across C# and Python packages.

## unit tests

- Local unit tests are in [OsLib.Tests](OsLib.Tests).
- Run from repository root: `dotnet test`
- Additional integration/usage tests still exist across JsonPitSolution.

## nuget publish automation

- GitHub Actions workflow: `.github/workflows/publish-nuget.yml`
- Trigger: push a version tag in format `v*` (example: `v3.2.1`)
- Safety check: workflow validates tag version equals `<Version>` in `OsLib.csproj`
- Required GitHub repository secret: `NUGET_API_KEY`
- Typical release command:
	- `git tag -a v3.2.1 -m "v3.2.1" && git push origin v3.2.1`