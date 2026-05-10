# Release Notes 3.7.11

## Summary

- Patch release for `OsLibCore` version `3.7.11`.
- Carries forward the async `RaiFile` APIs introduced in `3.7.10`.
- Refreshes the packaged README and release-facing metadata so NuGet shows the current OsLib feature line.

## Package README Refresh

- Updated the packaged `README.md` from the stale `3.7.9` summary to the current release line.
- Added `RaiFile.WriteFromAsync(Stream, CancellationToken)` and `RaiFile.ReadAllBytesAsync(CancellationToken)` to the README API summary.
- Updated the README release-notes pointer and example release tag to the current patch line.

## API Status

- No code changes relative to `3.7.10`.
- Public API remains:
  - `Task WriteFromAsync(Stream source, CancellationToken ct = default)`
  - `Task<byte[]> ReadAllBytesAsync(CancellationToken ct = default)`

## Validation

- `dotnet pack OsLib/OsLib.csproj --nologo -v minimal`
- Result: package builds successfully with the refreshed README and `3.7.11` metadata.
