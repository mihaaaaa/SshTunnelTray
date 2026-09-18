# SshTunnelTray

[Русский](README.md) | [English](README.en.md)

A small Windows utility for managing SSH tunnels from the system tray. The interface is available in Russian and English.

![SshTunnelTray settings window](docs/screenshots/settings.png)

> The repository contains source code and safe examples. Working configuration, private keys, passwords, logs, and build outputs are intentionally excluded.

## Features

- multiple named SSH tunnel profiles;
- start, stop, and reconnect from the system tray;
- verification that the local forwarding port is actually listening;
- private-key or SSH-password authentication;
- `ssh.exe` selection and a link to official Win32-OpenSSH releases;
- host-key verification using standard OpenSSH and the system `known_hosts`;
- a compact settings window and tunnel status shown by the tray icon color.

## Requirements

- Windows 10/11 x64;
- .NET 10 Desktop Runtime x64 for running the regular build;
- `ssh.exe` from Windows OpenSSH or Win32-OpenSSH.

The SSH client path may be left empty: the application will try to find `ssh.exe` in `PATH`. Download it from the official [Win32-OpenSSH Releases](https://github.com/PowerShell/Win32-OpenSSH/releases) section.

## Run from source

```powershell
dotnet restore --runtime win-x64
dotnet run --project .\SshTunnelTray.csproj
```

Run a specific profile:

```powershell
SshTunnelTray.exe -t "Profile name"
```

Without arguments, the application opens settings. A profile is created only after the required SSH server, port, and login fields are filled in.

## Build and publish

```powershell
dotnet restore --runtime win-x64
dotnet publish .\SshTunnelTray.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained false `
  -p:PublishSingleFile=true `
  -o .\publish
```

The `publish/` directory is ignored by Git and must not be committed. It is preferable to build the release EXE in CI or attach it to a GitHub Release separately from the source code.

## Where local configuration is stored

When running, the application creates these files next to the executable:

- `SshTunnelTray.json` — profiles, key paths, forwarding parameters, and SSH settings;
- `<profile-name>.cmd` — a generated launcher for the profile.

These files may contain real addresses, logins, local paths, and encrypted passwords. They are excluded by `.gitignore` and checked by `scripts/Verify-Public.ps1`. Do not use `git add -f` to add them.

The safe template is [samples/SshTunnelTray.json.example](samples/SshTunnelTray.json.example). It is not intended for real values.

Important: encrypting a password in local JSON does not make that JSON public or safe for GitHub. The private key remains outside the repository; the profile stores only its path.

## Project structure

```text
SshTunnelTray/
├── App/                 # startup, tray context, and profile launchers
├── Config/              # JSON configuration and local secrets
├── Domain/              # profile and state models
├── Runtime/             # ssh.exe startup and tunnel monitoring
├── Tray/                # tray icon and state
├── UI/                  # WinForms settings window
├── docs/                # documentation and safe screenshots
├── samples/             # anonymized examples only
├── scripts/             # pre-publication checks
└── .github/             # CI, issue, and pull request templates
```

## Pre-GitHub checks

Before the first commit, run:

```powershell
pwsh -NoProfile -File .\scripts\Verify-Public.ps1
```

After creating a Git repository, check the index specifically:

```powershell
pwsh -NoProfile -File .\scripts\Verify-Public.ps1 -TrackedOnly
```

The detailed publication procedure is in [docs/PUBLICATION-CHECKLIST.en.md](docs/PUBLICATION-CHECKLIST.en.md). You can enable the local hook template for commits:

```powershell
git config core.hooksPath .githooks
```

Also enable secret scanning and push protection on GitHub. These features help block supported secrets before they enter a public repository, but they do not replace file checks or rotation of keys that have already been exposed.

## Security and error reports

Rules for handling configuration and reporting a potential leak are described in [SECURITY.en.md](SECURITY.en.md). Do not attach real JSON files, logs, screenshots containing addresses, or private keys to an issue or pull request.

## License

The project is distributed under the MIT License.
