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

— (всё портировано)
