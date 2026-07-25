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

## Оценка объёма работ

| Этап | Сложность | Объём |
|---|---|---|
| 1. Разделение Core | Средняя | Рефакторинг существующего кода |
| 2. Каркас Avalonia | Низкая | Новый проект + DI |
| 3. Перенос UI | Высокая | ~10 экранов, самый сложный — PaintEditor |
| 4. Linux-сервисы | Средняя | CursorService готов (XcursorWriter), остальное — новое |
| 5. Упаковка | Низкая | AppImage + CI |
| 6. Тестирование | Средняя | 5 DE × ~15 сценариев |

Самая объёмная часть — **перенос UI на Avalonia** (Этап 3),
особенно PaintEditor с timeline, color wheel и пиксельным canvas.
