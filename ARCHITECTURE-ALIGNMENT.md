# Architecture Alignment Note (2026-03-04)

This repository aligns with private internal architecture decisions maintained outside this public repository.

## 3.5.3 release alignment

- The supported cloud-backed provider claim for the packaged `RAIkeep` stack is `OneDrive`, `GoogleDrive`, and `Dropbox`.
- OsLib remains the shared configuration and path-resolution foundation for those providers.
- JsonPit now treats `Id` as the canonical identifier, and OsLib documentation aligns with that cross-package contract.
- `OsLib 3.5.3` is a documentation-focused patch line for the current public package narrative.
- `CanonicalPath` remains documented as deprecated legacy surface rather than recommended active design.

## intent for OsLib

- Keep OsLib focused on generic foundations and contracts.
- Maintain strong backward compatibility expectations for public foundational APIs.
- Keep convention infrastructure (`RaiPath`, `IPathConventionFile`, `PathConventionType`, canonical convention support) in OsLib.

## boundary stance

- Image-domain class ownership is in RaiImage.
- OsLib provides reusable contracts and non-domain-specific path/file behavior.

## migration stance

- Support incremental integration from legacy capabilities.
- Prefer explicit, tested convention behavior over implicit path rules.
