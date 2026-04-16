# Release Notes 3.7.6

## Summary

- Adds `RaiFile.BackdateCreationTime(...)` for deterministic file-age manipulation in cloud-sync scenarios.
- Adds `RaiFile.DefaultSyncPropagationDelayMs` as a static override point for metadata propagation waits.
- Supports `SyncPropagationDelayMs` in `RAIkeep.json5` so machines can tune the default delay without recompilation.

## API Additions

- `RaiFile.FileAge` continues to measure age from `CreationTimeUtc`.
- `RaiFile.BackdateCreationTime(DateTime utc, int? propagationDelayMs = null)` now:
  - sets `CreationTimeUtc`
  - writes a best-effort `{name}.backdate.tmp` sentinel next to the file
  - waits using explicit parameter, then `Os.Config.SyncPropagationDelayMs`, then `RaiFile.DefaultSyncPropagationDelayMs`
  - removes the sentinel after the delay
- `RaiFile.DefaultSyncPropagationDelayMs` defaults to `10_000` milliseconds and is settable in-process.

## Configuration

- Current config naming in code is `RAIkeep.json5`.
- `SyncPropagationDelayMs` is optional and can be tuned per machine.
- This is especially useful when sync latency differs between native OneDrive and rclone/Google Drive setups.

## Documentation And Diagram Updates

- Refreshed `README.md`, `API.md`, and `CLOUD_STORAGE_DISCOVERY.md` to document the new backdate helper and delay configuration.
- Updated OsLib class diagrams that surface `RaiFile` methods to include the new API.

## Validation

- `dotnet test OsLib/OsLib.Tests/OsLib.Tests.csproj --configuration Release --nologo -v minimal`
- Result: 56 passed, 0 failed.