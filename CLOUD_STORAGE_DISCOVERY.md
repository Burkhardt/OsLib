# Cloud Storage Discovery

OsLib now uses a provider-based discovery model for cloud roots.

Supported providers:
- `Dropbox`
- `OneDrive`
- `GoogleDrive`
- `ICloud`

## Hard-Break Notes

The legacy Dropbox-only discovery flow has been removed.

Removed behavior:
- External command dependency (`FindDropbox.exe`)
- Legacy Dropbox-only fields and fallback chain

Current behavior:
- Cross-provider discovery with deterministic precedence
- Explicit overrides via environment variables
- Optional INI configuration file support

## Public API

In `Os`:
- `CloudStorageRoot`: returns preferred root by default order
- `GetCloudStorageRoots(bool refresh = false)`: returns all discovered roots
- `GetCloudStorageRoot(CloudStorageType provider, bool refresh = false)`: returns one provider root
- `GetPreferredCloudStorageRoot(params CloudStorageType[] preferredOrder)`: custom precedence
- `ResetCloudStorageCache()`: clears cache for re-discovery
- `GetCloudDiscoveryReport(bool refresh = false)`: readable diagnostics output

Default preferred order when resolving `CloudStorageRoot`:
1. `GoogleDrive`
2. `ICloud`
3. `Dropbox`
4. `OneDrive`

If none is found, `CloudStorageRoot` throws `DirectoryNotFoundException` with setup guidance.

## Discovery Precedence

For each provider, OsLib resolves roots in this order:
1. Environment variable override
2. INI configuration file
3. OS/provider-specific probing

First valid existing directory wins per provider.

## Environment Variables

Provider root overrides:
- `OSLIB_CLOUD_ROOT_DROPBOX`
- `OSLIB_CLOUD_ROOT_ONEDRIVE`
- `OSLIB_CLOUD_ROOT_GOOGLEDRIVE`
- `OSLIB_CLOUD_ROOT_ICLOUD`

Config file override:
- `OSLIB_CLOUD_CONFIG` (full path to `cloudstorage.ini`)

Examples:

```bash
export OSLIB_CLOUD_ROOT_GOOGLEDRIVE="$HOME/Library/CloudStorage/GoogleDrive-myaccount"
export OSLIB_CLOUD_ROOT_ICLOUD="$HOME/Library/Mobile Documents/com~apple~CloudDocs"
```

PowerShell:

```powershell
$env:OSLIB_CLOUD_ROOT_ONEDRIVE = "C:\Users\me\OneDrive"
$env:OSLIB_CLOUD_CONFIG = "C:\Users\me\AppData\Roaming\OsLib\cloudstorage.ini"
```

CMD:

```cmd
set OSLIB_CLOUD_ROOT_DROPBOX=C:\Users\me\Dropbox
set OSLIB_CLOUD_ROOT_GOOGLEDRIVE=C:\Users\me\Google Drive
```

## INI Configuration

Format: key-value pairs, one per line.
- Supports comments with `#` or `;`
- Keys are case-insensitive

Allowed keys:
- `dropbox`
- `onedrive`
- `googledrive` (or `google_drive`)
- `icloud` (or `icloud_drive`)

Example `cloudstorage.ini`:

```ini
# OsLib cloud roots
dropbox=/Users/me/Library/CloudStorage/Dropbox
onedrive=/Users/me/Library/CloudStorage/OneDrive-Contoso
googledrive=/Users/me/Library/CloudStorage/GoogleDrive-me@example.com
icloud=/Users/me/Library/Mobile Documents/com~apple~CloudDocs
```

Default config lookup paths:

macOS / Linux:
- `~/.config/oslib/cloudstorage.ini`
- `~/.oslib/cloudstorage.ini`

Windows:
- `%APPDATA%\OsLib\cloudstorage.ini`

`OSLIB_CLOUD_CONFIG` takes precedence over default locations.

## OS-Specific Probing

### macOS

Probes include:
- Dropbox: `~/.dropbox/info.json`, `~/Dropbox`, `~/Library/CloudStorage/Dropbox`
- OneDrive: env vars + `~/OneDrive*`, `~/Library/CloudStorage/OneDrive*`
- Google Drive: `~/Google Drive`, `~/GoogleDrive`, `~/Library/CloudStorage/GoogleDrive*`
- iCloud: `~/Library/Mobile Documents/com~apple~CloudDocs`

### Windows

Probes include:
- Dropbox: `%APPDATA%\Dropbox\info.json`, `%LOCALAPPDATA%\Dropbox\info.json`
- OneDrive: `OneDrive`, `OneDriveCommercial`, `OneDriveConsumer` env vars
- Google Drive: `%USERPROFILE%\Google Drive`, `%USERPROFILE%\My Drive`
- iCloud: `%USERPROFILE%\iCloudDrive`

### Ubuntu / Linux

Probes include:
- Dropbox: `~/.dropbox/info.json`, `~/Dropbox`
- OneDrive: `~/OneDrive`, `~/OneDrive - Personal`
- Google Drive: `~/Google Drive`, `~/GoogleDrive`
- iCloud (commonly via mounts): `~/iCloudDrive`, `~/Cloud/iCloud`, `~/rclone/iCloud`

Note:
- Linux cloud setups are often mount-based (`rclone`, FUSE, distro-specific tools).
- For reliable Linux behavior across setups, use env overrides or INI config.

## Recommended Setup Strategy

For production stability:
1. Set explicit env vars in service/user profile.
2. Keep an INI file for machine-local fallback and readability.
3. Use `GetCloudDiscoveryReport()` during startup diagnostics.
4. Call `ResetCloudStorageCache()` after changing env/config at runtime.

## Testing Guidance

Recommended test coverage:
- Env override precedence over INI and probing
- INI parsing and key aliases
- Preferred order resolution
- No-provider-found error path
- Cross-platform path normalization and trailing separator behavior

Integration tests:
- macOS with all 4 providers present
- Windows with OneDrive + Google Drive + Dropbox/iCloud where installed
- Ubuntu with mounted providers (`rclone` or native clients)
