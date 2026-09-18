# Configuration

[Русский](configuration.md) | [English](configuration.en.md)

The application creates a working `SshTunnelTray.json` next to the executable. This file is local and is not intended for GitHub.

For documentation and tests, use [SshTunnelTray.json.example](../samples/SshTunnelTray.json.example). It intentionally contains only anonymized values:

- `example.com` — example SSH server;
- `your-user` — example login;
- `C:\\Users\\YOUR_NAME\\.ssh\\id_ed25519` — path placeholder for a key;
- `password: null` — no password is written to the public example;
- `127.0.0.1` and test ports — local values.

`authMode: 0` means that a key file is used. It is more convenient to create real configuration through the application window than to edit it manually. The `checkAfterSave` field means that the selected profile is started automatically after settings are saved. The existing field name is retained for backward compatibility.

## What is stored locally

- profile name, SSH host, login, and ports;
- the path to a private key, but not the key itself;
- an encrypted local password value when password mode is selected;
- the path to `ssh.exe`;
- the selected interface language: `ru` or `en`.

None of these values should appear in a public commit or screenshot.
