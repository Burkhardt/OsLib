# Release Notes 3.7.7

## Summary

- Patch release for `OsLibCore` version `3.7.7`.
- Keeps the current `RAIkeep.json5`, `CloudPathWiring`, and sync-propagation contract unchanged.
- Refreshes the live docs and PlantUML diagrams so the current release surface matches the current codebase state.

## Documentation And Diagram Updates

- Refreshed `README.md`, `API.md`, `CLOUD_STORAGE_DISCOVERY.md`, and `ARCHITECTURE-ALIGNMENT.md` for the `3.7.7` package line.
- Updated active umbrella and OsLib PlantUML headers to `3.7.7` so current diagrams are distinct from historical artifacts.

## Validation

- `dotnet test RAIkeep.slnx --nologo -v minimal`
- Result: 214 passed, 0 failed, 0 skipped.