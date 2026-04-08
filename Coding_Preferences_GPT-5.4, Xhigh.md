- VS Code + C#: use tab-formatted source consistently.
- Indentation policy: 1 tab character = 1 indentation level.
- Editor setting context: tab size shown as 4 spaces, but prefer literal tab characters in code.
- Be proactive about spotting/reducing accidental space-indentation drift in C# files.
- In RAIkeep/JsonPit cloud-backed code, prefer RaiFile/OsLib file operations over direct System.IO File/Directory calls whenever a RaiFile or descendant is involved.
- Do not introduce temp-file stage-and-replace patterns for cloud-backed JsonPit persistence; cloud providers can create duplicate '(1)' files when a synced file appears to vanish and reappear.
- Treat generic cross-project file-safety patterns as suspect unless they are validated against this repo's cloud-sync behavior first.

- For architectural or testing-seam changes, involve the user in the decision instead of silently introducing workarounds; ask before changing core path/config design.
