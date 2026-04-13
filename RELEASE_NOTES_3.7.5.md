# Release Notes 3.7.5

## Summary

- Documentation and diagram alignment release for the current OsLib architecture.
- Clarifies the live `osconfig.json5` contract and removes outdated guidance that still described removed cloud-selection and observer APIs as current.
- Captures the current cloud-path model around `CloudPathWiring`, `RaiPath.CloudEvaluator`, and buffered `RaiPath.Cloud`.

## Documentation Changes

- Refreshed current OsLib docs: `README.md`, `API.md`, and `CLOUD_STORAGE_DISCOVERY.md`.
- Marked older design and test setup notes as historical where they still describe superseded API shapes.
- Updated cross-package guidance in JsonPit and RaiUtils to the current `osconfig.json5` / `Cloud.*` contract.
- Updated `CURRENT-STATE.md` and `CURRENT-STATUS.md` so they no longer advertise `CloudStorageRootDir` and public `LoadConfig(...)` as current OsLib behavior.

## Diagram Changes

- Updated `Os-ClassDiagram.puml` and `Os-verboseCD.puml` release markers to `3.7.5`.
- Kept touched PlantUML files on the `Os-verboseCD` visual style baseline.

## Architecture Notes

- `Os.Config` remains lazy, internal-load, and `dynamic`.
- `UserHomeDir` and `AppRootDir` remain intrinsic runtime values.
- `TempDir` and `LocalBackupDir` remain config-driven.
- Directory wait logic belongs to `RaiPath`.
- File wait logic belongs to `RaiFile`.

## Validation

- The latest directly verified command in this workspace state is:

```bash
dotnet test OsLib/OsLib.Tests/OsLib.Tests.csproj --nologo -v minimal
```

- Result: 50 passed, 0 failed.
