# Security

[Русский](SECURITY.md) | [English](SECURITY.en.md)

## What must not be published

Never add the following to GitHub:

- `SshTunnelTray.json` or any working configuration;
- private SSH keys (`id_ed25519`, `id_rsa`, `*.pem`, `*.ppk`, `*.pfx`, and similar files);
- passwords, tokens, `.env`, credential files, or memory dumps;
- generated `.cmd` files, logs, or diagnostic dumps;
- screenshots containing real server addresses, logins, paths, keys, or terminal contents;
- build outputs and local IDE files that may contain absolute paths.

`.gitignore` prevents ordinary addition of these files, but it does not protect a file that has already been added to the index. Therefore, run this before committing:

```powershell
pwsh -NoProfile -File .\scripts\Verify-Public.ps1 -TrackedOnly
```

## Local application data

The working JSON may contain an SSH server address, login, private-key path, forwarding parameters, and an encrypted password. Even an encrypted value should not be published: the format and encryption key are part of the application source, and the file contains environment metadata.

Only the anonymized [example](samples/SshTunnelTray.json.example), containing `example.com`, `your-user`, and `YOUR_NAME`, is allowed in the public repository.

## If a secret reaches Git

1. Immediately revoke or replace the affected password, key, or token. Removing the file from the latest commit is insufficient: the value remains in history.
2. Do not publish the secret in an issue, pull request, or comment.
3. Remove the secret from history using a separate verified procedure (`git filter-repo` or an equivalent), then check all branches and tags.
4. Enable GitHub Secret scanning and push protection.
5. Run `Verify-Public.ps1 -TrackedOnly` again and inspect the staged diff.

## Reporting a problem

Ordinary errors can be described in an issue without configuration or logs. Do not create a public issue for a potential leak: use private vulnerability reporting after enabling that feature in the repository settings, or contact the owner through their GitHub profile.

## Protection boundaries

The local check and GitHub Action block known dangerous filenames and common secret patterns. They cannot reliably detect a secret inside an arbitrary image or encrypted archive, so manual staged-diff review is required.
