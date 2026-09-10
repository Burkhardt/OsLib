# OsLib API Reference

This document provides a detailed, foldable overview of the current `OsLibCore 4.2.9` API surface, including accepted CR022 cloud-safe file and directory behavior.

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

		- `TempDir` comes from the immutable runtime configuration snapshot. Its first initialization performs a one-time OsLib `TmpFile` create/write/remove probe and fails fast if the configured directory does not exist or is not writable.
		- `TempDir` does not mutate `Os.Config`, bypass it, or fall back to a raw `System.IO` path.
		- `LocalBackupDir` is optional. If it is not configured, backup features stay disabled.
		</details>
	- <details>
		<summary>NormPath(path), NormSeperator(value), and Escape(value, mode): path helpers.</summary>

		- Normalize separators and apply the selected escaping mode.
		- Supports `noEsc`, `blankEsc`, `paramEsc`, and `backslashed` escape modes.
		</details>
	</details>

- <details>
	<summary>RaiPathException and RaiPathNotFoundException: path-domain failures.</summary>

	- `RaiPathException` is the OsLib path-error base type.
	- `RaiPathNotFoundException` reports a missing `RaiPath` boundary without exposing raw `System.IO.FileNotFoundException` to consumers.
	</details>

- <details>
	<summary>RaiCloudStorageException: rejected cloud-path operations.</summary>

	- Derives from `IOException` and is thrown before mutation when a RaiFile/RaiPath operation would violate the CR022 cloud-storage invariant.
	- `Operation`, `SourcePath`, and `DestinationPath` identify the rejected operation without requiring consumers to parse an operating-system message.
	- Guarded cases include TempDir-to-cloud file/directory moves and replacement of an existing cloud-backed directory.
	</details>

- <details>
	<summary>RaiFile.WriteFromAsync(chunks, cancellationToken): stream-free asynchronous ingestion.</summary>

	- Accepts `IAsyncEnumerable&lt;byte[]&gt;` so callers can write chunked content without taking a direct dependency on `System.IO.Stream`.
	- Honors cancellation and retains RaiFile's established path and lifecycle behavior.
	</details>

- <details>
	<summary>RaiFile.ReadAllBytesAsync(cancellationToken) and RaiFileIOException.</summary>

	- Reads the complete file asynchronously through the OsLib file boundary.
	- Wraps operating-system `IOException` and `UnauthorizedAccessException` failures in `RaiFileIOException`, retaining the original exception as `InnerException` and exposing the affected `FileName`.
	- `RaiFileIOException` derives from `IOException` for existing catch compatibility. Cancellation remains an `OperationCanceledException` and is not wrapped.
	</details>

- <details>
	<summary>CloudPathWiring: compatibility initializer for older callers.</summary>

	- Responsibilities: keep the historical `Initialize()` entry point available without mutating runtime state.
	- Cloud path classification now flows from `RaiPath` into the immutable `Os` runtime snapshot.
	</details>

- <details>
	<summary>RaiPath: directory path value object with buffered cloud state.</summary>

	- Responsibilities: keep directory path semantics, preserve trailing separator behavior, classify cloud-backed paths, and own directory wait behavior.
	- <details>
		<summary>Path / Cloud: normalized directory path and buffered cloud classification.</summary>

		- Setting `Path` ensures directory semantics by clearing file components internally.
		- `Cloud` is buffered when the path is set and is driven by the immutable `Os` runtime snapshot.
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
	- <details>
		<summary>mv / cp / backup: cloud-safe directory operations.</summary>

		- A move from `Os.TempDir` into a cloud-backed destination fails before either tree is changed.
		- Replacing an existing cloud-backed directory by move or recursive copy is prohibited; no child is deleted as an implementation detail.
		- A cloud-directory backup copies the tree and leaves its live pathname present.
		- An intentional move to a new destination remains available.
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
		- `cp(source)` opens and overwrites the destination in place; it never calls `rm()` first. Operating-system failures surface as `RaiFileIOException`.
		- `mv(source, replace: true)` updates an existing cloud destination in place and removes the source only after copying succeeds.
		- Moving a temporary file into a cloud destination fails with `RaiCloudStorageException` before path creation or source removal.
		</details>
	- <details>
		<summary>AwaitVanishing() / AwaitMaterializing(...): public wrappers over file wait logic.</summary>

		- These stay on `RaiFile` because they are about physical file latency, not directory latency.
		</details>
	- <details>
		<summary>LastWriteTimeUtc: physical file modification time.</summary>

		- Reads the filesystem's UTC last-write timestamp through `RaiFile`, keeping consumers independent of direct `System.IO.FileInfo` access.
		</details>
	- <details>
		<summary>FileAge / DefaultSyncPropagationDelayMs / BackdateCreationTime(...): deterministic file-age control.</summary>

		- `FileAge` is derived from `CreationTimeUtc`.
		- `BackdateCreationTime(...)` writes a best-effort sentinel file, waits for propagation, and removes the sentinel after the wait.
		- Delay precedence is explicit parameter, then `Os.Config.SyncPropagationDelayMs`, then `DefaultSyncPropagationDelayMs`.
		</details>
	- <details>
		<summary>mkdir(), rmdir(...), backup(copy): directory materialization and backup helpers.</summary>

		- `mkdir()` is virtual so derived `RaiFile` types can override directory creation through normal polymorphic dispatch.
		- Directory creation/deletion delegates to `RaiPath` in the base implementation.
		- `backup(copy)` composes the destination below `Os.LocalBackupDir` when backups are enabled.
		- A cloud source is copied to backup even when the compatibility API receives `copy: false`; its live pathname is not relocated.
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
		<summary>Read(), Save(backup), SaveInPlace(): load and persist lines.</summary>

		- `Save` can optionally create backups and handles cloud materialization semantics.
		- `SaveInPlace` writes directly to the target path without deleting or renaming it first; use it for small coordination files on cloud-synced paths.
		</details>
	- <details>
		<summary>Append(), Insert(), Delete(), Sort(): editing helpers.</summary>

		- Mutation operations mark file state as changed.
		</details>
	- <details>
		<summary>Explicit-extension construction with dotted logical names.</summary>

		- A non-default explicit extension preserves the complete logical stem, so `new TextFile(path, "Nkosikazi-AIA.Api-93455", "flag")` resolves to `Nkosikazi-AIA.Api-93455.flag`.
		- The tuple constructor `(Name, Ext)` is unambiguous even for a dotted stem whose explicit extension is `txt`.
		- Existing implicit filename parsing such as `new TextFile(path, "settings.json")` remains unchanged.
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
	<summary>IorgCommand: typed ImgSeeder/iorg process boundary.</summary>

	- `IorgListRequest(FileNamePattern, Root)` carries a filename pattern plus optional subscriber, cloud, JSON, quiet, debug, and no-logo settings.
	- `IorgMoveRequest(SourceItemId, TargetItemId, Root, PathConvention)` carries exact ItemId relocation or rename intent; `TargetItemId` may be omitted.
	- `BuildListArguments(...)` and `BuildMoveArguments(...)` expose deterministic argument arrays for verification.
	- `List` / `ListAsync` and `Move` / `MoveAsync` execute through `CliCommand` and `RaiSystem`; no shell interpolation is used.
	- `IorgCleanRequest` distinguishes exact-ItemId cleanup from explicit subscriber-wide cache cleanup.
	</details>

- <details>
	<summary>PathConventionType: compatible item-tree layout selection.</summary>

	- Values remain compatibility-safe: `CanonicalByName`, `ItemIdTree3x3`, `ItemIdTree8x2`, then `Flat`.
	- CLI numbers are one-based in that same order, making `ItemIdTree8x2` option 3 and `Flat` option 4.
	</details>

- <details>
	<summary>RaiPath recursive enumeration boundary.</summary>

	- `EnumerateFiles(searchPattern, recursive)` keeps consumer packages on `RaiPath`/`RaiFile` while OsLib alone owns the underlying platform traversal choice.
	</details>

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

		- Returns `RaiSystemResult` with `StandardOutput`, `StandardError`, combined `Output`, exact tokenized `ArgumentList`, exit code, derived `Succeeded`, and timeout state.
		- Supports argument-list based commands so higher-level wrappers do not need to hand-roll process execution.
		</details>
	- <details>
		<summary>ExecAsync(timeoutMilliseconds, cancellationToken): asynchronous execution with distinct timeout and caller-cancellation behavior.</summary>

		- Preserves the established off-caller-thread process-start contract and awaits process exit without blocking that caller.
		- Kills the process tree on timeout or cancellation.
		- A timeout returns a result with `TimedOut == true`; caller cancellation remains `OperationCanceledException`.
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

	- Responsibilities: executable resolution, package-manager install/update hints, exact tokenized arguments, and `RaiSystem`-backed execution.
	- <details>
		<summary>CliCommand: base abstraction for local CLI tools.</summary>

		- Resolves a working executable from candidate names or explicit paths.
		- Exposes sync/async execution through string-compatible and tokenized `Run(...)` and `RunAsync(...)` overloads.
		- Tokenized overloads accept an optional timeout; completed results retain the exact original argument vector.
		- Tokenized calls flow through `ProcessStartInfo.ArgumentList`, preserving argument count and values without shell reconstruction.
		- `BuildPosixShellCommand(...)` safely serializes the executable and argument tokens when an SSH boundary explicitly requires POSIX shell text.
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
	- <details>
		<summary>PitsCommand: typed invocation of the installed `pits` CLI.</summary>

		- `PitsTarget.Pit(...)` and `PitsTarget.Wwwa()` make the target mode explicit.
		- `PitsSeedRequest`, `PitsExportRequest`, `PitsAuditRequest`, `PitsDeletePropertyRequest`, `PitsDeleteItemRequest`, `PitsMaintainRequest`, and `PitsCommandOptions` model the preferred 4.x commands and global options.
		- `BuildMaintainArguments(...)`, `Maintain(...)`, and `MaintainAsync(...)` expose report/apply maintenance, explicit process-flag pruning with an age, and explicit legacy-extension repair.
		- All typed calls targeting the same provider/root/pit are serialized across every `PitsCommand` instance in the current process. WWWA takes the fixed `Person`, `Object`, `Place`, `Activity` gate order; unrelated pits may run concurrently and queued cancellation launches no child.
		- `PitsExportRequest.At` optionally requests CR017 point-in-time projection. The value is emitted as a canonical UTC timestamp in a separate `--at` argument token; omitting it preserves the established export argument vector.
		- `BuildSeedArguments`, `BuildExportArguments`, `BuildAuditArguments`, `BuildDeletePropertyArguments`, and `BuildDeleteItemArguments` validate required and mutually exclusive values before process start.
		- `DeleteProperty` uses an explicit dot-delimited `PropertyPath`; malformed paths are rejected before process execution.
		- `Seed`, `Export`, `Audit`, `DeleteProperty`, and `DeleteItem`, with async counterparts, return `RaiSystemResult` containing success/timeout state, original argument tokens, exit code, and separated standard output/error.
		- `ForManagedAssembly(...)` supports package-owned entry-point testing through `dotnet pits.dll` without hand-built process launch code.
		</details>
	- <details>
		<summary>IorgCommand: typed invocation of the installed `iorg` CLI.</summary>

		- `IorgOrganizeRequest`, `IorgCleanRequest`, and `IorgCommandOptions` model the preferred 4.x `organize` and `clean` commands.
		- Required source/root/short-name values and numbered path/naming conventions are validated before process start.
		- `BuildOrganizeArguments`, `BuildCleanArguments`, `Organize`, and `Clean` expose deterministic sync/async calls.
		- `ForManagedAssembly(...)` supports package-owned entry-point testing through `dotnet ImgSeeder.dll`.
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
