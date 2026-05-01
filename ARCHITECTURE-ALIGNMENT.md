# Architecture Alignment Note (2026-03-04)

This repository aligns with private internal architecture decisions maintained outside this public repository.

## 3.7.7 release alignment

- Patch release: refreshes current docs and diagrams around the post-purge OsLib architecture.
- Public API remains stable for downstream libraries; the `RaiFile.BackdateCreationTime(...)` and sync propagation delay controls are unchanged in this release.

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
