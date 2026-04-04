# Release Notes 3.7.3

## Summary

- Patch release for `OsLibCore` version `3.7.3`.
- Includes path-handling and release-alignment updates for the 3.7.3 line.

## Breaking Change Warning

- This is a breaking change for positional constructor calls.
- `TextFile(RaiPath path, string name, string ext = "txt", string content = null)` interprets the third positional string argument as `ext`, not `content`.
- `Script(RaiPath path, string name, string ext = "sh", string content = null)` follows the same parameter order, so the third positional string is also interpreted as `ext`.
- Previous positional usage that passed content as the third argument is now parsed as file extension and will not work as intended.

```csharp
new TextFile(new RaiPath("~"), "test.txt", "First line of textfile")
```

```csharp
new Script(new RaiPath("~"), "deploy.sh", "#!/bin/sh\necho ok")
```

- Use a named argument for content instead:

```csharp
new TextFile(new RaiPath("~"), "test.txt", content: "First line of textfile")
```

```csharp
new Script(new RaiPath("~"), "deploy.sh", content: "#!/bin/sh\necho ok")
```

## Notes

- Keep release order: publish `OsLibCore` before downstream packages (`RaiUtils`, `RaiImage`, `JsonPit`).
