# Release Notes 3.7.8

## Summary

- Patch release for `OsLibCore` version `3.7.8`.
- Focused on directory-level filesystem operations in `RaiPath`: `mv()`, `cp()`, and a new `backup()` method symmetrical to `RaiFile.backup`.
- No public API removals; signature additions only (`cp` gains an optional `keepBackup` parameter, new `RaiPath.backup` method).

## Bug Fixes — `RaiPath.mv()`

- Fixed `replace: true, keepBackup: false`: the prior implementation called `Directory.Move` over an existing target without first removing it, causing `IOException("Cannot create '<target>/' because a file or directory with the same name already exists.")`.
- Fixed `replace: true, keepBackup: true`: the prior implementation composed the backup name as `Path + "_backup_" + id`. Because `RaiPath.Path` always carries a trailing directory separator, the resulting path was a *child* of the target rather than a *sibling*, so `Directory.Move` failed with `IOException("Invalid argument")`.

## Behavior Changes — Backup Semantics

- `RaiPath.mv()` and `RaiPath.cp()` no longer create ad-hoc `_backup_<id>` siblings.
- When `keepBackup: true`, both methods now delegate to the new `RaiPath.backup()` method, which mirrors the source path under `Os.LocalBackupDir` and appends a UTC timestamp (`Os.DATEFORMAT`) to the leaf segment — symmetrical to `RaiFile.backup`.

## New API

- `RaiPath RaiPath.backup(bool copy = false)` — moves (or copies) the directory tree into `Os.LocalBackupDir`, mirroring the original absolute path under that root and appending a UTC timestamp to the leaf. Returns the resulting backup path, or `null` if the source does not exist. Throws `InvalidOperationException` when `Os.LocalBackupDir` is not configured.
- `RaiPath.cp(RaiPath from, bool replace, bool keepBackup = false)` — added optional `keepBackup` parameter for symmetry with `mv()`.

## Implementation Notes — `RaiPath.cp()`

- Reimplemented to traverse purely via `RaiPath.EnumerateFiles`, `RaiPath.EnumerateDirectories`, `RaiPath.mkdir()`, and `RaiFile.cp()` — no direct `System.IO` calls in the recursion. This removes the dependency on the previously-missing `CopyDirectory` helper.

## Tests

- Added six `RaiPath_Mv_*` tests in `OsLib.Tests/PathConventionsTests.cs` covering: move into empty target, throw on existing target without `replace`, replace without backup, replace with backup (verifies the backup landing point under `Os.LocalBackupDir`), missing source, and null source.
- The keep-backup test cleans up the mirrored backup subtree after the assertion.

## Validation

- `dotnet test RAIkeep.slnx --nologo -v minimal`
- Result: all OsLib.Tests pass (62/62 in the OsLib.Tests project; full solution suite green).
