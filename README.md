![MusyaLoader](SS14.Launcher/Assets/logo-long.png)

Space Station 14 launcher fork with patching, proxy, resource pack, and custom engine support.

![badge](Assets/README/no-stops-no-regrets.svg)
![badge](Assets/README/ensuring-code-integrity.svg)
![badge](Assets/README/works-on-selfmerging.svg)

---

## Русский

### Что это
`MusyaLoader` это форк лаунчера SS14 с упором на кастомизацию клиента, патчи, приватность и дополнительные инструменты для запуска.

### Скачать и запустить
ЕСЛИ ВАМ НЕ НАДО НЕ СОБИРАЙТЕ, А СКАЧАЙТЕ РЕЛИЗ ЕСЛИ ПРОСТО ХОТИТЕ ПОИГРАТЬ
Жмёте на кнопку Releases справа на сайте github и там скачиваете последний под вашу систему, windows, linux и т.д.
Распаковываете архив и так же запускаете exe, shell и т.д. файл под вашу систему.
По поводу 20+ детектов на сам bootstrap ну который "MusyaLoader.exe" я не знаю почему у него столько детектов, можете сами сбилдить и залить на virustotal там будут детекты типо вирус и т.д. может потом починю 

### Ссылки
- GitHub Releases: `https://github.com/MusyaCliento/MusyaLoader/releases`
- Discord: `https://discord.gg/u9d6nGSnse`
- Гайд по ресурс-пакам: [docs/RESOURCE_PACKS.md](docs/RESOURCE_PACKS.md)
- Гайд по кастомным движкам: [docs/CUSTOM_ENGINES.md](docs/CUSTOM_ENGINES.md)

### Основные возможности

#### Патчи и поведение клиента
- Harmony-патчинг client/shared/engine сборок.
- SideLoad пользовательского кода в клиент.
- Несколько уровней скрытия патчера и следов патчей (`Hide Level`).
- `Patchless` режим для запуска без патчей.
- `Throw on patch fail` для аварийной остановки при неуспешном патче.
- Backports и отдельный выключатель глобальных backports.
- Отключение `RemoteExecuteCommand`.
- Отключение redial / форс-переподключений.
- Пропуск privacy policy check при подключении.

#### Аккаунты и приватность
- Multi-account.
- Гостевой режим с отдельным guest username.
- Отключение авто-логина в последний аккаунт.
- Изменение имени выбранного аккаунта в лаунчере и имени, передаваемого клиенту при запуске.
- Привязка HWID к аккаунту, ручная подмена HWID, генерация случайного HWID.
- Авто-удаление HWID перед запуском.
- `HWID2 opt-out` для отказа от отправки HWID на сервер.
- `flYi` / forced user id для HWID-логики.
- Отключение Discord Rich Presence.
- Подмена имени в Discord Rich Presence.

#### Прокси
- Встроенная вкладка `Proxy` с профилями SOCKS5.
- Сохранение, выбор и редактирование нескольких proxy-профилей.
- Проверка proxy: RTT, TCP connect и UDP test.
- Proxy для самого лаунчера.
- Proxy для обновлений лаунчера и GitHub-запросов.
- Proxy для загрузчика через `ALL_PROXY` / `HTTP_PROXY` / `HTTPS_PROXY`.
- Обход региональных ограничений для загрузки Robust build'ов.
- Проксирование игрового UDP через `SS14.ProxyService` и SOCKS5 UDP relay.
- Блокировка запуска игры, если тест proxy провален.
- Debug-режим для `SS14.ProxyService`.

#### Интерфейс и кастомизация
- RU/EN и другие локализации интерфейса.
- Встроенные темы и `Custom Theme`.
- Настройка цветов фона, акцента, текста, popup и градиента.
- Импорт и экспорт кастомной темы в `.json`.
- Отдельные тумблеры для градиента и декоративного фона.
- Выбор встроенного шрифта и подключение собственного `.ttf` / `.otf`.
- Настройка колонок в списке серверов: round time, players, map, mode, ping.
- Рандомизация заголовка окна, хедера и сообщений подключения.

#### Ресурсы, движки и обновления
- Поддержка resource packs с порядком загрузки и быстрым включением/выключением.
- Поддержка custom engines из папки или `.zip`.
- Выбор кастомного движка прямо из лаунчера.
- Встроенный раздел обновлений лаунчера.
- Выбор GitHub-репозитория для обновлений.
- Фильтр `Release / Pre-release / All`.
- Установка выбранной версии из списка.
- Автообновление и уведомления о новых версиях.

#### Логи и отладка
- Раздельные логи: client, launcher, verbose, patcher, trace, debug.
- Отдельный лог патчера (`client.marsey.log`).
- Открытие папки логов из UI.
- Dump ресурсов/ассетов через `Resource Dumper`.
- Открытие папки дампов из UI.
- Очистка скачанных движков и server content из настроек.

### Сборка
1. Установить `.NET 10 SDK`.
2. Клонировать репозиторий с сабмодулями:
   `git clone --recurse-submodules https://github.com/MusyaCliento/MusyaLoader.git`
3. Собрать:
   `python publish.py windows --x64-only` для Windows

   `python publish.py linux --x64-only` для Linux

   `python publish.py osx` для macOS
4. Забрать архив `MusyaLoader_<OS>.zip`, распаковать и запустить.

---

## English

### What it is
`MusyaLoader` is an SS14 launcher fork focused on client customization, patches, privacy, and extra launch-related tools.

### Download and run
IF YOU DO NOT NEED TO BUILD IT, DO NOT BUILD IT, JUST DOWNLOAD A RELEASE IF YOU ONLY WANT TO PLAY
Click the `Releases` button on the right side of the GitHub page and download the latest build for your system, Windows, Linux, etc.
Extract the archive and run the `.exe`, shell script, or other file for your system.
About the 20+ detections on the bootstrap itself, the one called `MusyaLoader.exe`, I do not know why it gets that many detections. You can build it yourself and upload it to VirusTotal, and there will still be detections like virus and so on. Maybe I will fix it later.

### Links
- GitHub Releases: `https://github.com/MusyaCliento/MusyaLoader/releases`
- Discord: `https://discord.gg/u9d6nGSnse`
- Resource packs guide: [docs/RESOURCE_PACKS.md](docs/RESOURCE_PACKS.md)
- Custom engines guide: [docs/CUSTOM_ENGINES.md](docs/CUSTOM_ENGINES.md)

### Main features

#### Patches and client behavior
- Harmony patching for client/shared/engine assemblies.
- SideLoad of user code into the client.
- Multiple levels of patcher and patch trace hiding (`Hide Level`).
- `Patchless` mode for running without patches.
- `Throw on patch fail` for crashing on an unsuccessful patch.
- Backports and a separate global backports toggle.
- Disable `RemoteExecuteCommand`.
- Disable redial / forced reconnects.
- Skip the privacy policy check on connect.

#### Accounts and privacy
- Multi-account.
- Guest mode with a separate guest username.
- Disable auto-login into the last account.
- Change the selected account name in the launcher and the name passed to the client on launch.
- Bind HWID to an account, manually override HWID, generate a random HWID.
- Auto-delete HWID before launch.
- `HWID2 opt-out` to refuse sending HWID to the server.
- `flYi` / forced user id for HWID logic.
- Disable Discord Rich Presence.
- Override the name shown in Discord Rich Presence.

#### Proxy
- Built-in `Proxy` tab with SOCKS5 profiles.
- Save, select, and edit multiple proxy profiles.
- Proxy check: RTT, TCP connect, and UDP test.
- Proxy for the launcher itself.
- Proxy for launcher updates and GitHub requests.
- Proxy for the loader through `ALL_PROXY` / `HTTP_PROXY` / `HTTPS_PROXY`.
- Bypass regional restrictions for Robust build downloads.
- Proxy game UDP through `SS14.ProxyService` and SOCKS5 UDP relay.
- Block game launch if the proxy test fails.
- Debug mode for `SS14.ProxyService`.

#### Interface and customization
- RU/EN and other interface localizations.
- Built-in themes and `Custom Theme`.
- Configure background, accent, text, popup, and gradient colors.
- Import and export a custom theme as `.json`.
- Separate toggles for gradient and decorative background.
- Select a built-in font and connect your own `.ttf` / `.otf`.
- Configure columns in the server list: round time, players, map, mode, ping.
- Randomize the window title, header, and connect messages.

#### Resources, engines, and updates
- Resource pack support with load order and quick enable/disable.
- Support for custom engines from a folder or `.zip`.
- Select a custom engine directly in the launcher.
- Built-in launcher updates section.
- Choose the GitHub repository used for updates.
- `Release / Pre-release / All` filter.
- Install the selected version from the list.
- Auto-update and notifications about new versions.

#### Logs and debugging
- Separate logs: client, launcher, verbose, patcher, trace, debug.
- Separate patcher log (`client.marsey.log`).
- Open the logs folder from the UI.
- Dump resources/assets through `Resource Dumper`.
- Open the dump folder from the UI.
- Clear downloaded engines and server content from settings.

### Build
1. Install `.NET 10 SDK`.
2. Clone with submodules:
   `git clone --recurse-submodules https://github.com/MusyaCliento/MusyaLoader.git`
3. Build:
   `python publish.py windows --x64-only` for Windows

   `python publish.py linux --x64-only` for Linux

   `python publish.py osx` for macOS
4. Take `MusyaLoader_<OS>.zip`, extract it, and run it.
