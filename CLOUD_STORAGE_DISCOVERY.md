# Cloud Storage Discovery

Current contract note: the active OsLib config contract is `osconfig.json5` with PascalCase property names and direct dynamic access. See [OSCONFIG-BREAKING-CHANGE.md](OSCONFIG-BREAKING-CHANGE.md) for the current authoritative behavior. Historical references below to `osconfig.json`, camelCase keys, or typed config wrappers describe older behavior.

OsLib resolves config-driven paths through `Os.Config`, backed by `osconfig.json5` and refreshed into an in-memory cache.

Current startup contract:
- `osconfig.json5` is mandatory
- `TempDir` is mandatory and must already exist and allow read/write probes
- cloud support is optional, but every configured cloud root must already exist and allow read/write probes
- `LocalBackupDir` is optional; when it is absent or unusable, backup features are disabled instead of silently falling back
- configured observers are optional; when present, SSH access plus remote config and remote path probes must succeed

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
- `Observers[].Name`
- `Observers[].SshTarget`

Notes:

- `UserHomeDir` is intrinsic and no longer config-driven.
- `AppRootDir` is intrinsic and resolves the runtime meaning of `.`.
- `DefaultCloudOrder` is optional. If it is missing, OsLib still prefers `OneDrive`, `Dropbox`, `GoogleDrive` when choosing among already validated configured roots.
- If `DefaultCloudOrder` contains unsupported provider names, OsLib ignores them.
- `TempDir` is mandatory. Missing or unusable `TempDir` aborts startup.
- `LocalBackupDir` is optional. Missing or unusable `LocalBackupDir` disables backup features.
- `Cloud.*` entries are optional as a group. If no cloud root is configured, OsLib stays local-only.
- Any configured observer must be reachable via SSH, provide a readable remote `osconfig.json5`, and pass remote `TempDir` plus configured-cloud-root probes.

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
If a provider is explicitly listed in `DefaultCloudOrder`, its `Cloud.*` entry must be present and usable or startup aborts.

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
- OsLib logs a reduced-features warning with guidance to this document

If any cloud provider is configured, each configured root must exist and support read/write probes. A configured-but-unusable cloud root is a startup failure.

## Local Backup Behavior

`Os.LocalBackupDir` is optional.

When it is configured:
- it must exist
- it must allow read/write probes
- it must not be inside a configured cloud root

When it is absent or unusable, OsLib disables backup features and logs a reduced-features warning with guidance to this document. It does not silently fall back to `TempDir` or any other path.

## Logging And Startup Diagnostics

OsLib path and config diagnostics use `ILogger<T>` message templates.

Normal behavior:

- no library console chatter
- fallback path usage is log-only
- path resolution details are debug/info/warning logs depending on severity

Config-read failures such as missing or malformed `osconfig.json5` are startup-fatal and write both structured logs and a console startup diagnostic.

Validation semantics are:
- missing or unusable `TempDir`: startup-fatal
- no cloud configured at all: reduced-features warning only
- configured cloud root missing or unusable: startup-fatal
- missing or unusable `LocalBackupDir`: backup feature disabled, reduced-features warning only
- configured observer unreachable or remote config invalid/unusable: startup-fatal
- `CloudStorageRootDir` remains the stricter API and throws when code explicitly requires a preferred cloud root and none is configured

## Cloud-Aware File IO

`RaiFile` treats a path as cloud-backed when it falls under one of the effective configured provider roots.

If no configured provider root matches, the path is treated as local.

## Recommended Setup

For stable machine behavior:
1. Create and maintain `osconfig.json5` before starting any app that uses OsLib.
2. Set `TempDir` to an existing writable directory.
3. Edit `Cloud.*` entries only for providers you actually want to use, and make sure each configured root already exists and is writable.
4. Set `LocalBackupDir` only when you want local backups and the directory is truly local and writable.
5. Configure `Observers` only when SSH access and remote `osconfig.json5` are already working. See `OsLib/SSH_SETUP.md`.
6. Use `GetCloudDiscoveryReport()` in diagnostics when onboarding a new machine.
