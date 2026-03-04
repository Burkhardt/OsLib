# OsLib API Reference

This document provides a detailed, foldable API overview.

## core types

- <details>
	<summary>Os: OS-aware environment and path utilities.</summary>

	- Responsibilities: platform detection, home/temp discovery, separator and escaping helpers.
	- <details>
		<summary>HomeDir: user home directory detection (Windows/Unix).</summary>

		- Returns the current user's home directory with cross-platform fallback behavior.
		</details>
	- <details>
		<summary>CloudStorageRoot: cloud root discovery (Dropbox/OneDrive scenarios).</summary>

		- Attempts to resolve cloud root path depending on platform and available metadata.
		</details>
	- <details>
		<summary>NormPath(path): normalize path to current OS conventions.</summary>

		- Converts separators where needed and keeps path representation consistent.
		</details>
	- <details>
		<summary>Escape(value, mode): apply selected escaping strategy.</summary>

		- Supports `noEsc`, `blankEsc`, `paramEsc`, and `backslashed` modes.
		</details>
	- <details>
		<summary>NormSeperator(value): normalize slash direction to OS separator.</summary>

		- Replaces backslashes using the active `DIRSEPERATOR`.
		</details>
	</details>

- <details>
	<summary>RaiPath: directory path value object.</summary>

	- Responsibilities: keep directory path semantics and trailing separator; allow fluent path composition.
	- <details>
		<summary>Path: normalized directory path with trailing separator.</summary>

		- Setting `Path` ensures directory semantics by clearing file components internally.
		</details>
	- <details>
		<summary>operator /(self, subDir): append subdirectory.</summary>

		- Builds a new `RaiPath` by adding one segment plus separator.
		</details>
	</details>

- <details>
	<summary>RaiFile: generic file and directory utility.</summary>

	- Responsibilities: parse/compose file identity, IO operations, cloud-aware waiting semantics.
	- <details>
		<summary>Name / Ext / Path / FullName: core file identity properties.</summary>

		- `Name` setter parses extension; `Path` normalizes separators and ensures trailing separator.
		</details>
	- <details>
		<summary>Exists(), rm(), cp(), mv(...): existence, delete, copy, move lifecycle.</summary>

		- Includes optional replacement/backup behavior and cloud synchronization checks.
		</details>
	- <details>
		<summary>mkdir(), rmdir(...): directory creation and recursive cleanup.</summary>

		- `mkdir` creates target directories; `rmdir` supports depth and optional file deletion.
		</details>
	- <details>
		<summary>backup(copy): create dated backup file.</summary>

		- Creates a timestamped backup in configured local backup location.
		</details>
	</details>

## text and data files

- <details>
	<summary>TextFile: line-based text file abstraction.</summary>

	- Responsibilities: cached line edits with persistence helpers.
	- <details>
		<summary>Lines / indexer: in-memory line access.</summary>

		- Provides lazy loading and indexed read/write with change tracking.
		</details>
	- <details>
		<summary>Read(), Save(backup): load and persist lines.</summary>

		- `Save` can optionally create backups and handles cloud materialization semantics.
		</details>
	- <details>
		<summary>Append(), Insert(), Delete(), Sort(): editing helpers.</summary>

		- Mutation operations mark file state as changed.
		</details>
	</details>

- <details>
	<summary>CsvFile: tab-delimited CSV helper on top of TextFile.</summary>

	- <details>
		<summary>Read(...): parse CSV lines and align row structure.</summary>

		- Builds column selectors and normalizes malformed linefeeds in fields.
		</details>
	- <details>
		<summary>Objects(), Object(index): row-to-object conversion.</summary>

		- Converts rows into typed JSON objects where possible.
		</details>
	- <details>
		<summary>ToJsonFile(dest): export CSV to JSON array file.</summary>

		- Serializes current tabular content as JSON text.
		</details>
	</details>

## process and shell

- <details>
	<summary>ShellHelper: shell command convenience helpers.</summary>

	- <details>
		<summary>Bash(cmd): execute command via /bin/bash.</summary>

		- Returns captured stdout as string.
		</details>
	</details>

- <details>
	<summary>RaiSystem: external process execution wrapper.</summary>

	- <details>
		<summary>Exec(out msg): run command and capture stdout/stderr.</summary>

		- Runs process synchronously and returns exit code plus combined output.
		</details>
	- <details>
		<summary>Exec(wait): run with optional wait behavior.</summary>

		- Returns process handle and optionally waits for completion.
		</details>
	- <details>
		<summary>Start(): async process execution.</summary>

		- Uses `RunProcessAsTask` to run command asynchronously.
		</details>
	</details>

## path conventions

- <details>
	<summary>PathConventionType / IPathConventionFile: convention contract for convention-aware files.</summary>

	- <details>
		<summary>PathConventionType: supported convention kinds.</summary>

		- Values: `CanonicalByName`, `ItemIdTree`.
		</details>
	- <details>
		<summary>IPathConventionFile: convention-aware file behavior.</summary>

		- Defines `ConventionName` and `ApplyPathConvention()`.
		</details>
	</details>

- <details>
	<summary>CanonicalPath: canonical folder convention.</summary>

	- <details>
		<summary>RootPath / FileStem: canonical path inputs.</summary>

		- `RootPath` is normalized; `FileStem` defines the canonical subfolder name.
		</details>
	- <details>
		<summary>Apply(): enforce canonical folder structure.</summary>

		- Resulting path is `RootPath/FileStem/`.
		</details>
	</details>

- <details>
	<summary>CanonicalFile: file using canonical-by-name storage convention.</summary>

	- <details>
		<summary>ConventionName: reports `CanonicalByName`.</summary>

		- Identifies active path convention.
		</details>
	- <details>
		<summary>ApplyPathConvention(): enforce canonical path via CanonicalPath.</summary>

		- Moves/creates underlying file placement according to canonical layout.
		</details>
	</details>

## image-oriented model

- <details>
	<summary>ColorInfo: lightweight image color descriptor.</summary>

	- <details>
		<summary>Code / Name / Count: color code, display name, frequency.</summary>

		- `Code` validates hex color input.
		</details>
	</details>

- <details>
	<summary>ItemTreePath: ItemId-derived partitioning path convention.</summary>

	- <details>
		<summary>RootPath / ItemId / Topdir / Subdir: tree partition inputs and outputs.</summary>

		- `Topdir` is first 3 chars, `Subdir` first 6 chars of `ItemId`.
		</details>
	- <details>
		<summary>Apply(): rebuild partitioned path.</summary>

		- Composes final path as `RootPath/Topdir/Subdir/`.
		</details>
	</details>

- <details>
	<summary>ImageFile: image filename parser/composer.</summary>

	- <details>
		<summary>ItemId / NameExt / ImageNumber / TileTemplate / TileNumber / Color.</summary>

		- Represents image naming components and derived filename semantics.
		</details>
	- <details>
		<summary>Parse(): decode file naming scheme into model fields.</summary>

		- Interprets naming conventions, color segments, and tile suffix patterns.
		</details>
	- <details>
		<summary>ExtendToFirstExistingFile(extensions, colorInfo): resolve first existing file variant.</summary>

		- Searches by extension and optional color to update current image descriptor.
		</details>
	</details>

- <details>
	<summary>ImageTreeFile: ImageFile with ItemId-tree storage convention.</summary>

	- <details>
		<summary>ConventionName: reports `ItemIdTree`.</summary>

		- Identifies active path convention.
		</details>
	- <details>
		<summary>Topdir / Subdir / TopdirRoot / SubdirRoot.</summary>

		- Exposes partitioned path components derived from `ItemId`.
		</details>
	- <details>
		<summary>ApplyPathConvention(): enforce ItemTreePath-based location.</summary>

		- Recomputes and applies tree path from current `ItemId`.
		</details>
	</details>
