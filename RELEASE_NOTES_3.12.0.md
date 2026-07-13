# OsLibCore 3.12.0 Release Notes

## Summary

- Releases `OsLibCore` version `3.12.0`.
- Carries forward the coordinated `RAIkeep` package line with refreshed package metadata, README links, and diagram release markers.
- No public API changes from `3.11.5`.

## Validation

- `dotnet test OsLib.Tests/OsLib.Tests.csproj --nologo -v minimal`
- NuGet publishing remains wired through the parent sequential release chain and the tag-triggered `publish-nuget.yml` workflow.
