# План портирования Cursor Palette на Linux (Avalonia)

## Обзор

Cursor Palette — WPF-приложение (.NET 8, Windows-only) для управления курсорами.
Цель: Linux-версия на **Avalonia UI** с переиспользованием существующей бизнес-логики.

## Структура проекта

```
Cursor-Palette/
├── Cursor-Palette.Core/          # общая логика (net8.0)
├── Cursor-Palette.Wpf/           # Windows UI (net8.0-windows)
└── Cursor-Palette.Avalonia/      # Linux UI (net8.0, Avalonia)
```

## Что уже кросс-платформенное

- `AniCursorReader` / `AniCursorWriter` — чтение/запись `.cur` / `.ani`
- `XcursorWriter` — запись Xcursor-формата, маппинг ролей → алиасы
- `CursorCanvasService`, `CursorHotspotService`, `ImageToCursorService`
- `PresetStore`, `GroupStore`, `BoardOrderStore`, `PresetPackageService`
- `ArchiveImportService`, `LocalizationManager`
- Все `Models/` — модели данных без Windows-зависимостей

## Windows-зависимости и замены

| Сервис | Windows API | Linux-замена |
|---|---|---|
| `RegistryCursorService` | Registry, `SystemParametersInfo` | Установка Xcursor-темы в `~/.icons/` + `gsettings` / `xfconf-query` / `kwriteconfig5` |
| `AppPaths` | `SHGetKnownFolderPath` (shell32.dll) | XDG-пути (`XDG_DATA_HOME`, `XDG_DOWNLOAD_DIR`) |
| `NativeColorPicker` | `GetCursorPos`, `GetPixel` (user32/gdi32) | X11: `XGetImage`; Wayland: `xdg-desktop-portal` |
| `ExplorerService` | COM `Shell.Application`, `explorer.exe` | `xdg-open`, `nautilus --select` |
| `SingleInstanceService` | `RegisterWindowMessage`, `PostMessage` | `Mutex` + Unix domain socket / DBus |
| `UpdateChecker` | `.exe` скачивание | AppImage / `.tar.gz` / Flatpak |
| Все XAML views + `ToastService` + `ThemeManager` | WPF | Avalonia AXAML |

---

## Этап 1. Разделение проекта — Core

Создать `Cursor-Palette.Core` (net8.0) и перенести туда всю кросс-платформенную логику.

Добавить интерфейсы для платформозависимых сервисов:

- `ICursorService` — применение/чтение/сброс курсоров
- `IPlatformPaths` — пути к папкам (Downloads, Root, PresetsDir, StateDir, и т.д.)
- `IScreenColorPicker` — пипетка (цвет пикселя экрана)
- `IFileExplorer` — открытие файлового менеджера с выделением файла
- `ISingleInstance` — гарантия одного экземпляра
- `IUpdateChecker` — проверка обновлений

В WPF-проекте реализовать Windows-версии (обёртки над существующим кодом).

## Этап 2. Создание Avalonia-проекта

### NuGet-зависимости

- `Avalonia` 11.x, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`
- `Avalonia.Xaml.Behaviors` — для drag-and-drop
- `SharpCompress` — архивы (уже используется)

### Структура

```
Cursor-Palette.Avalonia/
├── App.axaml / App.axaml.cs
├── ViewModels/
├── Views/         — MainWindow, PresetEditWindow, ExportWindow, и т.д.
├── Controls/      — ColorWheelControl, PresetTile, и т.д.
├── Services/      — LinuxCursorService, LinuxPaths, и т.д.
├── Assets/        — иконки, картинки
├── Resources/     — локализация
└── Cursor-Palette.Avalonia.csproj
```

## Этап 3. Перенос UI (WPF XAML → Avalonia AXAML)

### Ключевые отличия WPF и Avalonia

| WPF | Avalonia |
|---|---|
| `xmlns` microsoft schema | `https://github.com/avaloniaui` |
| `DependencyProperty` | `StyledProperty<T>` / `DirectProperty<T>` |
| `BitmapImage` / `WriteableBitmap` | `Bitmap` / `WriteableBitmap` (другой API) |
| `Triggers` | `VisualStateManager` или C#-код |
| `Effect` (DropShadowEffect) | `DropShadowEffect` (API отличается) |
| `FileDialog` | `StorageProvider` API |
| `OnRender` в UserControl | `override Render(DrawingContext)` |
| `Application.Resources` | `Styles` в `App.axaml` |

### Порядок переноса экранов

1. **MainWindow** — галерея пресетов, заголовок, футер
2. **PresetEditWindow** — редактор пресетов (17 ролей, drag-and-drop)
3. **PivotPointEditor** — редактор hotspot (превью + 3x3 pad)
4. **PaintEditor** — самый сложный экран:
   - Canvas с пиксельным редактированием
   - Brush / Eraser / Fill / Eyedropper / Hotspot / Canvas / Move tools
   - Color wheel + color square
   - Animation timeline (до 60 кадров)
   - Background reference
   - Undo/redo
   - Zoom / pan
5. **ExportWindow** — экспорт пресетов
6. **ImportWindow** — импорт пресетов
7. **GroupEditWindow** — редактор групп
8. **RolePickerWindow** / **PresetPickerWindow** — выбор ролей
9. **HelpInfoWindow** — контекстная справка
10. **UpdateWindow** — проверка обновлений

### ColorWheelControl

WPF: `UserControl` с кастомным `OnRender`.
Avalonia: `override Render(DrawingContext)` — API близок, типы из `Avalonia.Media`.

### Drag-and-drop

WPF: `DragEnter` / `DragOver` / `Drop` + `DataObject`.
Avalonia: `DragDrop.SetAllowDrop` + события `DragEnter`, `DragOver`, `Drop`.

### Анимации (ToastService)

WPF: `DoubleAnimationUsingKeyFrames`, `EasingDoubleKeyFrame`, `BackEase`.
Avalonia: `Animation` с `KeyFrame` и easing-классами — концептуально похоже, API другой.

### Темы (ThemeManager)

WPF: `Application.Resources` со словарями `Brush.Surface`, `Brush.Accent`, и т.д.
Avalonia: `Styles` в `App.axaml` с селекторами по классам:

```xml
<Style Selector="Window.LightTheme">
	<Setter Property="Background" Value="#FFFFFF"/>
</Style>
<Style Selector="Window.DarkTheme">
	<Setter Property="Background" Value="#1E1E1E"/>
</Style>
```

### Локализация

Файлы локализации переносятся с минимальными изменениями.
Можно использовать `Application.Current.Resources` или библиотеку `Avalonia.Localization`.

---

## Этап 4. Linux-сервисы

### 4.1. LinuxCursorService (замена RegistryCursorService)

Установка пресета как Xcursor-темы:

1. Конвертировать `.cur`/`.ani` в Xcursor через `XcursorWriter.Build()`
2. Записать в `~/.icons/<theme>/cursors/<alias>` для каждого алиаса роли
3. Создать `~/.icons/<theme>/index.theme`
4. Активировать тему через DE-специфичную команду

Определение desktop environment:

```csharp
static string DetectDesktopEnvironment() =>
	Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "";
// "GNOME", "KDE", "XFCE", "ubuntu:GNOME", и т.д.
```

Применение темы по DE:

| DE | Команда активации темы |
|---|---|
| GNOME | `gsettings set org.gnome.desktop.interface cursor-theme '<name>'` |
| KDE | `kwriteconfig5 --file kdeglobals --group KDE --key MouseCursorTheme '<name>'` |
| XFCE | `xfconf-query -c xsettings -p /Gtk/CursorThemeName -s '<name>'` |
| Cinnamon | `gsettings set org.cinnamon.desktop.interface cursor-theme '<name>'` |
| MATE | `gsettings set org.mate.peripherals-mouse cursor-theme '<name>'` |
| Fallback | `~/.icons/default/index.theme` + `update-alternatives` |

Размер курсора:

| DE | Команда |
|---|---|
| GNOME | `gsettings set org.gnome.desktop.interface cursor-size <size>` |
| KDE | `kwriteconfig5 --file kdeglobals --group KDE --key MouseCursorSize <size>` |
| XFCE | `xfconf-query -c xsettings -p /Gtk/CursorThemeSize -s <size>` |

Сброс к умолчанию:

```bash
gsettings reset org.gnome.desktop.interface cursor-theme
gsettings set org.gnome.desktop.interface cursor-size 24
```

### 4.2. LinuxPaths (замена AppPaths)

- `Root` → `~/.local/share/Cursor-Palette/` (или `$XDG_DATA_HOME`)
- `DownloadsDir` → `$XDG_DOWNLOAD_DIR` или `~/Downloads`
- Остальные пути (`PresetsDir`, `StateDir`, и т.д.) — без изменений, строятся от `Root`

Чтение `XDG_DOWNLOAD_DIR` из `~/.config/user-dirs.dirs`:

```csharp
static string? ReadXdgUserDir(string key)
{
	var path = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
		".config", "user-dirs.dirs");
	if (!File.Exists(path)) return null;
	foreach (var line in File.ReadAllLines(path))
	{
		if (!line.StartsWith($"XDG_{key}_DIR=")) continue;
		var value = line.Substring($"XDG_{key}_DIR=".Length).Trim('"');
		return value.StartsWith("$HOME")
			? value.Replace("$HOME",
				Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
			: value;
	}
	return null;
}
```

### 4.3. LinuxScreenColorPicker (замена NativeColorPicker)

**X11:** P/Invoke `libX11.so` — `XOpenDisplay`, `XGetImage`, извлечение пикселя.

**Wayland:** `xdg-desktop-portal` DBus API (ScreenCast) — сложнее,
на первых этапах можно ограничиться X11 или использовать fallback
(скриншот всего экрана через `grim` + чтение пикселя).

### 4.4. LinuxFileExplorer (замена ExplorerService)

```csharp
public void RevealFile(string filePath)
{
	// Попытка через nautilus (GNOME)
	Process.Start("nautilus", $"--select \"{filePath}\"");
	// Fallback: открыть папку
	// Process.Start("xdg-open", Path.GetDirectoryName(filePath));
}
```

Альтернативы по DE:
- KDE: `dolphin --select <file>`
- XFCE: `thunar <folder>`

### 4.5. LinuxSingleInstance (замена SingleInstanceService)

`Mutex` работает на Linux в .NET 8 — основная проверка.
Для активации существующего окна — Unix domain socket:

```csharp
// Первый экземпляр: слушает сокет
// Второй экземпляр: пишет в сокет → первый активируется
var socketPath = Path.Combine(Path.GetTempPath(), "cursor-palette.sock");
```

### 4.6. LinuxUpdateChecker (замена UpdateChecker)

- Формат: AppImage (один файл, self-contained) или `.tar.gz`
- Проверка версии через GitHub Releases API (без изменений)
- Скачивание в `~/Downloads/`, `chmod +x`, запуск новой версии

---

## Этап 5. Упаковка и дистрибуция

### Варианты упаковки

| Формат | Плюсы | Минусы |
|---|---|---|
| **AppImage** | Один файл, self-contained, работает везде | Нет автообновления через пакетный менеджер |
| **Flatpak** | Интеграция с магазинами приложений (Flathub) | Sandbox-ограничения, сложнее доступ к `~/.icons` |
| **.tar.gz** | Просто, минимум зависимостей | Нет интеграции с системой |
| **.deb / .rpm** | Нативная интеграция | Нужно поддерживать два формата |
| **AUR** | Популярен у Arch-пользователей | Только Arch |

Рекомендация: **AppImage** как основной формат + опционально Flatpak.

### dotnet publish для Linux

```bash
dotnet publish Cursor-Palette.Avalonia \
	-c Release \
	-r linux-x64 \
	--self-contained true \
	-o publish/linux-x64
```

### CI/CD (GitHub Actions)

```yaml
- os: ubuntu-latest
- dotnet publish -r linux-x64 --self-contained
- упаковка в AppImage через appimagetool
- загрузка артефакта в Release
```

---

## Этап 6. Тестирование

### Desktop environments для тестирования

- GNOME (Wayland) — основной, самая большая доля
- KDE Plasma (Wayland + X11)
- XFCE (X11)
- Cinnamon (X11)
- MATE (X11)

### Что проверять

- Применение курсоров (тема активируется, курсор меняется)
- Размер курсора
- Сброс к умолчанию
- Пипетка (X11 и Wayland отдельно)
- Drag-and-drop файлов и папок
- Drag-and-drop из архива
- Импорт/экспорт (.cursorpalette, ZIP, Xcursor theme)
- Paint editor (все инструменты, undo/redo, timeline)
- Локализация (все 6 языков)
- Светлая/тёмная тема
- Single instance
- Проверка обновлений
- Пути (XDG-совместимость)

---

## Этап 7. Синхронизация новых фич WPF (коммиты 10339e7, 5bc1d77, 128f22e, e3c7a8f)

### 7.1. Full export (Windows + Linux) — коммит `10339e7`

**WPF:** `PresetPackageService.ExportFullPackageForFiles` — создаёт ZIP с двумя
папками: Windows (raw .cur/.ani) и Linux (Xcursor theme). Меню загрузки
упрощено до «Quick download» + «Full export (Windows + Linux)».

**Core (не портировано):**
- Добавить `ExportFullPackageForFiles` в `PresetPackageService`
- Добавить константы `FullPackageWindowsFolderName`, `FullPackageLinuxFolderName`
- `DownloadPresetAsFolder` — добавить параметр `author`

**Linux (не портировано):**
- Обновить подменю Download в `MainWindow.xaml` — заменить 6 пунктов
  (Bundle/Raw/FullArchive/LinuxArchive/Xcursor/README) на 2:
  «Quick download» + «Full export (Windows + Linux)»
- Обновить handlers в `MainWindow.cs` — убрать старые, добавить `OnMenuDownloadQuick`
  и `OnMenuDownloadFullExport`
- Обновить локализацию в `OnContextMenuOpening` (заменить 6 лейблов на 2)
- Локализация — ключи `S.Menu.DownloadQuick`, `S.Menu.DownloadFullExport` (6 языков, JSON)
- Help — обновить `Export.md` (6 языков)

### 7.2. README.txt в Linux cursor exports — коммит `5bc1d77`

**WPF:** `WriteXcursorThemeFolder` вызывает `WriteArchiveReadme(themeDir)` после
записи `index.theme`.

**Core (не портировано):**
- `WriteXcursorThemeFolder` — добавить `WriteArchiveReadme(themeDir)` после записи `index.theme`

### 7.3. Preset authorship — коммит `128f22e`

**WPF:** Author property в `Preset`/`PresetDraft`, запись в README.txt,
marker/manifest, чтение из README при импорте. Inline-редактирование автора
в PresetEditor (display text + pencil button, Enter/click to commit).

**Core (не портировано):**
- `Preset.cs` — добавить `Author` property в `Preset` и `PresetDraft`
- `PresetPackage.cs` — добавить `Author` в `ArchiveManifestPreset` и `SinglePresetMarker`
- `PresetStore.Save` — сохранять `Author` из `draft.Author`
- `PresetPackageService`:
  - `BuildManifest` / `BuildArchive` — писать `Author` в manifest/marker
  - `WriteWindowsPresetFolder` — параметр `author`, запись в marker + README
  - `WriteArchiveReadme` — параметр `author`, подстановка `{{AuthorSection}}`
  - `BuildReadmeContent` — параметр `author`, замена `{{AuthorSection}}`
  - `TryReadAuthorFromReadme` — парсинг `Author:` из README.txt
  - Импорт — чтение author из manifest/marker с fallback на README
- `ArchiveReadme.md` — добавить `{{AuthorSection}}` плейсхолдер

**Linux (не портировано):**
- `PresetEditorWindow.cs` — inline author edit (TextBlock + pencil button,
  Enter/click to commit, TextBox для редактирования)
- `PresetEditorWindow.xaml` — разметка для author edit
- Локализация — `S.Editor.Author`, `S.Editor.Author.Tooltip`,
  `S.Editor.Author.Placeholder` (6 языков, JSON)
- Help — обновить `Editor.md` (6 языков)

### 7.4. Icon sub-sizes — коммит `e3c7a8f`

**WPF:** Multi-size cursors — несколько размеров в одном .cur/.ani файле.
`BuildMultiSizeBytes` и `TryReadAllImagesFromBytes` в `CursorCanvasService`,
multi-size overload в `AniCursorWriter.Save`, UI icon sizes в PaintEditor
(`PaintEditorWindow.IconSizes.cs`, 673 строк), `SizeChangeIcon32.png` ресурс.

**Core (не портировано):**
- `CursorCanvasService`:
  - `BuildMultiSizeBytes(IReadOnlyList<CursorCanvasImage>)` — запись multi-size .cur
  - `TryReadAllImagesFromBytes(byte[])` — чтение всех images из multi-size .cur/.ani
  - `TryReadFromBytes` — делегировать в `TryReadAllImagesFromBytes` и возвращать первый
- `AniCursorWriter`:
  - Multi-size overload `Save(..., iconSizes, iconSizeCustomImages,
    iconSizeScaleModeOverrides, iconSizesScaleMode)` — каждый кадр записывается
    как multi-size icon через `BuildMultiSizeBytes`

**Linux (не портировано):**
- `PaintEditorWindow.IconSizes.cs` — UI для управления sub-sizes:
  - Список размеров (add/remove, default 32)
  - Custom image per size (опционально)
  - ScaleMode override per size (опционально)
  - Превью каждого размера
  - Чтение существующих размеров из .ani при открытии
- `PaintEditorWindow.xaml` — разметка для icon sizes panel
- `PaintEditorWindow.Actions.cs` — передача icon sizes в `AniCursorWriter.Save`
- `PaintEditorWindow.Canvas.cs` — синхронное масштабирование всех timeline frames
- `PaintEditorWindow.History.cs` — undo/redo для icon size changes
- `PaintEditorWindow.Tools.cs` — инструмент изменения размера
- `PresetEditorWindow.SlotActions.cs` — передача icon size info при save
- `SizeChangeIcon32.png` — ресурс
- Локализация — ключи для icon sizes UI (6 языков, JSON)
- Help — обновить `Paint.md` (6 языков)

### Порядок портирования

1. **7.2** (README в Xcursor exports) — 1 строка в Core, тривиально
2. **7.3** (Preset authorship) — Core модели + сервисы, затем Linux UI
3. **7.1** (Full export) — Core метод + Linux menu update
4. **7.4** (Icon sub-sizes) — самый объёмный, Core + Linux UI (673+ строк)

---

## Оценка объёма работ

| Этап | Сложность | Объём |
|---|---|---|
| 1. Разделение Core | Средняя | Рефакторинг существующего кода |
| 2. Каркас Avalonia | Низкая | Новый проект + DI |
| 3. Перенос UI | Высокая | ~10 экранов, самый сложный — PaintEditor |
| 4. Linux-сервисы | Средняя | CursorService готов (XcursorWriter), остальное — новое |
| 5. Упаковка | Низкая | AppImage + CI |
| 6. Тестирование | Средняя | 5 DE × ~15 сценариев |
| 7. Синхронизация фич | Высокая | 4 фичи, самая сложная — icon sub-sizes |

Самая объёмная часть — **перенос UI на Avalonia** (Этап 3),
особенно PaintEditor с timeline, color wheel и пиксельным canvas.
Этап 7 добавляет icon sub-sizes в PaintEditor — второй по сложности блок.
