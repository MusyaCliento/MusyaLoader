# Resource Packs

## Русский

### Где лежат паки
Лаунчер ищет паки в одной из папок:

- `Marsey/ResourcePacks/<ИмяПака>`

### Минимальная структура
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

### Обязательный `meta.json`
В корне пака должен быть `meta.json`, иначе пак не загрузится.

Пример:
```json
{
  "name": "My Pack",
  "description": "My custom textures and locale",
  "target": ""
}
```

Поля:
- `name` - название пака.
- `description` - описание пака.
- `target` - `fork id`, для которого предназначен пак. Пустая строка означает, что пак не привязан к конкретному форку.

### Как работает подмена
- Подмена идёт по относительному пути внутри `Resources/`.
- `meta.json` и `icon.png` в корне пака считаются метаданными и не заменяют игровые файлы.
- Локализацию можно подменять через `Resources/Locale/<locale>/...` и `.ftl` файлы.
- Порядок паков важен: если несколько паков меняют один и тот же ресурс, выше стоящий в списке пак имеет приоритет.

### Важное правило для `.rsi`
Если вы заменяете файлы внутри `.rsi`-папки, рядом должен лежать корректный `meta.json` именно этой `.rsi`.

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

Без `.rsi/meta.json` клиент может некорректно прочитать состояния спрайта, и замена будет работать неправильно или не сработает вообще.

### Включение в лаунчере
1. Откройте вкладку `Resource Packs`.
2. Положите пак в папку `Marsey/ResourcePacks/`.
3. Нажмите обновление списка, если пак не появился сразу.
4. Включите пак и при необходимости поднимите его выше в порядке загрузки.

### Полезно знать
- Для отладки можно использовать dump ресурсов из настроек лаунчера.
- В debug-настройках есть опция отключения строгой проверки `target` для паков.

## English

### Location
Put packs in:

- `Marsey/ResourcePacks/<PackName>`

### Minimal structure
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

### Required `meta.json`
Each pack must contain a root `meta.json`.

Example:
```json
{
  "name": "My Pack",
  "description": "My custom textures and locale",
  "target": ""
}
```

- `name`: pack name.
- `description`: optional description.
- `target`: target fork id. Empty means no fork restriction.

### How overriding works
- Overriding is matched by relative path inside `Resources/`.
- `meta.json` and `icon.png` in the pack root are treated as metadata and do not replace game files.
- Localization can be overridden through `Resources/Locale/<locale>/...` and `.ftl` files.
- Pack order matters: if several packs change the same resource, the pack placed higher in the list has priority.

### Important rule for `.rsi`
If you replace files inside an `.rsi` folder, a correct `meta.json` for that exact `.rsi` must be placed next to them.

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

Without `.rsi/meta.json`, the client may read sprite states incorrectly, and the override will work incorrectly or not work at all.

### Enabling in the launcher
1. Open the `Resource Packs` tab.
2. Put the pack into `Marsey/ResourcePacks/`.
3. Refresh the list if the pack does not appear immediately.
4. Enable the pack and move it higher in the load order if needed.

### Useful to know
- For debugging, you can use the resource dump from launcher settings.
- There is a debug setting that disables strict `target` checking for packs.
