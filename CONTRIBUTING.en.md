# Contributing

[Русский](CONTRIBUTING.md) | [English](CONTRIBUTING.en.md)

## Before making changes

1. Create a separate branch.
2. Do not copy real configuration, private keys, or logs into the working tree unless necessary.
3. Use `example.com`, `your-user`, `YOUR_NAME`, and test ports in examples.

## Checks

From the project root, run:

```powershell
pwsh -NoProfile -File .\scripts\Verify-Public.ps1
dotnet restore --runtime win-x64
dotnet build .\SshTunnelTray.csproj --configuration Release --runtime win-x64 --no-restore
```

If Git has already been initialized, additionally check the index before committing:

```powershell
pwsh -NoProfile -File .\scripts\Verify-Public.ps1 -TrackedOnly
git diff --cached --check
git diff --cached --name-only
git diff --cached
```

## Pull requests

Include:

- what changed and why;
- how it was checked;
- whether configuration, secret storage, or SSH startup is affected;
- whether the interface changed and an anonymized screenshot is attached.

Do not attach real `SshTunnelTray.json`, `.cmd` files, keys, passwords, logs, or screenshots of a working environment.
