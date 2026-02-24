# OsLib

	Handling of files and system calls.

_formerly_ __OsLibCore__

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

- RaiPath: `Path`, `/` operator
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

- TmpFile: `create`
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

## unit tests

Check out JsonPitSolution for unit tests and usage of OsLib and other packages.