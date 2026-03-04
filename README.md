# OsLib

	Handling of files and system calls.

_formerly_ __OsLibCore__

## 3.0.0

- Adds `ImageFile` and `ImageTreeFile` as first-class OsLib types for image-oriented naming and tree-based storage paths.
- Uses `ItemId` terminology for image identity in the new image file model.
- This major version marks an architectural expansion: OsLib now includes structured media path semantics, not only generic OS/file helpers.

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
<summary>Os: Platform helpers for paths, escaping, and cloud storage root discovery.</summary>

- Os: `CloudStorageRoot`, `HomeDir`, `TempDir`, `Escape`, `NormSeperator`
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
<summary>ItemTreePath: RaiPath convention with partition folders from ItemId prefixes.</summary>

- ItemTreePath: `RootPath`, `ItemId`, `Topdir`, `Subdir`, `Apply`
</details>

<details>
<summary>PathConventionType and IPathConventionFile: Shared path-convention contract for convention-aware files.</summary>

- PathConventionType: `CanonicalByName`, `ItemIdTree`
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
<summary>ColorInfo: Lightweight image color descriptor.</summary>

- ColorInfo: `Code`, `Name`, `Count`
</details>

<details>
<summary>ImageFile: Image-oriented filename parser and composer.</summary>

- ImageFile: `ItemId`, `NameExt`, `ImageNumber`, `TileTemplate`, `TileNumber`, `ExtendToFirstExistingFile`
</details>

<details>
<summary>ImageTreeFile: ImageFile with tree-based storage path conventions.</summary>

- ImageTreeFile: `Topdir`, `Subdir`, tree-aware `Path`
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

## unit tests

- Local unit tests are in [OsLib.Tests](OsLib.Tests).
- Run from repository root: `dotnet test`
- Additional integration/usage tests still exist across JsonPitSolution.

## nuget publish automation

- GitHub Actions workflow: `.github/workflows/publish-nuget.yml`
- Trigger: push a version tag in format `v*` (example: `v3.0.0`)
- Safety check: workflow validates tag version equals `<Version>` in `OsLib.csproj`
- Required GitHub repository secret: `NUGET_API_KEY`
- Typical release command:
	- `git tag -a v3.0.0 -m "v3.0.0" && git push origin v3.0.0`