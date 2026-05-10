# Release Notes 3.8.0

## Summary

- Coordinated release for `OsLibCore` version `3.8.0`.
- Carries forward the async `RaiFile` APIs introduced in `3.7.10`.
- Refreshes current OsLib docs, release pointers, and PlantUML source headers for the `3.8.0` package line.

## API Status

- No new OsLib API changes relative to `3.7.11`.
- Public API continues to include:
  - `Task WriteFromAsync(Stream source, CancellationToken ct = default)`
  - `Task<byte[]> ReadAllBytesAsync(CancellationToken ct = default)`
  - `void BackdateCreationTime(DateTime utc, int? propagationDelayMs = null)`

## Documentation

- Updated `README.md`, `API.md`, `CLOUD_STORAGE_DISCOVERY.md`, and `ARCHITECTURE-ALIGNMENT.md` for the `3.8.0` package line.
- Refreshed the active PlantUML sources `Os-ClassDiagram.puml`, `Os-verboseCD.puml`, and `RaiFile-Hierarchy.puml` so current diagrams are labeled consistently with the live release.

## Validation

- `dotnet test OsLib/OsLib.Tests/OsLib.Tests.csproj --nologo -v minimal`
- Result: 64 passed, 0 failed, 0 skipped.
