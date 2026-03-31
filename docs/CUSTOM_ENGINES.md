# Custom Engines

## Русский

### Что поддерживается
Лаунчер умеет подхватывать кастомные движки:

- из папки с файлами движка;
- из `.zip` архива;
- из `.zip` с отдельным sidecar-файлом `*.engine.json`.

### Где искать движки
Папка с движками:

- `Marsey/Engines/`

### Вариант 1: движок как папка
```text
Marsey/
  Engines/
    MyEngine/
      engine.json
      icon.png                (необязательно)
      Robust.Client.zip       (необязательно, если рядом уже готовая структура)
      Robust.Client/
      Robust.Shared/
      Resources/
```

### Вариант 2: движок как архив
```text
Marsey/
  Engines/
    MyEngine.zip
```

Метаданные можно положить:
- либо внутрь архива как `engine.json`;
- либо рядом как `MyEngine.zip.engine.json`.

### Формат `engine.json`
Пример:
```json
{
  "name": "Custom Engine",
  "description": "My custom engine",
  "icon": "icon.png",
  "clientZip": "Robust.Client_win-x64.zip",
  "signature": ""
}
```

Поля:
- `name` - отображаемое имя.
- `description` - описание.
- `icon` - путь до иконки.
- `clientZip` - какой архив или подпапку использовать как клиент движка.
- `signature` - сигнатура движка, если используется.

### Как лаунчер выбирает файлы
- Если указан `clientZip`, он используется в первую очередь.
- Если в папке есть готовый набор файлов движка, лаунчер может собрать временный zip сам.
- Если найдено несколько `Robust.Client_<rid>.zip`, лаунчер попытается выбрать лучший вариант под текущую платформу.

### Как включить
1. Положите движок в `Marsey/Engines/`.
2. Откройте вкладку `Engines`.
3. Нажмите обновление списка.
4. Выберите нужный движок и активируйте его.

### Важно
- Некоторые кастомные движки требуют отключения проверки подписи движка.
- Если движок не появляется, сначала проверьте `engine.json`, структуру папки и наличие client zip.

### Пример публикации движка
```bash
dotnet publish Robust.Client/Robust.Client.csproj -c Release -r win-x64 -p:FullRelease=True -p:TargetOS=Windows
```

## English

### What is supported
The launcher can load custom engines:

- from a folder with engine files;
- from a `.zip` archive;
- from a `.zip` with a separate sidecar file `*.engine.json`.

### Where to put engines
Engine folder:

- `Marsey/Engines/`

### Option 1: engine as a folder
```text
Marsey/
  Engines/
    MyEngine/
      engine.json
      icon.png                (optional)
      Robust.Client.zip       (optional, if a ready structure is already present next to it)
      Robust.Client/
      Robust.Shared/
      Resources/
```

### Option 2: engine as an archive
```text
Marsey/
  Engines/
    MyEngine.zip
```

Metadata can be placed:
- either inside the archive as `engine.json`;
- or next to it as `MyEngine.zip.engine.json`.

### `engine.json` format
Example:
```json
{
  "name": "Custom Engine",
  "description": "My custom engine",
  "icon": "icon.png",
  "clientZip": "Robust.Client_win-x64.zip",
  "signature": ""
}
```

Fields:
- `name` - display name.
- `description` - description.
- `icon` - path to the icon.
- `clientZip` - which archive or subfolder to use as the engine client.
- `signature` - engine signature, if used.

### How the launcher chooses files
- If `clientZip` is specified, it is used first.
- If the folder already contains a ready set of engine files, the launcher can build a temporary zip by itself.
- If multiple `Robust.Client_<rid>.zip` files are found, the launcher will try to choose the best one for the current platform.

### How to enable it
1. Put the engine into `Marsey/Engines/`.
2. Open the `Engines` tab.
3. Refresh the list.
4. Select the engine you need and activate it.

### Important
- Some custom engines require disabling engine signature verification.
- If the engine does not appear, first check `engine.json`, the folder structure, and whether a client zip is present.

### Engine publish example
```bash
dotnet publish Robust.Client/Robust.Client.csproj -c Release -r win-x64 -p:FullRelease=True -p:TargetOS=Windows
```
