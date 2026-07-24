# 📥 Import presets

This window opens after picking a Cursor Palette bundle (.cursorpalette) or ZIP archive — either via the Import button or by dropping the file onto the main window. Dropping a folder works the same way; either one is recognized whether it's still zipped or already extracted, including a Linux export (an Xcursor theme or a plain folder of raw cursor files) — an Xcursor theme is converted back into regular .cur/.ani cursors automatically.

## 🖼️ Tiles — one per preset found in the file; click to select/deselect (blue border = selected).
- "Select all" / "Select none" — buttons at the top.

☑️ "Uniform cursor size" — check to reveal a size slider; every imported preset then gets that one uniform cursor size instead of whatever size is stored in the file. Uncheck to restore each preset's own size from the file.

📦 "Import" — adds the selected presets to your gallery as new presets (with new IDs, so they never collide with existing ones).

🎨 Colored group tiles — selecting one auto-selects all its presets; deselecting a member auto-deselects the group. Only import a group's own tile to recreate the group itself.

ℹ️ Both bundles and ZIP archives restore everything (roles, locked roles, cursor size, groups). ZIP archives also include a README.txt with install instructions and human-readable folders with raw cursor files.