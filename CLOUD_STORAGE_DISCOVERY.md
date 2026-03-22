# Cloud Storage Discovery

OsLib resolves cloud roots through `Os.Config`, backed by `osconfig.json` and refreshed into an in-memory cache.

The file is no longer treated as optional noise. Missing, unreadable, or malformed `osconfig.json` is a startup-critical configuration problem. OsLib can continue in degraded mode with intrinsic and fallback paths, but it logs an error and emits an explicit console startup diagnostic so operators can correct the configuration.

Supported-and-documented providers in the `3.5.0` package line:
- `Dropbox`
- `OneDrive`
- `GoogleDrive`

## Current Model

The active configuration file is `osconfig.json`.

Default locations:
- macOS / Linux: `~/.config/RAIkeep/osconfig.json`
- Windows: `%APPDATA%\RAIkeep\osconfig.json`

`Os.Config` wraps that file through `OsConfigFile`, and `Os.LoadConfig()` returns the typed `OsConfigModel`.

The config is broader than cloud-only state. It can hold:
- `tempDir`
- `localBackupDir`
- `defaultCloudOrder`
- `cloud.dropbox`
- `cloud.onedrive`
- `cloud.googledrive`

Notes:

- `UserHomeDir` is intrinsic and no longer config-driven.
- Legacy `homeDir` values are accepted for compatibility, ignored at runtime, and logged as deprecated.
- `AppRootDir` is intrinsic and resolves the runtime meaning of `.`.

## Public API

In `Os`:
- `Config`: the reusable `OsConfigFile` wrapper
- `LoadConfig(bool refresh = false)`: load or reload the typed `OsConfigModel`
- `GetDefaultConfigPath()`: resolve the platform-specific config file path
- `UserHomeDir`: intrinsic OS user home as `RaiPath`
- `AppRootDir`: current working directory as `RaiPath`
- `TempDir`: effective temp directory as `RaiPath`
- `LocalBackupDir`: effective non-cloud backup directory as `RaiPath`
- `CloudStorageRootDir`: preferred cloud root as `RaiPath`
- `GetCloudStorageRoots(bool refresh = false)`: return all effective provider roots
- `GetCloudStorageRoot(CloudStorageType provider, bool refresh = false)`: return one provider root
- `GetPreferredCloudStorageRoot(params CloudStorageType[] preferredOrder)`: resolve a custom provider order
- `GetCloudStorageRootDir(CloudStorageType provider, bool refresh = false)`: return one provider root as `RaiPath`
- `GetPreferredCloudStorageRootDir(params CloudStorageType[] preferredOrder)`: resolve a custom provider order as `RaiPath`
- `ResetCloudStorageCache()`: clear the effective root cache
- `GetCloudDiscoveryReport(bool refresh = false)`: emit readable diagnostics
- `GetCloudStorageSetupGuidance()`: emit a short setup hint that points here

Default preferred order:
1. `OneDrive`
2. `Dropbox`
3. `GoogleDrive`

## Discovery Order

For each provider, OsLib resolves roots in this order:
1. configured value from `Os.Config`
2. OS/provider-specific probing

First valid existing directory wins per provider.

Discovered provider roots are merged back into `osconfig.json` only when the corresponding config entry is currently empty. That keeps the file useful as durable machine-local state without overwriting explicit user choices.

## JSON Format

Example `osconfig.json`:

```json
{
	"tempDir": "/var/folders/.../T/",
	"localBackupDir": "/Users/me/Library/Application Support/OsLib/Backup/",
	"defaultCloudOrder": ["OneDrive", "Dropbox", "GoogleDrive"],
	"cloud": {
		"dropbox": "/Users/me/Library/CloudStorage/Dropbox/",
		"onedrive": "/Users/me/Library/CloudStorage/OneDrive-Contoso/",
		"googledrive": "/Users/me/Library/CloudStorage/GoogleDrive-me@example.com/My Drive/"
	}
}
```

The cloud section remains intentionally simple so other tools and languages can share it easily.

## OS-Specific Probing

### macOS

Probes include:
- Dropbox: `~/.dropbox/info.json`, `~/Dropbox`, `~/Library/CloudStorage/Dropbox`
- OneDrive: `~/OneDrive*`, `~/Library/CloudStorage/OneDrive*`
- Google Drive: `~/Library/CloudStorage/GoogleDrive*`, preferring `My Drive` when present, then `~/GoogleDrive` and `~/Google Drive`

### Windows

Probes include:
- Dropbox: `%APPDATA%\Dropbox\info.json`, `%LOCALAPPDATA%\Dropbox\info.json`
- OneDrive: common `OneDrive*` locations under the user profile and cloud-storage folder
- Google Drive: `%USERPROFILE%\Google Drive`, `%USERPROFILE%\My Drive`

### Ubuntu / Linux

Probes include:
- Dropbox: `~/.dropbox/info.json`, `~/Dropbox`
- OneDrive: `~/OneDrive`, `~/OneDrive - Personal`
- Google Drive: `~/Google Drive`, `~/GoogleDrive`

For Linux machines with custom cloud mounts, edit `osconfig.json` directly instead of relying on probes.

## Local Backup Behavior

`Os.LocalBackupDir` now comes from config first, then falls back to OS-local defaults. If the configured backup directory is itself inside a discovered cloud root, OsLib rejects it and falls back to a non-cloud local path.

Fallback selection is logged through `ILogger<T>` as a serious configuration warning, but it does not print to the console during normal operation.

## Logging And Startup Diagnostics

OsLib path and config diagnostics use `ILogger<T>` message templates.

Normal behavior:

- no library console chatter
- fallback path usage is log-only
- path resolution details are debug/info/warning logs depending on severity

Startup-critical behavior:

- missing `osconfig.json`
- unreadable `osconfig.json`
- malformed `osconfig.json`
- missing preferred cloud root when a cloud root is required

These conditions log errors and also emit a console startup diagnostic that explicitly says startup continues in degraded mode if that is the chosen behavior.

## Cloud-Aware File IO

`RaiFile` treats a path as cloud-backed when it falls under one of the effective provider roots returned by `GetCloudStorageRoots()`.

That behavior now applies consistently whether the root came from:
- explicit config
- probing
- cached discovered state restored through `Os.Config`

## Recommended Setup

For stable machine behavior:
1. Create and maintain `osconfig.json` explicitly instead of relying on silent runtime creation.
2. Edit `cloud.*` entries for any provider whose probe path is not the path you want to standardize on.
3. Set `localBackupDir` if you need backups in a specific non-cloud location.
4. Call `ResetCloudStorageCache()` after changing config at runtime.
5. Use `GetCloudDiscoveryReport()` in diagnostics when onboarding a new machine.
