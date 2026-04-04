# OsConfig Breaking Change

This note is the authoritative description of the current OsLib config contract.

Older docs that mention `osconfig.json`, camelCase keys, compatibility rewrites, or typed config wrappers describe older behavior and should be treated as historical context only.

## Breaking Change Summary

- The expected config file name is now `osconfig.json5`.
- The default local config location is `~/.config/RAIkeep/osconfig.json5`.
- The expected remote config location is `~/.config/RAIkeep/osconfig.json5`.
- `Os.Config` is deserialized directly into a `dynamic` object.
- OsLib no longer rewrites, canonicalizes, or normalizes config property names.
- Config property names are expected in PascalCase.

## Expected Property Names

- `HomeDir`
- `TempDir`
- `LocalBackupDir`
- `DefaultCloudOrder`
- `Cloud.Dropbox`
- `Cloud.OneDrive`
- `Cloud.GoogleDrive`
- `Observers.<name>.SshTarget`

## Why This Is Breaking

- Existing config files named `osconfig.json` are no longer the default contract.
- Existing config files that still use camelCase keys like `tempDir`, `localBackupDir`, `defaultCloudOrder`, `cloud`, `observers`, or `sshTarget` are no longer rewritten during load.
- Call sites now read the `dynamic` config object directly. Missing or mismatched member names will fail at runtime and are expected to be handled by the surrounding `try`/`catch` logic.
- This is intentional. The config object is treated as a dynamic contract, not a typed model with compatibility shims.

## HomeDir Is Deprecated

- `HomeDir` is not used to resolve `Os.UserHomeDir`.
- `Os.UserHomeDir` is intrinsic OS/runtime state and must be resolved before the config file location can be expanded.
- The config file itself lives under `~/.config/RAIkeep/osconfig.json5`.
- Resolving `~` already requires a usable home directory.

## Why HomeDir Cannot Be The Source Of Truth

If OsLib tried to use `HomeDir` from the config file to resolve `UserHomeDir`, the resolution would be circular:

1. OsLib needs `UserHomeDir` to expand `~` in `~/.config/RAIkeep/osconfig.json5`.
2. OsLib would need to open that config file to read `HomeDir`.
3. Therefore `HomeDir` cannot be the source of truth for locating the config file that contains `HomeDir`.

Because of that, `UserHomeDir` must come from the operating system environment and runtime APIs, not from the config file.

## Path Shorthand Resolution

OsLib resolves a few path shorthands when config values are later passed through `RaiPath` or `RaiFile`.

- `~` resolves to `Os.UserHomeDir`.
- `~/something` resolves relative to `Os.UserHomeDir`.
- `.` resolves to `Os.AppRootDir`.
- `./something` resolves relative to `Os.AppRootDir`.

Important:

- `Os.AppRootDir` currently comes from `Directory.GetCurrentDirectory()`.
- That means `.` and `./wwwroot` refer to the current working directory of the running process that uses OsLib.
- This is often the launch root of the application, but it is not a promise that `.` means the physical directory of the executable file on disk.

## Expected Example

```json5
{
	HomeDir: "/ignored/if/present/",
	TempDir: "~/temp/",
	LocalBackupDir: "./backup/",
	DefaultCloudOrder: ["OneDrive", "Dropbox", "GoogleDrive"],
	Cloud: {
		Dropbox: "/Users/me/Library/CloudStorage/Dropbox/",
		OneDrive: "/Users/me/Library/CloudStorage/OneDrive-Contoso/",
		GoogleDrive: "/Users/me/Library/CloudStorage/GoogleDrive-me@example.com/My Drive/"
	},
	Observers: {
		mzansi: {
			SshTarget: "rsb@Mzansi"
		}
	}
}
```

## Operational Notes

- `HomeDir` may still appear in config for legacy or documentary reasons, but OsLib does not use it for home resolution.
- `~` is useful in value positions like `TempDir: "~/temp/"` because the user home has already been resolved from the operating system by that point.
- `.` is useful in value positions like `LocalBackupDir: "./backup/"` when you intentionally want a path relative to the current working directory of the running process.
- `TempDir`, `LocalBackupDir`, cloud roots, and observer targets remain config-driven.
- The contract change to `osconfig.json5` is about the expected file name and schema. Parsing still goes through the current Newtonsoft.Json-based code path, so file contents must stay compatible with that parser.