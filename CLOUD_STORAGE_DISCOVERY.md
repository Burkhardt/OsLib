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
- RaiFile cloud-aware IO waits follow discovered provider roots, including Google Drive and iCloud

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

`OSLIB_CLOUD_CONFIG` takes precedence over default locations and is used as the sole config file candidate when set.

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

### Ubuntu Recommendation

For Ubuntu development machines, especially when Google Drive is mounted through `rclone`, GNOME integration, or another user-specific path, do not rely on probe-only behavior.

Recommended setup for Mzansi and related local development:

```bash
export OSLIB_CLOUD_ROOT_GOOGLEDRIVE="$HOME/GoogleDrive-Mzansi"
```

If the mount path differs by machine, keep the same variable name and only change the value:

```bash
export OSLIB_CLOUD_ROOT_GOOGLEDRIVE="$HOME/cloud/mzansi-gdrive"
```

Equivalent `cloudstorage.ini` entry:

```ini
googledrive=/home/me/GoogleDrive-Mzansi
```

This is the preferred approach for Ubuntu-based development of Mzansi and the upcoming Python `OsLib`, `RaiUtils`, and `JsonPit` packages because it keeps the cloud-root contract deterministic across languages and machines.

## Recommended Setup Strategy

For production stability:
1. Set explicit env vars in service/user profile.
2. Keep an INI file for machine-local fallback and readability.
3. Use `GetCloudDiscoveryReport()` during startup diagnostics.
4. Call `ResetCloudStorageCache()` after changing env/config at runtime.

## Cloud-Aware File IO

`RaiFile` now treats a path as cloud-backed when it is inside one of the discovered provider roots.

This matters for file operations that wait for cloud-synced directories or files to materialize or vanish.

Practical consequence:
- A custom Google Drive root on Ubuntu, configured through `OSLIB_CLOUD_ROOT_GOOGLEDRIVE` or `cloudstorage.ini`, participates in the same cloud-aware IO behavior as Dropbox and OneDrive.

## Cross-Language Convention

If multiple libraries in the same environment need cloud-root discovery, they should share the same environment variable and INI key contract.

Recommended shared convention for C# and Python packages:
- Use `OSLIB_CLOUD_ROOT_GOOGLEDRIVE` for Google Drive overrides.
- Use `OSLIB_CLOUD_CONFIG` for an explicit machine-local config file.
- Use the same INI keys: `dropbox`, `onedrive`, `googledrive` or `google_drive`, `icloud` or `icloud_drive`.
- Apply the same precedence: environment override, explicit INI file, default INI locations, then OS-specific probing.

That keeps `OsLib`, `RaiUtils`, and `JsonPit` aligned whether the caller is .NET or Python.

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

For Ubuntu integration tests, prefer a configured Google Drive mount path over home-directory probe assumptions, and verify both discovery and `RaiFile` cloud-aware IO behavior under that mounted root.
