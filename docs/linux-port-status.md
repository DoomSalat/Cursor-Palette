# Linux-порт Cursor Palette — статус

> Порт на Avalonia UI, .NET 8. Проект `Cursor-Palette.Linux` +
> `Cursor-Palette.Core` (общая логика). Запуск проверен через WSL (Ubuntu) с WSLg.

## Готово

- **Сборка и запуск** — Avalonia UI, .NET 8, кросс-платформенная сборка
- **Галерея пресетов** — ячейки пресетов, "по умолчанию", Add cell с символом "+"
- **Тулбар** — слайдер размера, Apply, Undo, Theme (☀/🌙), Language
- **Редактор пресета** — слоты ролей (17 шт), превью курсоров, кнопка Browse
  для каждого слота, кнопка Clear, имя пресета, слайдер размера, Save/Cancel.
  Открывается из контекстного меню (Edit) с загрузкой существующего пресета
- **Контекстное меню** — Edit, Rename (диалог с TextBox), Move left/right,
  Download (TODO), Delete (диалог подтверждения)
- **Локализация** — JSON-файлы вместо XAML, 6 языков (en/ru/de/es/ja/zh),
  переключатель в шапке. Контекстное меню локализуется через `OnContextMenuOpening`
- **Темы** — Dark/Light через `ThemeDictionaries` в `App.xaml`, цвета сопоставлены
  с WPF-оригиналом (#FF1E1F24 фон dark, #FFF4F5F7 фон light)
- **Drag-and-drop импорт** — перетаскивание .cur/.ani файлов и папок на окно
- **Превью курсоров** — через `CursorPreviewService`
- **Single instance** — через Unix domain socket
- **Screen color picker** — через X11 interop
- **Применение курсоров** — `LinuxCursorService`: Xcursor файлы в `~/.icons/`,
  `gsettings` для переключения темы и размера
- **Toast-уведомления** — всплывающие сообщения после применения пресета/размера
- **О программе** — клик по футеру открывает диалог с названием и лицензией
- **Zoom интерфейса** — кнопки −/+ в тулбаре, `ScaleTransform` на RootGrid,
  сохраняется через `AppState.SetUiScale`
- **Ползунок размера ячеек** — слайдер в тулбаре, `ScaleTransform` на Gallery,
  сохраняется через `AppState.SetGalleryCellScale`
- **Экспорт/импорт пакетов** — контекстное меню Download экспортирует пресет
  в `.cursorpalette`, drag-drop импортирует пакеты через `PresetPackageService`
- **Затемнение + спиннер** — overlay с вращающимся Ellipse во время применения
  пресета/размера
- **Кастомный скроллбар** — тонкий (10px), закруглённый thumb, hover-эффект,
  цвета для dark/light тем
- **Drag-and-drop reorder** — перетаскивание плиток пресетов для изменения порядка,
  сохраняется через `BoardOrderStore`
- **Группы пресетов** — отображение групп, collapse/expand по клику,
  контекстное меню (rename, delete, collapse), цветовые метки через `GroupColors`
- **Проверка обновлений** — фоновая проверка через GitHub API при запуске,
  toast-уведомление при наличии новой версии
- **Paint editor** — окно с pixel canvas (brush/eraser/hotspot), импорт
  PNG/BMP/cursor файлов, zoom, интеграция с PresetEditor слотами
- **Константы** — магические строки и числа вынесены в `private const` по стилю
  WPF-оригинала

## Известные проблемы

### Кириллица/CJK рендеринг — частично починено

1. **JSON файлы локализации** были в Windows-1251 вместо UTF-8 — пересозданы
   из WPF XAML-оригиналов в UTF-8 без BOM (все 6 языков)
2. **Шрифт** — в `App.xaml` задан Inter (из `Avalonia.Fonts.Inter`) с fallback
   на системные `Noto Sans CJK`, `Noto Sans`, `DejaVu Sans` через стиль
   `:is(TemplatedControl)`. Для CJK нужны установленные в системе шрифты:

```bash
sudo apt install -y fonts-noto-cjk fonts-noto-cjk-extra fonts-dejavu
```

### Применение курсоров в систему — реализовано

`LinuxCursorService` записывает Xcursor файлы в `~/.icons/<theme>/cursors/`,
создаёт `index.theme`, и переключает тему через `gsettings set org.gnome.desktop
.interface cursor-theme`. Размер курсора — через `gsettings set org.gnome.desktop
.interface cursor-size`. Поддерживаются GNOME и совместимые окружения.

### Перенос пресетов между Windows и Linux

Пресеты хранятся в платформо-зависимых путях (`%APPDATA%` vs `~/.config`),
ручной перенос невозможен. Нужен механизм синхронизации или импорт/экспорт
настроек.

## Не портировано (TODO)

### 1. Кнопки Export/Import в футере

WPF-оригинал имеет кнопки Export и Import в футере:
- **Export** — открывает `ExportWindow` с выбором пресетов/групп для экспорта
  в `.cursorpalette`, `.zip`, `.tar.gz`, Xcursor theme. В Linux есть только
  Download из контекстного меню (экспорт одного пресета).
- **Import** — открывает `OpenFileDialog` для выбора `.cursorpalette` файла,
  затем `ImportPickerWindow` с выбором пресетов для импорта. В Linux есть
  только drag-drop импорт (без диалога выбора конкретных пресетов).

**Файлы WPF:** `MainWindow.ImportExport.cs`, `ExportWindow.xaml(.cs)`,
`ImportPickerWindow.xaml(.cs)`
**Статус Linux:** drag-drop импорт пакетов есть, кнопок Export/Import нет,
`ExportWindow` и `ImportPickerWindow` не портированы.

### 2. Управление группами

WPF-оригинал имеет полнофункциональное управление группами:
- **GroupEditWindow** — диалог создания/редактирования группы с выбором имени
  и цвета (color swatches из `GroupColors.Palette`)
- **Group toolbar** — нижняя панель, появляющаяся при Ctrl+click выборе пресетов:
  счётчик выбранных, color swatches, поле имени, кнопки Create/Cancel
- **Ctrl+click выбор пресетов** — множественный выбор для последующей группировки
- **Selection badges** (✓) — визуальный индикатор выбранных пресетов
- **Контекстное меню "Create Group"** — по правому клику на фоне галереи
- **Контекстное меню "Assign to Group"** — подменю со списком групп для
  назначения пресета в группу
- **Контекстное меню "Remove from Group"** — для пресетов внутри группы
- **Контекстное меню "Edit Group"** — открытие GroupEditWindow для группы
- **Inline rename группы** — двойной клик по имени группы для переименования
- **Group outline** — пунктирная рамка вокруг сгруппированных пресетов
  (используя `GroupColors.ResolveHex`)

**Файлы WPF:** `MainWindow.Groups.cs`, `GroupEditWindow.xaml(.cs)`,
`MainWindow.Gallery.cs` (CreatePresetCell, CreateGroupCell)
**Статус Linux:** отображение групп, collapse/expand, delete, toggle collapse
  в контекстном меню. `GroupColorHex` передаётся в `BoardItem`, но не
  используется в XAML-шаблоне. GroupEditWindow не портирован.

### 3. UI ячеек пресетов

- **Mixed badge (🧩)** — индикатор для пресетов со смешанными ролями
  (RoleRefs). `BoardItem.IsMixed` вычисляется, но не отображается в XAML.
- **Tooltips на ячейках** — WPF показывает tooltip с именем пресета и
  подсказкой контекстного меню. В Linux tooltips отсутствуют.
- **Double-click to edit** — WPF открывает редактор пресета по двойному клику.
  В Linux только через контекстное меню (Edit).
- **Hover эффект** — WPF меняет фон ячейки при наведении (`BrushSurfaceHover`).
  В Linux hover не реализован.
- **Selection border** — утолщённая рамка для выбранных пресетов
  (`SelectionBorderThickness = 4`).

**Файлы WPF:** `MainWindow.Gallery.cs` (CreatePresetCell, CreateDefaultCell)
**Статус Linux:** базовые ячейки есть, расширенные визуальные эффекты нет.

### 4. Download System Cursors

WPF-оригинал имеет контекстное меню на ячейке "Windows Default":
- **Download System Cursors** → подменю:
  - **PNG/GIF** — экспорт системных курсоров как PNG/GIF изображения
  - **CUR/ANI** — экспорт системных курсоров как .cur/.ani файлы

Использует `RegistryCursorService.GetWindowsDefaultValues()` и
`DownloadSystemCursors(asImages)`.

**Файлы WPF:** `MainWindow.Gallery.cs` (CreateDefaultCell),
`MainWindow.PresetActions.cs` (DownloadSystemCursors)
**Статус Linux:** не портировано. На Linux нет реестра Windows, но можно
  экспортировать курсоры из X11/Xcursor.

### 5. Индикатор обновлений

WPF-оригинал имеет полноценный UI обновлений в футере:
- **UpdateSpinner** — вращающийся спиннер во время проверки
- **"Checking..." label** — текст во время проверки
- **"Update Available" button** — кнопка-индикатор при наличии обновления
  (открывает `UpdateWindow` с changelog и кнопкой скачивания)
- **"✓ Up to date" label** — текст при актуальной версии (клик = перепроверка)
- **UpdateWindow** — диалог с информацией о новой версии, changelog,
  кнопкой "Download" и "Open in browser"

**Файлы WPF:** `MainWindow.Updates.cs`, `UpdateWindow.xaml(.cs)`
**Статус Linux:** только toast-уведомление при наличии обновления.
  `UpdateWindow` не портирован.

### 6. Info/Help dialogs

WPF-оригинал имеет кнопку "ⓘ" в шапке и в каждом диалоге:
- **InfoHelpWindow** — диалог с справочной информацией по текущему окну
  (Main, Editor, Paint, Hotspot, Export, Import, RolePicker, PresetPicker, About)
- **HelpTextService** — загружает тексты справки из JSON

**Файлы WPF:** `InfoHelpWindow.xaml(.cs)`, `HelpTextService.cs`
**Статус Linux:** не портировано.

### 7. Open Folder After Download toggle

WPF-оригинал имеет кнопку-переключатель в футере:
- **OpenFolderToggle** — иконка папки, переключает
  `AppState.OpenFolderAfterDownload`
- При включении автоматически открывает папку после скачивания/экспорта
- Используется во всех окнах (MainWindow, PresetEditor, PaintEditor, Export)

**Файлы WPF:** `MainWindow.xaml`, `MainWindow.Updates.cs`,
`AppState.cs` (GetOpenFolderAfterDownload/SetOpenFolderAfterDownload)
**Статус Linux:** не портировано. `AppState.OpenFolderAfterDownload`
  существует в Core, но UI toggle отсутствует.

### 8. GitHub icon link

WPF-оригинал имеет иконку GitHub в футере:
- Клик открывает `AppInfo.GitHubUrl` в браузере

**Файлы WPF:** `MainWindow.xaml` (OnGitHubIconClick)
**Статус Linux:** не портировано.

### 9. Drag Ghost и индикаторы reorder

WPF-оригинал имеет расширенные визуальные индикаторы при drag-and-drop:
- **DragGhost** — полупрозрачная ячейка-призрак, следующая за курсором
  при перетаскивании пресета/группы
- **ReorderInsertionLine** — вертикальная линия, показывающая позицию
  вставки при reordering
- **WindowDropIndicator** — пунктирная рамка вокруг окна при drag-over
  файлов извне
- **GroupAttachIndicator** — индикатор-рамка при перетаскивании пресета
  на группу (для добавления в группу)
- **Group drag-and-drop** — перетаскивание групп для reordering

**Файлы WPF:** `MainWindow.DragDrop.cs`, `MainWindow.xaml`
**Статус Linux:** базовый reorder пресетов через drag-drop есть,
  визуальные индикаторы и drag group нет.

### 10. AboutWindow (отдельное окно)

WPF-оригинал имеет отдельное `AboutWindow` (не диалог по клику на футер):
- Открывается из контекстного меню или кнопки "ⓘ"
- Показывает версию, автора, лицензию, ссылку на GitHub
- Имеет кнопку "ⓘ" для открытия справки об About

**Файлы WPF:** `AboutWindow.xaml(.cs)`
**Статус Linux:** есть диалог "О программе" по клику на футер
  (программно построенный), отдельного `AboutWindow` нет.

### 11. Language dropdown menu

WPF-оригинал открывает выпадающее меню при клике на кнопку языка:
- `ContextMenu` с пунктами для каждого языка
- Отображает `DisplayName` каждого языка
- Checkable items с галочкой у текущего языка

**Файлы WPF:** `MainWindow.Settings.cs` (OnLanguageButtonClick)
**Статус Linux:** циклическое переключение языков по клику
  (без выпадающего меню).

### 12. Window state persistence

WPF-оригинал сохраняет размер окна:
- `AppState.SetMainWindowSize(Width, Height)` при закрытии
- `AppState.GetMainWindowWidth/Height` при открытии

**Файлы WPF:** `MainWindow.xaml.cs` (OnClosed, конструктор)
**Статус Linux:** не портировано (размер окна фиксирован в XAML).
