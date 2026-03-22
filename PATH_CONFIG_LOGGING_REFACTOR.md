# OsLib Path, Config, and Logging Refactor Design

Date: 2026-03-21

## Intent

This refactor separates intrinsic OS/runtime paths from application configuration, removes normal library console chatter, and adds structured diagnostics suitable for server applications.

## Directory Semantics

Intrinsic values, never config-driven:

- `Os.UserHomeDir : RaiPath`
  - The real OS user home directory.
  - Derived from environment variables and OS APIs only.
- `Os.AppRootDir : RaiPath`
  - The runtime meaning of `.`.
  - Derived from `Directory.GetCurrentDirectory()` only.

Config-driven values:

- `Os.TempDir : RaiPath`
- `Os.LocalBackupDir : RaiPath`
- cloud provider root directories
- future storage/workspace directories

Compatibility decision:

- Existing string-based path helpers stay available only where needed for compatibility.
- The production-facing `*Dir` properties move to `RaiPath` so call sites compose with `RaiPath` and `RaiFile` rather than `Path.Combine`.

## Config Model

Config file location remains:

- macOS/Linux: `~/.config/RAIkeep/osconfig.json`
- Windows: `%APPDATA%\RAIkeep\osconfig.json`

Behavioral changes:

- Missing `osconfig.json` is startup-critical.
- Startup may continue in degraded mode with intrinsic/fallback values.
- Missing config no longer silently normalizes the problem away as acceptable runtime state.
- Legacy `homeDir` config is treated as deprecated compatibility input and is not used to resolve `UserHomeDir`.

## Degraded Startup Rules

Startup-critical conditions:

- `osconfig.json` missing
- `osconfig.json` unreadable
- `osconfig.json` malformed
- cloud configuration unavailable when a preferred cloud root is required

Startup-critical conditions must:

- log an error through `ILogger<T>`
- emit a clear console error message
- state whether startup continues in degraded mode

Log-only conditions:

- `TempDir` missing from config and OS temp fallback used
- `LocalBackupDir` missing from config and fallback chosen
- deprecated config fields encountered
- probe failures and path normalization details

## Logging Strategy

Primary logging contract:

- library diagnostics are written via `Microsoft.Extensions.Logging.ILogger<T>`
- message templates are structured and never use interpolation

Practical migration strategy in this pass:

- new diagnostics go through a DI-configurable logging bridge exposed by `Os`
- server hosts can wire the bridge from DI during startup
- static `Os` remains as a compatibility facade for the existing codebase while logging categories remain `ILogger<T>` based

This keeps the current solution working while giving host applications a clean path to configure logging centrally through Serilog at the application layer.

## Fallback Strategy

- `UserHomeDir` always resolves from OS state.
- `AppRootDir` always resolves from current process working directory.
- `TempDir` prefers config, then falls back to OS temp.
- `LocalBackupDir` prefers configured non-cloud path, then OS-local candidates, then OS temp backup path.
- `CloudStorageRootDir` prefers configured/discovered provider roots and throws if no provider can be resolved.

Every fallback logs a structured diagnostic.

## Platform Abstractions

Current behavior:

- `IsUnixLike` is true on macOS and Ubuntu/Linux.
- `IsLinuxLike` is only true on Ubuntu/Linux.

Conclusion:

- `IsUnixLike` still has real value for shared non-Windows behavior.
- `IsLinuxLike` is too narrow as a name for Linux-specific behavior because macOS is Unix-like but not Linux.
- This pass keeps the existing names for compatibility and adds more explicit `IsMacOS` and `IsWindows` helpers where path/runtime logic benefits from clearer intent.

## RaiPath vs System.IO.Path

Policy:

- prefer `RaiPath` and `RaiFile` at call sites and tests
- keep `System.IO.Path`, `File`, and `Directory` usage inside low-level path and file primitives where that is the actual platform boundary
- where higher-level code still needs `Path.Combine` or similar outside the low-level primitives, annotate the usage briefly

## Test Scope

Tests added or updated in this pass cover:

- `UserHomeDir` resolution without config
- `AppRootDir` resolution
- `~` config path resolution
- missing config degraded startup behavior
- malformed config degraded startup behavior
- `TempDir` fallback
- `LocalBackupDir` fallback
- cloud-root degraded behavior
- structured logging capture
- startup-critical console diagnostics
- platform helper semantics