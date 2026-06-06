# OsLibCore 3.8.14 Release Notes

## Summary

- Releases `OsLibCore` version `3.8.14`.
- Aligns package metadata, markdown docs, and PlantUML release markers for the coordinated release chain.
- No public API changes from `3.8.13`.

## Validation

- `dotnet build OsLib.csproj --nologo -v minimal`
- NuGet publishing is handled by the tag-triggered `publish-nuget.yml` workflow.
