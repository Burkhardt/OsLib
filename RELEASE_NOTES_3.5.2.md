# OsLib 3.5.2 Release Notes

## Highlights

- Version bumped to `3.5.2`.
- Refreshes the packaged NuGet icon asset to a square `128x128` PNG.
- Establishes the next upstream `OsLibCore` patch release before later downstream package migrations.

## Packaging

- `OsLib.csproj` continues to publish as `OsLibCore`.
- Package metadata points at the tracked `HardCastle.png` asset used for the NuGet icon.
- This release is intended to settle on NuGet before later `RaiUtils`, `RaiImage`, and `JsonPit` fallback package updates.

## Runtime And API Surface

- No public API behavior was intentionally changed in this patch.
- The current documented path, config, and cloud-discovery semantics from `3.5.0` remain in effect.

## Validation

- Local validation for this patch uses the `OsLib` test suite before tagging and publishing.