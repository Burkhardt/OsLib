# OsLib API Reference

This document provides a detailed, foldable overview of the current `OsLib 3.7.7` API surface.

Historical docs that mention `CloudStorageRootDir`, provider-precedence helper APIs, typed config wrappers, or public `LoadConfig(...)` behavior describe older package lines and should not be treated as current.

## core types

- <details>
	<summary>Os: OS-aware environment, lazy config access, and diagnostics.</summary>

	- Responsibilities: platform detection, intrinsic runtime path resolution, config-driven temp/backup resolution, separator and escaping helpers, and diagnostic logging.
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
		<summary>Config / IsConfigLoaded / DefaultConfigFileLocation: lazy config entry points.</summary>

		- `Config` exposes the current `dynamic` config object backed by `RAIkeep.json5`.
		- `IsConfigLoaded` reports whether that object has been materialized yet.
		- `DefaultConfigFileLocation` exposes the default config path used by bootstrap loading.
		- Config loading is lazy and internal; callers do not invoke a public `LoadConfig(...)` API.
		</details>
	- <details>
		<summary>TempDir / LocalBackupDir: config-driven directories.</summary>

		- `TempDir` resolves from config and falls back to `Path.GetTempPath()` if config cannot be read.
		- `LocalBackupDir` is optional. If it is not configured, backup features stay disabled.
		</details>
	- <details>
		<summary>NormPath(path), NormSeperator(value), and Escape(value, mode): path helpers.</summary>

		- Normalize separators and apply the selected escaping mode.
		- Supports `noEsc`, `blankEsc`, `paramEsc`, and `backslashed` escape modes.
		</details>
	</details>

- <details>
	<summary>CloudPathWiring: delegate bridge between `Os.Config` and `RaiPath`.</summary>

	- Responsibilities: initialize `RaiPath.CloudEvaluator` from the current config contract.
	- There is no public provider-enum selection API in `Os`; cloud path classification is delegated to `RaiPath` through this wiring layer.
	</details>

- <details>
	<summary>RaiPath: directory path value object with buffered cloud state.</summary>

	- Responsibilities: keep directory path semantics, preserve trailing separator behavior, classify cloud-backed paths, and own directory wait behavior.
	- <details>
		<summary>Path / Cloud: normalized directory path and buffered cloud classification.</summary>

		- Setting `Path` ensures directory semantics by clearing file components internally.
		- `Cloud` is buffered when the path is set and is driven by `CloudEvaluator`.
		</details>
	- <details>
		<summary>CloudEvaluator: delegate firewall for cloud awareness.</summary>

		- Defaults to `false` until initialized.
		- `CloudPathWiring.Initialize()` assigns the production evaluator from `Os.Config`.
		</details>
	- <details>
		<summary>operator /(self, subDir) and Parent: directory composition helpers.</summary>

		- Builds new `RaiPath` values while preserving directory semantics.
		</details>
	- <details>
		<summary>mkdir() / rmdir(...): directory mutation and cloud-aware waits.</summary>

		- `mkdir` waits for materialization only when the buffered `Cloud` flag is true.
		- `rmdir` waits for vanishing only when the buffered `Cloud` flag is true.
		</details>
	</details>

- <details>
	<summary>RaiFile: generic file utility with cloud-aware file waits.</summary>

	- Responsibilities: parse and compose file identity, perform IO operations, and own file wait behavior.
	- <details>
		<summary>Name / Ext / Path / FullName: core file identity properties.</summary>

		- `Path` copies the buffered `Cloud` flag from the assigned `RaiPath`.
		</details>
	- <details>
		<summary>Exists(), rm(), cp(), mv(...): existence, delete, copy, and move lifecycle.</summary>

		- `rm()` waits for vanishing only when the buffered `Cloud` flag is true.
		</details>
	- <details>
		<summary>AwaitVanishing() / AwaitMaterializing(...): public wrappers over file wait logic.</summary>

		- These stay on `RaiFile` because they are about physical file latency, not directory latency.
		</details>
	- <details>
		<summary>FileAge / DefaultSyncPropagationDelayMs / BackdateCreationTime(...): deterministic file-age control.</summary>

		- `FileAge` is derived from `CreationTimeUtc`.
		- `BackdateCreationTime(...)` writes a best-effort sentinel file, waits for propagation, and removes the sentinel after the wait.
		- Delay precedence is explicit parameter, then `Os.Config.SyncPropagationDelayMs`, then `DefaultSyncPropagationDelayMs`.
		</details>
	- <details>
		<summary>mkdir(), rmdir(...), backup(copy): directory materialization and backup helpers.</summary>

		- Directory creation/deletion delegates to `RaiPath`.
		- `backup(copy)` composes the destination below `Os.LocalBackupDir` when backups are enabled.
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
	<summary>SshSystem: reusable remote execution over ssh.</summary>

	- Responsibilities: execute remote commands and scripts through `ssh` and read remote `RAIkeep.json5` content when needed.
	- <details>
		<summary>SshSystem(target, remoteCommand): execute one remote command using argument-list-safe process launch.</summary>

		- Reuses `RaiSystem` instead of ad hoc `Process` code.
		- Provides `ExecuteRemoteCommand(...)`, `ExecuteScript(...)`, and `ReadRemoteConfigJson5(...)`.
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
