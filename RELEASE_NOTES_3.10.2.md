# OsLibCore 3.10.2 Release Notes

## Summary

- Releases `OsLibCore` version `3.10.2`.
- Includes cloud-path wiring refinements with matching docs/diagram refreshes.
- Updates path-convention test coverage around current cloud-path behavior.

## Validation

- `dotnet build OsLib.csproj --nologo -v minimal`
- NuGet publishing remains wired through the parent sequential release chain and the tag-triggered `publish-nuget.yml` workflow.
