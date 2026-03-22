# OsLib 3.5.0 Release Notes

## Highlights

- Version bumped to `3.5.0`.
- OsLib now documents the supported cloud-backed provider claim as `OneDrive`, `GoogleDrive`, and `Dropbox`.
- The shared `osconfig.json` contract and `defaultCloudOrder` documentation were aligned with the current implementation.
- Os path semantics were clarified around intrinsic runtime paths versus config-driven directories.
- Startup-critical configuration diagnostics now use structured logging and explicit degraded-mode console diagnostics.

## Configuration And Discovery

- The documented default cloud order is now consistent with the current implementation: `OneDrive`, `Dropbox`, `GoogleDrive`.
- `UserHomeDir` and `AppRootDir` are now documented as intrinsic runtime paths and no longer config-driven.
- `TempDir`, `LocalBackupDir`, and `CloudStorageRootDir` are documented as `RaiPath`-based directory properties.
- Missing, unreadable, or malformed `osconfig.json` now degrades startup without silently creating configuration.

## Path And Backup Semantics

- `CloudStorageRootDir` is the primary API; the string-based `CloudStorageRoot` remains only as an obsolete compatibility wrapper.
- `GetBackupRelativeDirectoryPath(...)` now returns `RaiPath`.
- `RaiFile.backup(copy)` now composes its destination through `Os.LocalBackupDir / relativePath` instead of string concatenation.

## Cross-Package Alignment

- OsLib documentation now explicitly aligns with JsonPit's `Id`-based identifier contract.
- Legacy JsonPit payloads that only contain `Name` are documented as normalizing to `Id` in the framework layer.

## Validation

- Full workspace validation passed after the `3.5.0` version bump: `200` tests total, `193` passed, `7` skipped, `0` failed.