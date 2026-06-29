# OsLibCore 3.12.1 Release Notes

## Summary

- Releases `OsLibCore` version `3.12.1`.
- Carries forward the coordinated `RAIkeep` package line with refreshed package metadata, README links, and diagram release markers.
- No public API changes from `3.12.0`.

## Validation

- `dotnet build OsLib.csproj --nologo -v minimal`
- NuGet publishing remains wired through the parent sequential release chain and the tag-triggered `publish-nuget.yml` workflow.
