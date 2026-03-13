# OsLib API Reference

This document provides a detailed, foldable API overview.

## core types

- <details>
	<summary>Os: OS-aware environment and path utilities.</summary>

	- Responsibilities: platform detection, home/temp discovery, separator and escaping helpers, and provider-based cloud discovery.
	- <details>
		<summary>HomeDir: user home directory detection (Windows/Unix).</summary>

		- Returns the current user's home directory with cross-platform fallback behavior.
		</details>
	- <details>
		<summary>CloudStorageRoot: preferred discovered cloud root by default provider order.</summary>

		- Uses GoogleDrive, ICloud, Dropbox, then OneDrive, and throws if nothing is configured or discovered.
		</details>
	- <details>
		<summary>GetCloudStorageRoots(refresh): discover all available provider roots.</summary>

		- Applies precedence in this order: `Os.Config` values first, then OS-specific probing.
		- The default configuration file is `~/.config/RAIkeep/osconfig.json` on macOS and Linux, and `%APPDATA%\RAIkeep\osconfig.json` on Windows.
		- On Ubuntu and other Linux setups with custom mounts, callers should edit `cloud.googledrive` or the other `cloud.*` entries directly in config.
		</details>
	- <details>
		<summary>Config / LoadConfig(refresh): typed `OsConfigModel` access.</summary>

		- `Config` exposes the reusable `OsConfigFile` wrapper.
		- `LoadConfig()` restores the typed object from JSON, while `LoadConfig(refresh: true)` forces a reload.
		- The config object includes `HomeDir`, `TempDir`, `LocalBackupDir`, `DefaultCloudOrder`, and nested `Cloud` settings, plus convenience cloud path accessors.
		</details>
	- <details>
		<summary>GetDefaultConfigPath() / GetDefaultCloudConfigPath(): config file helpers.</summary>

		- `GetDefaultConfigPath()` resolves the active `osconfig.json` path.
		- `GetDefaultCloudConfigPath()` remains only as an obsolete compatibility alias.
		</details>
	- <details>
		<summary>GetCloudStorageRoot(provider, refresh): return one provider root or null.</summary>

		- Useful when callers need a specific provider instead of the default preferred order.
		</details>
	- <details>
		<summary>GetPreferredCloudStorageRoot(order): resolve a custom provider precedence.</summary>

		- Returns the first available provider root from the supplied order.
		</details>
	- <details>
		<summary>ResetCloudStorageCache(): force re-discovery after environment or config changes.</summary>

		- Clears cached provider roots so later lookups observe updated config or machine state.
		</details>
	- <details>
		<summary>GetCloudDiscoveryReport(refresh): readable diagnostics for all providers.</summary>

		- Lists each provider and the resolved root or a not-found marker.
		- This is the recommended startup diagnostic for Ubuntu development environments with mounted Google Drive paths.
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
	- <details>
		<summary>mkdir(): create the directory represented by this path.</summary>

		- Delegates to `RaiFile.mkdir()` so chained composition like `new RaiPath(root) / "ProjectRoot"` can materialize directly.
		</details>
	</details>

- <details>
	<summary>RaiFile: generic file and directory utility.</summary>

	- Responsibilities: parse/compose file identity, IO operations, and cloud-aware waiting semantics based on discovered provider roots.
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

		- Creates a timestamped backup in the resolved local backup location; `Os.LocalBackupDir` avoids discovered cloud roots and can be configured in `osconfig.json`.
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

- <details>
	<summary>TmpFile: temporary file wrapper on top of RaiFile.</summary>

	- <details>
		<summary>create(): create file and ensure missing parent directories.</summary>

		- Uses `TextFile.Save()` internally, which calls `RaiFile.mkdir()` before writing.
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

- <details>
	<summary>CliCommand and tool wrappers: reusable typed command launchers.</summary>

	- Responsibilities: executable resolution, package-manager install/update hints, and `RaiSystem`-backed execution.
	- <details>
		<summary>CliCommand: base abstraction for local CLI tools.</summary>

		- Resolves a working executable from candidate names or explicit paths.
		- Exposes sync/async execution through `Run(...)` and `RunAsync(...)`.
		</details>
	- <details>
		<summary>CurlCommand / ZipCommand / SevenZipCommand: generic command wrappers.</summary>

		- Provide install/update hints per OS and, for 7-Zip, alternate executable probing.
		</details>
	- <details>
		<summary>RCloneCommand: typed wrapper for `rclone` subcommands.</summary>

		- Supports optional explicit command-path configuration.
		- Provides `BuildArguments`, `RunSubcommand`, and `RunSubcommandAsync` for higher-level cloud tooling.
		</details>
	</details>

## path conventions

- <details>
	<summary>PathConventionType / IPathConventionFile: convention contract for convention-aware files.</summary>

	- <details>
		<summary>PathConventionType: supported convention kinds.</summary>

		- Values: `CanonicalByName`, `ItemIdTree3x3`, `ItemIdTree8x2`.
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

## architecture note

- Image-domain classes are intentionally maintained in a dedicated image package.
- OsLib remains responsible for generic file/path/process foundations and shared contracts.
