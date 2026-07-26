# Linux-порт Cursor Palette — статус

> Порт на Avalonia UI, .NET 8. Проект `Cursor-Palette.Linux` +
> `Cursor-Palette.Core` (общая логика). Запуск проверен через WSL (Ubuntu) с WSLg.

## Готово

- **Сборка и запуск** — Avalonia UI, .NET 8, кросс-платформенная сборка
- **Галерея пресетов** — ячейки пресетов, "по умолчанию", Add cell с символом "+"
- **Тулбар** — слайдер размера, Apply, Undo, Theme (☀/🌙), Language
- **Редактор пресета** — слоты ролей (17 шт), превью курсоров, кнопка Browse
  для каждого слота, кнопка Clear, имя пресета, слайдер размера, scaling checkbox,
  Save/Cancel. Открывается из контекстного меню (Edit) и двойным кликом
- **Контекстное меню** — Edit, Rename (диалог с TextBox), Move left/right,
  Download, Download System Cursors, Collapse/Expand, Random pick, Edit group,
  Delete (диалог подтверждения). Локализуется через `OnContextMenuOpening`
- **Локализация** — JSON-файлы вместо XAML, 6 языков (en/ru/de/es/ja/zh),
  переключатель в шапке (выпадающее меню ContextMenu с галочкой у текущего)
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
- **Экспорт/импорт пакетов** — кнопки Export и Import в футере.
  Export открывает `ExportWindow` с выбором пресетов/групп для экспорта
  в `.cursorpalette`, `.zip`, Xcursor theme, raw cursor files.
  Import — диалог выбора файла + импорт через `PresetPackageService`.
  Также Download из контекстного меню (экспорт одного пресета)
- **Затемнение + спиннер** — overlay с вращающимся Ellipse во время применения
  пресета/размера
- **Кастомный скроллбар** — тонкий (10px), закруглённый thumb, hover-эффект,
  цвета для dark/light тем
- **Drag-and-drop reorder** — перетаскивание плиток пресетов для изменения порядка,
  сохраняется через `BoardOrderStore`. Drag ghost и reorder insertion line есть
- **Группы пресетов** — отображение групп, collapse/expand по клику,
  контекстное меню (collapse/expand, random pick, edit group, consolidate,
  ungroup, delete), цветовые метки через `GroupColors` (левая полоса + бейдж),
  `GroupEditWindow` портирован, assign to group (подменю), remove from group,
  Ctrl+click выбор пресетов, selection badges (✓), group toolbar с color
  swatches и кнопками Create/Cancel, контекстное меню "Create Group" на фоне
- **Проверка обновлений** — фоновая проверка через GitHub API при запуске,
  toast-уведомление при наличии новой версии, кнопка-индикатор в шапке
- **Paint editor** — окно с pixel canvas (brush/eraser/hotspot), импорт
  PNG/BMP/cursor файлов, zoom, интеграция с PresetEditor слотами
- **Константы** — магические строки и числа вынесены в `private const` по стилю
  WPF-оригинала
- **Масштаб курсора (scaling)** — checkbox в тулбаре и в редакторе пресета,
  сохраняется в пресете, применяется при выборе
- **Mixed badge (🧩)** — индикатор для пресетов со смешанными ролями (RoleRefs).
  `BoardItem.IsMixed` вычисляется и отображается в XAML
- **Scaling icon (📐)** — индикатор на ячейке пресета, если scaling включён
- **Hover эффект** — фон ячейки меняется при наведении (`BrushSurfaceHover`)
- **Group color indicator** — левая цветная полоса + бейдж на ячейках
  пресетов, принадлежащих группе
- **Info/Help dialog** — кнопка "ⓘ" в шапке открывает `InfoHelpWindow`
  со справкой (`HelpTextService.Get("Main")`)
- **Open Folder After Download toggle** — кнопка-переключатель 📂 в футере,
  переключает `AppState.OpenFolderAfterDownload`
- **GitHub icon link** — кнопка в футере, клик открывает `AppInfo.GitHubUrl`
- **Window state persistence** — размер окна сохраняется/восстанавливается
  через `AppState.SetMainWindowSize` / `GetMainWindowWidth/Height`
- **Случайный пресет** — клик по логотипу "Cursor Palette" применяет случайный
  пресет со всего табло; пункт "Random pick" в контекстном меню группы
  применяет случайный пресет из группы

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

### 1. Управление группами — частично

WPF-оригинал имеет полнофункциональное управление группами:
- **Group drag-and-drop** — перетаскивание групп для reordering
- **GroupAttachIndicator** — индикатор-рамка при перетаскивании пресета
  на группу (для добавления в группу)

**Файлы WPF:** `MainWindow.Groups.cs`, `MainWindow.Gallery.cs`
**Статус Linux:** `GroupEditWindow` портирован. Collapse/expand, delete, random
  pick, edit group, consolidate, ungroup, assign to group, remove from group
  в контекстном меню есть. Цветовая полоса + бейдж на ячейках отображаются.
  Ctrl+click выбор пресетов, selection badges (✓), group toolbar (с color
  swatches, полем имени, кнопками Create/Cancel) — портированы.
  Контекстное меню "Create Group" на фоне галереи — портировано.
  Group drag, group attach indicator — не портированы.

### 2. UI ячеек пресетов — полностью

**Файлы WPF:** `MainWindow.Gallery.cs` (CreatePresetCell, CreateDefaultCell)
**Статус Linux:** базовые ячейки есть, mixed badge (🧩), scaling icon (📐),
  hover эффект, group color indicator, selection border + selection badges (✓),
  tooltip с именем пресета и подсказкой "ПКМ — меню" — всё отображается.

### 3. Paint editor — частично

WPF-оригинал (v2.1.0) имеет полнофункциональный Paint editor:
- **Brush/Eraser** — рисование по пикселям ✅
- **Hotspot** — перетаскивание маркера + 9 кнопок быстрой установки ✅
  (но без кнопок быстрой установки, только маркер)
- **Fill** — заливка области одним цветом ❌
- **Eyedropper** — пипетка (Alt+клик) ❌
- **Color wheel / color square** — выбор цвета (переключатель ◐/■) ❌
- **Undo/Redo** — история изменений (Ctrl+Z / Ctrl+Y) ❌
- **Background Ref** — полупрозрачная картинка позади спрайта ❌
- **Canvas tool** — визуальная растяжка холста за края/углы ❌
- **Move tool** — перетаскивание спрайта + джойстик "прижать к краю" ❌
- **Hand tool** — панорамирование ❌
- **Animation timeline** — до 60 кадров для .ani ❌
- **Shift+клик** — прямая линия от последней точки ❌
- **Import image** — диалог с режимами Over/Replace ❌ (есть только простой импорт)
- **Paint editor для пустых слотов** — рисование с нуля ❌
- **Горячие клавиши** инструментов (V/H/B/E/G/C/O) ❌
- **Сохранение между запусками** — последний инструмент, зум, позиция, цвет ❌

**Файлы WPF:** `PaintEditorWindow.xaml(.cs)`, `ColorWheelControl.xaml(.cs)`
**Статус Linux:** базовый pixel canvas с brush/eraser/hotspot и импортом.
  Fill, eyedropper, color wheel, undo/redo, background ref, canvas/move/hand
  tools, animation timeline, shift+line — не портированы.

### 4. Микс ролей из существующих пресетов (RoleRefs)

WPF-оригинал (v1.1.2) позволяет брать роль из другого пресета:
- **RolePickerWindow** — двухшаговый picker: выбор пресета-источника → выбор роли
- **Тумблер "только текущая роль"**
- **Иконка-цепочка** на слоте с тултипом источника ссылки
- **Правка hotspot на ссылочном слоте** — отвязывает роль в собственную копию

**Файлы WPF:** `PresetEditorWindow.xaml(.cs)`, `RolePickerWindow.xaml(.cs)`
**Статус Linux:** `IsMixed` вычисляется и отображается (🧩), но UI для создания
  ссылочных ролей (RolePicker) не портирован.

### 5. Индикатор обновлений — частично

WPF-оригинал имеет полноценный UI обновлений в футере:
- **UpdateSpinner** — вращающийся спиннер во время проверки
- **"Checking..." label** — текст во время проверки
- **"Update Available" button** — кнопка-индикатор при наличии обновления
  (открывает `UpdateWindow` с changelog и кнопкой скачивания)
- **"✓ Up to date" label** — текст при актуальной версии (клик = перепроверка)
- **UpdateWindow** — диалог с информацией о новой версии, changelog,
  кнопкой "Download" и "Open in browser"

**Файлы WPF:** `MainWindow.Updates.cs`, `UpdateWindow.xaml(.cs)`
**Статус Linux:** фоновая проверка через GitHub API, toast-уведомление,
  кнопка-индикатор в шапке. `UpdateWindow` не портирован.

### 6. Download System Cursors — частично

WPF-оригинал имеет подменю с двумя форматами:
- **PNG/GIF** — экспорт системных курсоров как изображения
- **CUR/ANI** — экспорт системных курсоров как .cur/.ani файлы

**Файлы WPF:** `MainWindow.PresetActions.cs` (DownloadSystemCursors)
**Статус Linux:** есть `OnMenuDownloadSystem` — экспортирует текущие
  системные курсоры как .xcursor файлы в Downloads. PNG/GIF и CUR/ANI
  форматы не поддерживаются.

### 7. WindowDropIndicator

WPF-оригинал показывает пунктирную рамку вокруг окна при drag-over
файлов извне (визуальная реакция на перетаскивание).

**Файлы WPF:** `MainWindow.DragDrop.cs`, `MainWindow.xaml`
**Статус Linux:** не портировано. Drop работает, но визуальной реакции нет.

### 8. AboutWindow (отдельное окно)

WPF-оригинал имеет отдельное `AboutWindow` (не диалог по клику на футер):
- Открывается из контекстного меню или кнопки "ⓘ"
- Показывает версию, автора, лицензию, ссылку на GitHub
- Имеет кнопку "ⓘ" для открытия справки об About

**Файлы WPF:** `AboutWindow.xaml(.cs)`
**Статус Linux:** есть диалог "О программе" по клику на футер
  (программно построенный), отдельного `AboutWindow` нет.
