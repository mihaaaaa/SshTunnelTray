# GitHub publication checklist

[Русский](PUBLICATION-CHECKLIST.md) | [English](PUBLICATION-CHECKLIST.en.md)

## Before the first commit

- [ ] No real `SshTunnelTray.json` is among the files to be added.
- [ ] No private keys, certificates, `.env`, credentials, passwords, tokens, or logs are present.
- [ ] No `bin/`, `obj/`, `publish/`, `.pdb`, `.exe`, or local IDE files are present.
- [ ] Screenshots show only an empty or demonstration profile.
- [ ] Working addresses, logins, paths, and server names have been replaced with placeholders.
- [ ] `pwsh -NoProfile -File .\scripts\Verify-Public.ps1` has been run.

## After `git init`

```powershell
git add .
pwsh -NoProfile -File .\scripts\Verify-Public.ps1 -TrackedOnly
git diff --cached --check
git diff --cached --name-only
git diff --cached
```

If the check finds a file or pattern, remove the value from the staged diff first. Do not bypass the protection with `git add -f`.

## GitHub settings

- [ ] Secret scanning is enabled.
- [ ] Push protection is enabled.
- [ ] Private vulnerability reports are enabled if the project accepts external security reports.
- [ ] A license is selected and `LICENSE` is included.
- [ ] Repository access permissions and visibility have been checked.

Current GitHub instructions:

- [Secret scanning](https://docs.github.com/en/code-security/concepts/secret-security/secret-scanning)
- [Push protection](https://docs.github.com/en/code-security/concepts/secret-security/push-protection)
- [Ignoring files](https://docs.github.com/en/get-started/getting-started-with-git/ignoring-files)
- [Community health files](https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/creating-a-default-community-health-file)
