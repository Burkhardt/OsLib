# Release Notes 3.8.1

## Summary

- Makes `RaiFile.mkdir()` virtual so base-class methods use standard polymorphic dispatch for derived file types.
- Preserves the existing `RaiFile.mkdir()` implementation logic.

## Validation

- `dotnet test RAIkeep.slnx --nologo -v minimal`
- Result: 223 passed, 0 failed, 0 skipped.