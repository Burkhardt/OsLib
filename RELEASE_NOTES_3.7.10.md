# Release Notes 3.7.10

## Summary

- Patch release for `OsLibCore` version `3.7.10`.
- Adds two async `RaiFile` APIs for stream-based writes and byte-array reads.
- Preserves existing `RaiFile` path resolution, trailing slash handling, and filename normalization behavior.

## New API

- `Task WriteFromAsync(Stream source, CancellationToken ct = default)`
  - Ensures the target directory exists using the existing `mkdir()` flow.
  - Creates or overwrites the target file and copies the source stream asynchronously.
- `Task<byte[]> ReadAllBytesAsync(CancellationToken ct = default)`
  - Reads the full file contents asynchronously via `System.IO.File.ReadAllBytesAsync`.

## Tests

- Added `RaiFile_WriteFromAsync_WritesStreamToDisk`.
- Added `RaiFile_ReadAllBytesAsync_ReturnsFileContents`.

## Validation

- `dotnet test OsLib/OsLib.Tests/OsLib.Tests.csproj --nologo -v minimal`
- Result: 64 passed, 0 failed, 0 skipped.
