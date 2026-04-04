# Cloud Storage Discovery

Current contract note: the active OsLib config contract is `osconfig.json5` with PascalCase property names and direct dynamic access. See [OSCONFIG-BREAKING-CHANGE.md](OSCONFIG-BREAKING-CHANGE.md) for the current authoritative behavior. Historical references below to `osconfig.json`, camelCase keys, or typed config wrappers describe older behavior.

OsLib resolves cloud roots through `Os.Config`, backed by `osconfig.json5` and refreshed into an in-memory cache.

Cloud support is optional. OsLib can be used for purely local file and path operations without any configured cloud provider. When cloud-specific configuration is missing or malformed, non-cloud behavior should still continue through intrinsic and fallback paths, while cloud-specific APIs degrade gracefully.

Supported-and-documented providers in the `3.5.0` package line:
- `Dropbox`
- `OneDrive`
- `GoogleDrive`

## Current Model

The active configuration file is `osconfig.json5`.

Default locations:
- macOS / Linux: `~/.config/RAIkeep/osconfig.json5`
- Windows: `%APPDATA%\RAIkeep\osconfig.json5`

`Os.Config` exposes the active dynamic config object, and `Os.LoadConfig()` reads that file into the current in-memory config state.

The config is broader than cloud-only state. It can hold:
- `TempDir`
- `LocalBackupDir`
- `DefaultCloudOrder`
- `Cloud.Dropbox`
- `Cloud.OneDrive`
- `Cloud.GoogleDrive`

Notes:

- `UserHomeDir` is intrinsic and no longer config-driven.
- `AppRootDir` is intrinsic and resolves the runtime meaning of `.`.
- `DefaultCloudOrder` is optional. If it is missing, OsLib falls back to `OneDrive`, `Dropbox`, `GoogleDrive`.
- If `DefaultCloudOrder` contains unsupported provider names, OsLib ignores them.

## Public API

In `Os`:
- `Config`: the active dynamic config object
- `LoadConfig()`: load or reload config from disk into the current process
- `ConfigFileFullName`: resolve the active config file path
- `UserHomeDir`: intrinsic OS user home as `RaiPath`
- `AppRootDir`: current working directory as `RaiPath`
- `TempDir`: effective temp directory as `RaiPath`
- `LocalBackupDir`: effective non-cloud backup directory as `RaiPath`
- `CloudStorageRootDir`: preferred cloud root as `RaiPath`
- `GetCloudStorageRoot(Cloud provider, bool refresh = false)`: return one provider root as `RaiPath`, or `null` when unavailable
- `GetCloudDiscoveryReport(bool refresh = false)`: emit readable diagnostics
- `GetCloudStorageSetupGuidance()`: emit a short setup hint that points here

Default preferred order:
1. `OneDrive`
2. `Dropbox`
3. `GoogleDrive`

## Discovery Order

For each supported provider, OsLib currently resolves roots from the configured `Cloud.*` values in `Os.Config`.

Provider selection order is determined like this:
1. values from `DefaultCloudOrder`, if present and valid
2. built-in fallback order: `OneDrive`, `Dropbox`, `GoogleDrive`

Unsupported provider names in `DefaultCloudOrder` are ignored.

If no provider root is configured for a given provider, `GetCloudStorageRoot(...)` returns `null` for that provider.

## JSON Format

Example `osconfig.json5`:

```json5
{
	TempDir: "/var/folders/.../T/",
	LocalBackupDir: "/Users/me/Library/Application Support/OsLib/Backup/",
	DefaultCloudOrder: ["OneDrive", "Dropbox", "GoogleDrive", "ICloud"],
	Cloud: {
		Dropbox: "/Users/me/Library/CloudStorage/Dropbox/",
		OneDrive: "/Users/me/Library/CloudStorage/OneDrive-Contoso/",
		GoogleDrive: "/Users/me/Library/CloudStorage/GoogleDrive-me@example.com/My Drive/"
	}
}
```

In that example, `ICloud` in `DefaultCloudOrder` is ignored because it is not a supported OsLib provider.

## Cloud Optionality

If no cloud provider is configured:
- `IsCloudPath(...)` returns `false`
- `RaiFile.Cloud` stays `false`
- `GetCloudStorageRoot(provider)` returns `null`
- cloud-specific tests should skip rather than fail when they require an actual configured cloud root

If a cloud provider is configured but its path is empty, invalid, or not recognized as a cloud-backed path, cloud-aware behavior should stay inactive for that path.

## Local Backup Behavior

`Os.LocalBackupDir` now comes from config first, then falls back to OS-local defaults. If the configured backup directory is itself inside a configured cloud root, OsLib rejects it and falls back to a non-cloud local path.

Fallback selection is logged through `ILogger<T>` as a serious configuration warning, but it does not print to the console during normal operation.

## Logging And Startup Diagnostics

OsLib path and config diagnostics use `ILogger<T>` message templates.

Normal behavior:

- no library console chatter
- fallback path usage is log-only
- path resolution details are debug/info/warning logs depending on severity

Config-read failures such as missing or malformed `osconfig.json5` are logged as degraded-mode startup diagnostics.

Cloud-specific failure semantics are narrower:
- if no cloud root is configured, generic non-cloud APIs should keep working
- `IsCloudPath(...)` should simply return `false`
- `GetCloudStorageRoot(...)` should return `null`
- `CloudStorageRootDir` is the stricter API and may throw when code explicitly requires a preferred cloud root and none can be resolved

## Cloud-Aware File IO

`RaiFile` treats a path as cloud-backed when it falls under one of the effective configured provider roots.

If no configured provider root matches, the path is treated as local.

## Recommended Setup

For stable machine behavior:
1. Create and maintain `osconfig.json5` explicitly when you want cloud-aware behavior.
2. Edit `Cloud.*` entries for any provider you want OsLib to treat as cloud-backed.
3. Set `localBackupDir` if you need backups in a specific non-cloud location.
4. Keep `DefaultCloudOrder` if you care about preferred-provider selection, but do not depend on it being exhaustive.
5. Use `GetCloudDiscoveryReport()` in diagnostics when onboarding a new machine.
