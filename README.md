# Cursor Palette

A lightweight Windows app for applying mouse cursor presets visually, in a
single click — no digging through system settings by hand.

Built with a focus on **simplicity, minimalism, and ease of use**: a single
screen, no unnecessary settings — just a gallery of presets and instant
apply.

![Main screen](docs/screenshots/main-screen.png)

## Features

- **Preset gallery** — every saved preset is shown as a tile with a preview,
  name, number of filled roles, and cursor size.
- **One-click apply** — clicking a tile instantly changes the cursor
  system-wide via the Windows registry and `SystemParametersInfo`.
- **Create and edit presets** — drag and drop `.cur`/`.ani` files onto the
  slot for the role you want (Arrow, Wait, IBeam, Hand, etc. — 17 system
  cursor roles in total), or pick files manually.

  ![Preset editor](docs/screenshots/preset-editor.png)

- **Drag-and-drop from the desktop** — drop files or a folder of cursors
  straight onto the main window to create a new preset right away.
- **Animated `.ani` preview** — animated cursors (busy, working, etc.) play
  their full frame sequence with original timing in the gallery and preset
  editor, instead of a static first frame.
- **Cursor size per preset** — a slider controls the system cursor size
  (32–256 px), stored separately for each preset and for the default tile.
  The "Apply" button lights up whenever the slider no longer matches what's
  actually applied, as a reminder to confirm the change.
- **Reset to Windows defaults** — a dedicated "Default" tile restores the
  system cursor scheme.
- **Undo last change** ("Back") — the app keeps a snapshot of the previous
  cursor state and lets you roll back one step.
- **Light/dark theme** — a toggle in the header switches themes instantly;
  on first launch it follows the Windows system theme, then remembers your
  choice.
- **Language switcher** — Russian, English, Simplified Chinese, Japanese,
  Spanish, and German out of the box, picked from a menu in the header; also
  follows the system language on first launch. Adding a new language only
  requires a new resource file, no code changes.
- **Interface zoom and grid size** — independent controls to scale the whole
  UI (footer, VS Code-style) and the size of gallery tiles (50–350%),
  each remembered between launches.

## How it works

Cursor Palette reads and writes cursor values directly in the
`HKCU\Control Panel\Cursors` registry key, then applies them via
`SystemParametersInfo(SPI_SETCURSORS)` — the same mechanism used by the
standard Windows Control Panel. Presets are stored locally in
`%LocalAppData%\Cursor-Palette\presets\`, each in its own folder with the
cursor files and a `manifest.json`.

## Installation

1. Download `Cursor-Palette.exe` from the [Releases](../../releases) page.
2. Run the file — the app is self-contained, no .NET installation required.

## Requirements

- Windows 10/11 (x64)

## Tech stack

.NET 8, WPF (C#)

## License

[MIT](LICENSE)
