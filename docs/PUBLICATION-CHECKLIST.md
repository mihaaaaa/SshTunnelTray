# Чек-лист публикации на GitHub

[Русский](PUBLICATION-CHECKLIST.md) | [English](PUBLICATION-CHECKLIST.en.md)

## До первого коммита

- [ ] В рабочем дереве нет реального `SshTunnelTray.json` среди добавляемых файлов.
- [ ] Нет приватных ключей, сертификатов, `.env`, credentials, паролей, токенов и логов.
- [ ] Нет `bin/`, `obj/`, `publish/`, `.pdb`, `.exe` и локальных файлов IDE.
- [ ] Скриншоты показывают только пустой или демонстрационный профиль.
- [ ] Рабочие адреса, логины, пути и названия серверов заменены на placeholders.
- [ ] Выполнен `pwsh -NoProfile -File .\scripts\Verify-Public.ps1`.

## После `git init`

```powershell
git add .
pwsh -NoProfile -File .\scripts\Verify-Public.ps1 -TrackedOnly
git diff --cached --check
git diff --cached --name-only
git diff --cached
```

Если проверка нашла файл или шаблон, сначала удалите значение из staged diff. Не обходите защиту через `git add -f`.

## В настройках GitHub

- [ ] Включен Secret scanning.
- [ ] Включен push protection.
- [ ] Включены private vulnerability reports, если проект принимает внешние сообщения о безопасности.
- [ ] Выбрана лицензия и добавлен `LICENSE`.
- [ ] Проверены права доступа и visibility репозитория.

Актуальные инструкции GitHub:

- [Secret scanning](https://docs.github.com/en/code-security/concepts/secret-security/secret-scanning)
- [Push protection](https://docs.github.com/en/code-security/concepts/secret-security/push-protection)
- [Ignoring files](https://docs.github.com/en/get-started/getting-started-with-git/ignoring-files)
- [Community health files](https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/creating-a-default-community-health-file)
