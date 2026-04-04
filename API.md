# OsLib API Reference

This document provides a detailed, foldable API overview.

## 3.7.3 scope note

- Minor release: path and canonicalization alignment for JsonPit integration.
- `OsLib 3.7.3` keeps the public API surface stable while refining path behavior.
- `CanonicalPath` remains available for compatibility but is deprecated; prefer direct `RaiPath` composition.

## core types

- <details>
	<summary>Os: OS-aware environment and path utilities.</summary>

	- Responsibilities: platform detection, intrinsic runtime path resolution, config-driven directory resolution, separator and escaping helpers, and provider-based cloud discovery.
	- <details>
		<summary>UserHomeDir: intrinsic OS user home directory.</summary>

		- Returns the current user's home directory as `RaiPath` with cross-platform fallback behavior.
		- This value is never taken from config.
		</details>
	- <details>
		<summary>AppRootDir: intrinsic runtime working directory.</summary>

		- Resolves the runtime meaning of `.` as `RaiPath`.
		- This value is never taken from config.
		</details>
	- <details>
		<summary>CloudStorageRootDir: preferred discovered cloud root by default provider order.</summary>

		- Uses the configured `defaultCloudOrder`, which defaults to OneDrive, Dropbox, and GoogleDrive, and throws if nothing is configured or discovered.
		- Returns `RaiPath` so callers stay on `RaiPath`/`RaiFile` composition.
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
		- Missing, unreadable, or malformed `osconfig.json` is treated as startup-critical and logged as degraded mode.
		- The config object includes `TempDir`, `LocalBackupDir`, `DefaultCloudOrder`, and nested `Cloud` settings.
		- Legacy `homeDir` is accepted only for compatibility, ignored at runtime, and logged as deprecated.
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
		- The `GetPreferredCloudStorageRootDir(order)` companion returns `RaiPath`.
		</details>
	- <details>
		<summary>ResetCloudStorageCache(): force re-discovery after environment or config changes.</summary>

		- Clears cached provider roots so later lookups observe updated config or machine state.
		</details>
	- <details>
		<summary>GetCloudDiscoveryReport(refresh): readable diagnostics for all providers.</summary>

		- Lists each provider and the resolved root or a not-found marker.
		- This is the recommended startup diagnostic for Ubuntu development environments with mounted Google Drive paths.
		- Console output remains reserved for startup-critical configuration failures.
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
		- Backup path composition stays on `RaiPath`: `GetBackupRelativeDirectoryPath(...)` returns `RaiPath`, and `backup(copy)` composes the destination as `Os.LocalBackupDir / relativePath`.
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
		<summary>ExecResult(timeoutMilliseconds): run and capture structured stdout/stderr results.</summary>

		- Returns `RaiSystemResult` with `StandardOutput`, `StandardError`, combined `Output`, exit code, and timeout state.
		- Supports argument-list based commands so higher-level wrappers do not need to hand-roll process execution.
		</details>
	- <details>
		<summary>Start(): async process execution.</summary>

		- Uses `RunProcessAsTask` to run command asynchronously.
		</details>
	- <details>
		<summary>CreateScript(path, name, content): create an executable script file.</summary>

		- Builds a `Script` instance that stores content through `TextFile` and marks Unix scripts executable.
		</details>
	</details>

- <details>
	<summary>SshSystem / SshFileProbe: reusable remote execution and remote file observation over ssh.</summary>

	- Responsibilities: execute remote commands and scripts through `ssh`, capture structured results via `RaiSystem`, and support remote file checks needed for integration diagnostics.
	- <details>
		<summary>SshSystem(target, remoteCommand): execute one remote command using argument-list-safe process launch.</summary>

		- Reuses `RaiSystem` instead of ad hoc `Process` code.
		- Provides `ExecuteRemoteCommand(...)` and `ExecuteScript(...)` for command and script execution.
		</details>
	- <details>
		<summary>SshFileProbe: remote file and directory observation helpers.</summary>

		- Supports `DirectoryExists`, `ReadFile`, `RemoveDirectory`, `WaitForFileContainingAll`, and `WaitForMissing`.
		- Intended for real-cloud integration tests and remote diagnostics where another synchronized node must be observed over ssh.
		</details>
	- <details>
		<summary>RemoteCloudSyncProbe: bind one local cloud root to one remote observer/root pair.</summary>

		- Resolves a local provider root through `Os.GetCloudStorageRoot(...)` and validates remote access through environment-driven ssh configuration.
		- Exposes `LocalCloudRoot`, `RemoteCloudRoot`, `Observer`, and relative-path helpers so OsLib and JsonPit tests can share the same real-cloud probe setup.
		</details>
	</details>

- <details>
	<summary>Script: executable script file abstraction.</summary>

	- Responsibilities: combine `TextFile` persistence with `RaiSystem` execution and Unix executable-mode handling.
	- <details>
		<summary>Script(path, name, content): create and persist a script file from text content.</summary>

		- Delegates content creation to the `TextFile` constructor and then applies executable mode where relevant.
		</details>
	- <details>
		<summary>Save(backup) / EnsureExecutable(): persist updates and restore executable mode.</summary>

		- `Save` reuses `TextFile.Save`; `EnsureExecutable` is a no-op on Windows and sets Unix execute bits elsewhere.
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
	<summary>CanonicalPath: deprecated canonical folder convention retained for compatibility.</summary>

	- <details>
		<summary>RootPath / FileStem: canonical path inputs.</summary>

		- `RootPath` is normalized; `FileStem` defines the canonical subfolder name.
		</details>
	- <details>
		<summary>Apply(): enforce canonical folder structure.</summary>

		- Resulting path is `RootPath/FileStem/`.
		- Prefer direct `RaiPath` composition for new code.
		</details>
	</details>

- <details>
	<summary>CanonicalFile: file using canonical-by-name storage convention.</summary>

	- <details>
		<summary>ConventionName: reports `CanonicalByName`.</summary>

		- Identifies active path convention.
		</details>
	- <details>
		<summary>ApplyPathConvention(): enforce canonical-by-name path behavior.</summary>

		- Maintains canonical layout behavior for compatibility with existing callers.
		</details>
	</details>

## architecture note

- Image-domain classes are intentionally maintained in a dedicated image package.
- OsLib remains responsible for generic file/path/process foundations and shared contracts.
