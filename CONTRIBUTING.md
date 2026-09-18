# Участие в проекте

## Перед изменениями

1. Создайте отдельную ветку.
2. Не копируйте в рабочее дерево реальные конфигурации, приватные ключи и логи без необходимости.
3. Для примеров используйте `example.com`, `your-user`, `YOUR_NAME` и тестовые порты.

## Проверки

Из корня проекта выполните:

```powershell
pwsh -NoProfile -File .\scripts\Verify-Public.ps1
dotnet restore --runtime win-x64
dotnet build .\SshTunnelTray.csproj --configuration Release --runtime win-x64 --no-restore
```

Если Git уже инициализирован, перед коммитом дополнительно выполните проверку индекса:

```powershell
pwsh -NoProfile -File .\scripts\Verify-Public.ps1 -TrackedOnly
git diff --cached --check
git diff --cached --name-only
git diff --cached
```

## Pull request

В описании укажите:

- что изменилось и зачем;
- как это проверялось;
- затронуты ли конфигурация, хранение секретов или запуск SSH;
- есть ли изменения интерфейса и приложен ли обезличенный скриншот.

Не прикладывайте реальные `SshTunnelTray.json`, `.cmd`, ключи, пароли, логи или скриншоты рабочего окружения.
