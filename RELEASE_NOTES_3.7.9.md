# Release Notes 3.7.9

## Summary

- Patch release for `OsLibCore` version `3.7.9`.
- Carries forward the `3.7.8` RaiPath filesystem fixes.
- Refreshes current release docs, package metadata, and active PlantUML headers for the live package line.

## Documentation

- Updated `README.md`, `API.md`, `CLOUD_STORAGE_DISCOVERY.md`, and `ARCHITECTURE-ALIGNMENT.md` to the `3.7.9` package line.
- Refreshed the active OsLib PlantUML headers so current diagrams are labeled consistently with the live release.

## Validation

- `dotnet test OsLib.slnx --nologo -v minimal`
- Result: 62 passed, 0 failed, 0 skipped.
