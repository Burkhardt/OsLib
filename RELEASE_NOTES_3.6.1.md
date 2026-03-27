# OsLib 3.6.1 Release Notes

## Highlights

- Patch release following `3.6.0`.
- Corrects the NuGet publish order: OsLibCore is now published first so downstream packages (`RaiUtils`, `RaiImage`, `JsonPit`) can resolve their fallback `PackageReference` against a live NuGet version.
- No API or behavioral changes beyond `3.6.0`.

## Validation

- All `OsLib.Tests` pass green with this release.
