# Cloud Storage Configuration

This note describes the current `OsLib 3.7.7` cloud-related contract.

Historical docs that mention `CloudStorageRootDir`, public `LoadConfig(...)`, provider-precedence APIs, `DefaultCloudOrder`, or observer-specific `Os` APIs describe older package lines and should not be treated as current.

## Current Model

The active configuration file is `RAIkeep.json5`.

Default location used by the current code:
- `~/.config/RAIkeep.json5`

`Os.Config` exposes the active dynamic config object. Loading is lazy and internal.

Current config values used by the cloud/path model:
- `TempDir`
- `LocalBackupDir`
- `SyncPropagationDelayMs`
- `Cloud.Dropbox`
- `Cloud.OneDrive`
- `Cloud.GoogleDrive`

Notes:
- `UserHomeDir` is intrinsic and not config-driven.
- `AppRootDir` is intrinsic and resolves the runtime meaning of `.`.
- `Cloud.*` entries are optional. If none are configured, OsLib stays local-only.
- There is no public preferred-cloud-root helper in `Os`. Callers that need one specific root should read it explicitly from `Os.Config.Cloud` and wrap it in `RaiPath`.

## How Cloud Awareness Works

Cloud awareness now flows through `RaiPath`, not through provider-selection helpers on `Os`.

- `CloudPathWiring.Initialize()` assigns `RaiPath.CloudEvaluator`.
- `RaiPath.Path` buffers its `Cloud` flag when the path is set.
- `RaiFile.Path` copies that buffered `Cloud` flag from the assigned `RaiPath`.

That means cloud-aware waits stay close to the object that mutates the filesystem:
- `RaiPath` owns directory waits.
- `RaiFile` owns file waits.

## Example Config

```json5
{
	TempDir: "/var/folders/.../T/",
	LocalBackupDir: "/Users/me/Library/Application Support/OsLib/Backup/",
	SyncPropagationDelayMs: 15000,
	Cloud: {
		Dropbox: "/Users/me/Library/CloudStorage/Dropbox/",
		OneDrive: "/Users/me/Library/CloudStorage/OneDrive-Contoso/",
		GoogleDrive: "/Users/me/Library/CloudStorage/GoogleDrive-me@example.com/My Drive/"
	}
}
```

## Using a Configured Cloud Root

Example: use the configured Google Drive root explicitly.

```csharp
var configuredRootText = (string?)Os.Config.Cloud?.GoogleDrive
	?? throw new InvalidOperationException("Configure Cloud.GoogleDrive in RAIkeep.json5 first.");

var configuredRoot = new RaiPath(configuredRootText);
var personRoot = configuredRoot / "AfricaStage" / "OTW" / "person";
```

## Config Failure Behavior

The current model is intentionally lightweight.

- `Os.Config` is a lazy `dynamic` object.
- If the config file cannot be read or parsed, `Os.Config` falls back to a minimal object with `TempDir = Path.GetTempPath()`.
- In that fallback case, cloud classification remains effectively disabled because no `Cloud.*` roots are available.

## Local Backup Behavior

`Os.LocalBackupDir` is optional.

- When it is configured, `RaiFile.backup(copy)` composes backup destinations below that root.
- When it is absent, backup features stay disabled.
- There is no fallback back into a preferred cloud root.

## Metadata Propagation Delay

`RaiFile.BackdateCreationTime(...)` exists for scenarios where tests or tools must manipulate `FileAge` deterministically and still give cloud providers a chance to observe the metadata change.

- The method sets `CreationTimeUtc`.
- It writes a best-effort `{name}.backdate.tmp` sentinel file next to the target.
- It waits for a propagation delay.
- It deletes the sentinel afterward.

Delay priority:

- explicit `propagationDelayMs`
- `Os.Config.SyncPropagationDelayMs`
- `RaiFile.DefaultSyncPropagationDelayMs` (10 seconds)
