# Architecture Alignment Note (2026-03-04)

This repository aligns with private internal architecture decisions maintained outside this public repository.

## 3.7.3 release alignment

- Minor release: aligns path and canonicalization behavior used by JsonPit cloud integration workflows.
- Public API remains stable for downstream libraries.

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
