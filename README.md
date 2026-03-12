![MusyaLoader](SS14.Launcher/Assets/logo-long.png)

Space Station 14 launcher fork with client-side patching and resource pack support.

![badge](Assets/README/no-stops-no-regrets.svg)
![badge](Assets/README/ensuring-code-integrity.svg)
![badge](Assets/README/works-on-selfmerging.svg)

---

## Русский

### Что это
`MusyaLoader` - форк лаунчера SS14 с дополнительными возможностями для патчинга, приватности и кастомизации клиента.

### Основные функции

#### Игра и патчи
- Интеграция Harmony-патчинга для client/shared/engine сборок.
- Сайдлоад пользовательского кода в клиент.
- Несколько уровней скрытия патчера/патчей (`Hide Level`).
- `Patchless` режим (killswitch для патчей).
- `Throw on patch fail` (аварийный выход при ошибке патча).
- Backports (включая глобальные/целевые) с отдельным выключателем.
- Опция whitelist для `RemoteExecuteCommand`.
- Отключение редиала (форс-редиректов).

#### Приватность и аккаунты
- Multi-account.
- Гостевой режим и настройка guest username.
- Отключение авто-логина в последний аккаунт.
- Подмена/привязка HWID, генерация случайного HWID, auto-delete HWID.
- Opt-out от отправки HWID (`HWID2 opt-out`).
- Отключение Discord RPC.
- Fake RPC username.
- Локальная подмена username для лаунчера/скриншотов (не меняет ник на сервере).

#### Интерфейс и UX
- Локализация интерфейса (включая RU/EN).
- Темы: набор встроенных + Custom theme.
- Кастомные цвета (фон, акцент, текст, popup, градиенты).
- Импорт/экспорт custom theme в `.json`.
- Градиент и декоративный фон как отдельные опции.
- Выбор встроенного шрифта + загрузка своего `.ttf/.otf`.
- Настройка отображаемых полей в списке серверов (time/players/map/mode/ping).
- Рандомизация заголовка окна, хедера и сообщений подключения.

#### Логи и отладка
- Раздельные тумблеры логов: client/launcher/verbose/patcher/trace/debug.
- Раздельный лог патчера (`client.marsey.log`).
- Открытие папки логов из UI.
- Dump CVars.
- Dump ресурсов/ассетов (через Resource Dumper) + кнопка открытия папки дампов.

#### Обновления лаунчера
- Встроенный раздел обновлений лаунчера.
- Выбор GitHub-репозитория обновлений.
- Фильтры `Release / Pre-Release / All`.
- Установка выбранной версии из списка.
- Автообновление и уведомления об обновлениях.

### Быстрый старт (сборка)
1. Установить `.NET 10 SDK`.
2. Клонировать репозиторий с сабмодулями:
   `git clone --recurse-submodules https://github.com/MusyaCliento/MusyaLoader.git`
3. Собрать:
   `python publish.py windows --x64-only`
   `python publish.py linux --x64-only`
   `python publish.py osx`
4. Распаковать архив из `MusyaLoader_YourOS.zip` и запустить.

### Resource Packs: документация

#### Куда класть
Паки лежат в папке:
`Marsey/ResourcePacks/<ИмяПака>`

#### Минимальная структура пака
```text
Marsey/
  ResourcePacks/
    MyPack/
      meta.json
      icon.png                (необязательно)
      Resources/
        Textures/
        Locale/
```

#### Обязательный `meta.json` в корне пака
`meta.json` в корне папки пака обязателен, иначе пак не загрузится.

Пример:
```json
{
  "name": "My Pack",
  "description": "My custom textures and locale",
  "target": ""
}
```

Поля:
- `name` - обязательно.
- `description` - рекомендуется.
- `target` - fork id (если пусто, применяется без привязки к конкретному форку).

#### Правило для `.rsi` (ВАЖНО)
Если меняешь текстуры внутри `.rsi`-папки, рядом с изменёнными файлами должен лежать корректный `meta.json` этой `.rsi`.

Пример:
```text
Resources/
  Textures/
    Mobs/
      Ghosts/
        ghost_human.rsi/
          meta.json
          icon.png
          animated.png
          inhand-left.png
          inhand-right.png
```

Без `meta.json` у `.rsi` клиент может невалидно читать спрайт/состояния, и замена будет работать некорректно или не сработает.

#### Как подменяются ресурсы
- Подмена идёт по относительному пути внутри `Resources/`.
- Файлы `meta.json` и `icon.png` в корне пака не подменяют игровые ресурсы (это метаданные пака).
- Для локализации можно класть свои `.ftl` в `Resources/Locale/<locale>/...`.

---

## English

### What it is
`MusyaLoader` is an SS14 launcher fork with additional patching, privacy, and client customization features.

### Main features

#### Game and patching
- Harmony-based patching for client/shared/engine assemblies.
- Custom code sideloading into the client.
- Multiple patcher visibility levels (`Hide Level`).
- `Patchless` mode (patch killswitch).
- `Throw on patch fail` safety toggle.
- Backports support (global and targeted).
- `RemoteExecuteCommand` whitelist option.
- Redial/forced reconnect disable.

#### Privacy and account controls
- Multi-account support.
- Guest mode with custom guest username.
- Disable auto-login to last account.
- HWID override/bind/random generation + HWID auto-delete.
- HWID send opt-out (`HWID2 opt-out`).
- Disable Discord RPC.
- Fake RPC username.
- Local launcher-side username override (does not change in-game account name).

#### UI and customization
- Launcher localization (including RU/EN).
- Built-in themes + Custom theme mode.
- Custom color controls (background/accent/text/popup/gradient).
- Import/export custom theme as `.json`.
- Gradient and decorative background toggles.
- Built-in font selection + custom `.ttf/.otf` font loading.
- Server list column toggles (time/players/map/mode/ping).
- Randomized window titles/header images/connection messages.

#### Logging and debugging
- Separate logging toggles: client/launcher/verbose/patcher/trace/debug.
- Separate patcher log file (`client.marsey.log`).
- Open log directory from UI.
- CVar dump.
- Resource dump mode + quick open for dump directory.

#### Launcher updates
- Built-in launcher update tab.
- Custom GitHub repository source.
- `Release / Pre-Release / All` filtering.
- Install selected version from list.
- Auto-update and update notifications.

### Quick build
1. Install `.NET 10 SDK`.
2. Clone with submodules:
   `git clone --recurse-submodules https://github.com/MusyaCliento/MusyaLoader.git`
3. Build:
   `python publish.py windows --x64-only`
   `python publish.py linux --x64-only`
   `python publish.py osx`
4. Unzip from `PublishFiles` and run.

### Resource Packs: documentation

#### Where to place packs
Use:
`Marsey/ResourcePacks/<PackName>`

#### Minimal pack structure
```text
Marsey/
  ResourcePacks/
    MyPack/
      meta.json
      icon.png                (optional)
      Resources/
        Textures/
        Locale/
```

#### Required root `meta.json`
Each pack must have `meta.json` in the pack root, otherwise it will not load.

Example:
```json
{
  "name": "My Pack",
  "description": "My custom textures and locale",
  "target": ""
}
```

Fields:
- `name` - required.
- `description` - recommended.
- `target` - fork id (empty means not tied to a specific fork).

#### `.rsi` rule (IMPORTANT)
If you override textures inside an `.rsi` directory, keep a valid `.rsi/meta.json` next to the changed textures.

Example:
```text
Resources/
  Textures/
    Mobs/
      Ghosts/
        ghost_human.rsi/
          meta.json
          icon.png
          animated.png
          inhand-left.png
          inhand-right.png
```

Without `.rsi/meta.json`, sprite states/metadata may be invalid and overrides can fail or behave incorrectly.

#### How overrides are resolved
- Overrides are matched by relative path inside `Resources/`.
- Pack-root `meta.json` and `icon.png` are treated as pack metadata.
- Localization overrides can be provided via `Resources/Locale/<locale>/...` `.ftl` files.
