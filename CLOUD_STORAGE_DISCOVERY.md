# Cloud Storage Discovery

OsLib now resolves cloud roots through `Os.Config`, backed by a JSON config file that is created on demand and refreshed into an in-memory cache.

Supported providers:
- `Dropbox`
- `OneDrive`
- `GoogleDrive`
- `ICloud`

## Current Model

The active configuration file is `osconfig.json`.

Default locations:
- macOS / Linux: `~/.config/RAIkeep/osconfig.json`
- Windows: `%APPDATA%\RAIkeep\osconfig.json`

`Os.Config` wraps that file through `OsConfigFile`, and `Os.LoadConfig()` returns the typed `OsConfigModel`.

The config is broader than cloud-only state. It can hold:
- `homeDir`
- `tempDir`
- `localBackupDir`
- `defaultCloudOrder`
- `cloud.dropbox`
- `cloud.onedrive`
- `cloud.googledrive`
- `cloud.icloud`

## Public API

In `Os`:
- `Config`: the reusable `OsConfigFile` wrapper
- `LoadConfig(bool refresh = false)`: load or reload the typed `OsConfigModel`
- `GetDefaultConfigPath()`: resolve the platform-specific config file path
- `GetCloudStorageRoots(bool refresh = false)`: return all effective provider roots
- `GetCloudStorageRoot(CloudStorageType provider, bool refresh = false)`: return one provider root
- `GetPreferredCloudStorageRoot(params CloudStorageType[] preferredOrder)`: resolve a custom provider order
- `CloudStorageRoot`: resolve the preferred provider root by default order
- `ResetCloudStorageCache()`: clear the effective root cache
- `GetCloudDiscoveryReport(bool refresh = false)`: emit readable diagnostics
- `GetCloudStorageSetupGuidance()`: emit a short setup hint that points here

Default preferred order:
1. `GoogleDrive`
2. `ICloud`
3. `Dropbox`
4. `OneDrive`

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
	"homeDir": "/Users/me",
	"tempDir": "/var/folders/.../T/",
	"localBackupDir": "/Users/me/Library/Application Support/OsLib/Backup/",
	"defaultCloudOrder": ["GoogleDrive", "ICloud", "Dropbox", "OneDrive"],
	"cloud": {
		"dropbox": "/Users/me/Library/CloudStorage/Dropbox/",
		"onedrive": "/Users/me/Library/CloudStorage/OneDrive-Contoso/",
		"googledrive": "/Users/me/Library/CloudStorage/GoogleDrive-me@example.com/My Drive/",
		"icloud": "/Users/me/Library/Mobile Documents/com~apple~CloudDocs/"
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
- iCloud: `~/Library/Mobile Documents/com~apple~CloudDocs`, plus a few common fallback paths

### Windows

Probes include:
- Dropbox: `%APPDATA%\Dropbox\info.json`, `%LOCALAPPDATA%\Dropbox\info.json`
- OneDrive: common `OneDrive*` locations under the user profile and cloud-storage folder
- Google Drive: `%USERPROFILE%\Google Drive`, `%USERPROFILE%\My Drive`
- iCloud: `%USERPROFILE%\iCloudDrive`

### Ubuntu / Linux

Probes include:
- Dropbox: `~/.dropbox/info.json`, `~/Dropbox`
- OneDrive: `~/OneDrive`, `~/OneDrive - Personal`
- Google Drive: `~/Google Drive`, `~/GoogleDrive`
- iCloud: common mount-style paths such as `~/iCloudDrive`, `~/Cloud/iCloud`, `~/rclone/iCloud`

For Linux machines with custom cloud mounts, edit `osconfig.json` directly instead of relying on probes.

## Local Backup Behavior

`Os.LocalBackupDir` now comes from config first, then falls back to OS-local defaults. If the configured backup directory is itself inside a discovered cloud root, OsLib rejects it and falls back to a non-cloud local path.

## Cloud-Aware File IO

`RaiFile` treats a path as cloud-backed when it falls under one of the effective provider roots returned by `GetCloudStorageRoots()`.

That behavior now applies consistently whether the root came from:
- explicit config
- probing
- cached discovered state restored through `Os.Config`

## Recommended Setup

For stable machine behavior:
1. Let OsLib create `osconfig.json` on first use.
2. Edit `cloud.*` entries for any provider whose probe path is not the path you want to standardize on.
3. Set `localBackupDir` if you need backups in a specific non-cloud location.
4. Call `ResetCloudStorageCache()` after changing config at runtime.
5. Use `GetCloudDiscoveryReport()` in diagnostics when onboarding a new machine.
