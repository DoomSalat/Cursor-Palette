# 📤 Export presets

## 🖼️ Tiles — click a preset to select/deselect it (blue border = selected).
- "Select all" / "Select none" — buttons at the top.

🎨 Colored group tiles — clicking a group tile selects/deselects all its members. A group is included in the export only when all its presets are selected.

📝 File name — optional; type a name for the exported file, or leave it empty to use the default name.

## 🗂️ Bundle vs. ZIP archive — the two export buttons produce different files:
- Bundle (.cursorpalette) — full-fidelity: every role, locked roles, cursor size, scaling flag and scale mode (Smooth/Nearest) are preserved, and roles borrowed from other presets are copied in so the file is self-contained. Groups are also saved and restored on import. Meant to be re-imported into this app later with everything intact.
- ZIP archive — one folder per preset with the raw .cur/.ani files, plus a cursor-palette.json manifest with full metadata (roles, locked roles, cursor size, scaling flag, scale mode, groups) and a README.txt with install instructions. Can be re-imported into this app just like a bundle, or used outside it with other tools.

🐧 Linux — the small "▾" next to the ZIP archive button offers two more formats: an Xcursor theme (index.theme + a cursors folder, ready to drop into ~/.icons) or the same raw .cur/.ani files with no extra metadata. Both can be dragged back into this app later, zipped or already extracted — the preset editor's own download button has the same "▾" for exporting just the preset you're currently editing.

📄 That same "▾" also has a "Download README" entry, which saves just the README.txt on its own (app link and install instructions) without exporting any preset.

{img:DownloadIcon32} All of these are saved to the Downloads folder, and File Explorer opens with the new file selected.