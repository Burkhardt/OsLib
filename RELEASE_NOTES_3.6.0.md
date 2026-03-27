# OsLib 3.6.0 Release Notes

## Highlights

- `CanonicalFile` constructor crash fixed: eliminated infinite recursion in the `Name` getter and setter overrides.
- `CanonicalFile` constructor is now safe when called with directory-style paths (trailing `/`).
- `PathConventionsTests` updated with new constructor tests covering the `RaiPath` and string overloads for real-world cloud-storage paths.
- Aligns the `OsLib` package version with the shared `3.6.0` `RAIkeep` package line.

## Bug Fix: CanonicalFile recursion

The `Name` getter in `CanonicalFile` checked `string.IsNullOrEmpty(Name)`, which called itself
recursively and caused a `StackOverflowException` on .NET (a process-terminating crash/core dump).
The setter contained a symmetric problem.  Both are resolved by routing internal reads through `base.Name`.

The constructor also crashed when passed a directory-style path (trailing `/`) because
`new RaiFile(fullName).NameWithExtension` returns an empty string in that case, and the
subsequent canonicalization then called `Name` before it had been initialised.

## Tests Adjusted

- `CanonicalFile_StringConstructor_WithNomsaNetDirectoryPath_PreservesDirectoryPath`
- `CanonicalFile_RaiPathConstructor_WithNomsaNetDirectoryPathAndName_KeepsCanonicalByStem`

Both tests exercise the previously crashing constructor paths with the
`/Users/RSB/Library/CloudStorage/OneDrive/OneDriveData/Nomsa.net/` path family.

## Validation

- All `OsLib.Tests` pass green with this release.
