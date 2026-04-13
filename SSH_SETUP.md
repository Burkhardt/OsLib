# SSH Setup For Remote Observer Tests

## Historical note for 3.7.5

This guide documents an older remote-observer test harness.

It remains useful as setup history, but it should not be read as the current OsLib public API contract. In particular, references below to `DefaultCloudOrder` and `Observers` describe an older config shape than the stripped-down current `Os.Config` guidance.

This guide explains how to make OsLib remote-observer tests work on macOS, Ubuntu, and Windows.

The tests do not depend on a specific observer name such as `Mzansi`.
They use any configured observer entry in `osconfig.json5` that has a reachable `SshTarget`.

## What The Tests Need

Local machine requirements:
- an `osconfig.json5` file at the active OsLib config path
- at least one `Observers` entry with `Name` and `SshTarget`
- an SSH client that can connect to that target without interactive prompts blocking the test

Remote machine requirements:
- an SSH server that accepts the configured account
- a readable `~/.config/RAIkeep/osconfig.json5`
- a usable `TempDir`
- any configured remote cloud root directories must exist and be writable

## Current Local Config Shape

OsLib currently reads observer configuration from `osconfig.json5`, not from a separate remote-test file.

Typical locations:
- macOS / Ubuntu: `~/.config/RAIkeep/osconfig.json5`
- Windows: `%APPDATA%\RAIkeep\osconfig.json5`

Example:

```json5
{
	TempDir: "~/temp/",
	LocalBackupDir: "~/backup/",
	DefaultCloudOrder: ["OneDrive", "Dropbox", "GoogleDrive"],
	Cloud: {
		OneDrive: "/path/to/OneDrive/",
		Dropbox: "/path/to/Dropbox/",
		GoogleDrive: "/path/to/GoogleDrive/"
	},
	Observers: [
		{
			Name: "observer-a",
			SshTarget: "user@example-host"
		}
	]
}
```

## Client Setup

### macOS

Check whether the SSH client is available:

```bash
ssh -V
```

If needed, install Xcode Command Line Tools:

```bash
xcode-select --install
```

Generate a key if you do not already have one:

```bash
ssh-keygen -t ed25519 -C "raikeep-tests"
```

Copy the public key to the remote server:

```bash
ssh-copy-id user@example-host
```

If `ssh-copy-id` is unavailable on your macOS installation, copy the contents of `~/.ssh/id_ed25519.pub` into the remote user's `~/.ssh/authorized_keys` manually.

### Ubuntu

Install the client if necessary:

```bash
sudo apt update
sudo apt install -y openssh-client
```

Generate a key:

```bash
ssh-keygen -t ed25519 -C "raikeep-tests"
```

Copy the key:

```bash
ssh-copy-id user@example-host
```

### Windows

Modern Windows usually includes OpenSSH Client already.
Check in PowerShell:

```powershell
ssh -V
```

If it is missing, install it from Optional Features or with PowerShell:

```powershell
Add-WindowsCapability -Online -Name OpenSSH.Client~~~~0.0.1.0
```

Generate a key:

```powershell
ssh-keygen -t ed25519 -C "raikeep-tests"
```

Copy the public key to the remote server manually, or use:

```powershell
type $env:USERPROFILE\.ssh\id_ed25519.pub
```

and append that content to the remote user's `authorized_keys` file.

## Server Setup

### Ubuntu Server

Install and enable OpenSSH Server:

```bash
sudo apt update
sudo apt install -y openssh-server
sudo systemctl enable --now ssh
```

Confirm it is listening:

```bash
sudo systemctl status ssh
```

Prepare the remote account:

```bash
mkdir -p ~/.ssh
chmod 700 ~/.ssh
nano ~/.ssh/authorized_keys
chmod 600 ~/.ssh/authorized_keys
```

If UFW is enabled, allow SSH:

```bash
sudo ufw allow ssh
```

### Windows Server Or Windows Workstation As Remote Host

Install OpenSSH Server in PowerShell as Administrator:

```powershell
Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0
Start-Service sshd
Set-Service -Name sshd -StartupType Automatic
```

Allow the firewall rule if needed:

```powershell
New-NetFirewallRule -Name sshd -DisplayName "OpenSSH Server" -Enabled True -Direction Inbound -Protocol TCP -Action Allow -LocalPort 22
```

Create the `.ssh` directory for the target user and place the public key in `authorized_keys`.

## Remote OsLib Requirements

The remote account must expose a readable `~/.config/RAIkeep/osconfig.json5`.

Example remote file:

```json5
{
	TempDir: "~/temp/",
	LocalBackupDir: "~/backup/",
	DefaultCloudOrder: ["Dropbox", "OneDrive", "GoogleDrive"],
	Cloud: {
		OneDrive: "/srv/ServerData/OneDriveData/",
		Dropbox: "/srv/ServerData/DropboxData/",
		GoogleDrive: "/srv/ServerData/GDriveData/"
	}
}
```

The tests assume:
- `TempDir` resolves to an existing writable directory
- any configured remote cloud root directory exists and is writable
- the remote `osconfig.json5` can be read without interactive prompts or manual repair during the test run

## Manual Verification

From the local machine, verify the configured SSH target first:

```bash
ssh user@example-host "printf ready"
```

Expected output:

```text
ready
```

Then verify the remote config file:

```bash
ssh user@example-host "test -f ~/.config/RAIkeep/osconfig.json5 && printf ready || printf missing"
```

Inspect the remote config when needed:

```bash
ssh user@example-host "cat ~/.config/RAIkeep/osconfig.json5"
```

Verify the remote temp directory:

```bash
ssh user@example-host 'temp="$HOME/temp"; mkdir -p "$temp" && probe="$temp/raikeep-probe.txt" && printf ready > "$probe" && cat "$probe" && rm -f "$probe"'
```

## Common Failure Causes

- SSH prompts for a password, host-key confirmation, or MFA in a way the test process cannot answer.
- The configured `SshTarget` is wrong.
- The remote user does not have permission to log in through SSH.
- `~/.config/RAIkeep/osconfig.json5` is missing on the remote host.
- The remote `TempDir` does not exist.
- The remote `TempDir` exists but is not writable.
- The configured remote cloud root path does not exist.
- The local machine can reach the host interactively, but the non-interactive shell used by the test gets a different environment.

## Practical Recommendation

For stable automated runs:
- use key-based SSH authentication
- avoid interactive shell prompts during login
- keep `TempDir` and `LocalBackupDir` on always-available local storage
- keep remote observer entries in local `osconfig.json5` current
- verify the remote `osconfig.json5` after server changes
