# OsLib 3.3.0 Release Notes

## Highlights

- Version bumped to `3.3.0`.
- Introduced `Os.Config` as the top-level typed configuration entry point.
- Added reusable `ConfigFile<TData>` infrastructure for JSON-backed configuration files.
- Standardized machine-local configuration on `osconfig.json`.

## Configuration Model

- Cloud roots now live in a nested `cloud` object.
- Configurable directories now include:
  - `homeDir`
  - `tempDir`
  - `localBackupDir`
- Default provider precedence is configurable through `defaultCloudOrder`.

## Discovery And Runtime Behavior

- Cloud-root discovery now prefers configured values first, then provider probing.
- Discovered roots are cached for runtime efficiency.
- Missing provider roots can be persisted back into config when the corresponding config entry is empty.
- Local backup resolution now rejects cloud-backed configured backup paths and falls back to a local path.

## Validation

- Full solution validation remained green after the refactor and version bump.
