# OsLib 3.5.3 Release Notes

## Highlights

- Documentation patch release for the OsLib package surface.
- Advances the published documentation line from `3.5.2` to `3.5.3`.
- Documents `CanonicalPath` as deprecated legacy API surface and directs new usage toward `RaiPath` composition.

## Documentation

- Updated `README.md`, `API.md`, and `ARCHITECTURE-ALIGNMENT.md` for the `3.5.3` documentation line.
- Updated hierarchy and dependency diagrams to reflect the deprecated status of `CanonicalPath` while keeping the type visible because it still exists in source.
- Repointed the main package documentation to these `3.5.3` release notes.

## Runtime And API Surface

- No implementation changes are included in this documentation patch.
- `CanonicalPath` still exists in source for compatibility, but the recommended path-composition model is direct `RaiPath` usage.
- Existing `CanonicalFile` behavior remains documented as compatibility-oriented canonical-by-name support.

## Validation

- Validation for this patch is documentation consistency: version references, release-note links, and diagram wording were updated together.