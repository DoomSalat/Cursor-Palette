# 📤 Export presets

## 🖼️ Tiles — click a preset to select/deselect it (blue border = selected).
- "Select all" / "Select none" — buttons at the top.

🎨 Colored group tiles — clicking a group tile selects/deselects all its members. A group is included in the export only when all its presets are selected.

📝 File name — optional; type a name for the exported file, or leave it empty to use the default name.

## 🗂️ Bundle vs. ZIP archive — the two export buttons produce different files:
- Bundle (.cursorpalette) — full-fidelity: every role, locked roles and cursor size are preserved, and roles borrowed from other presets are copied in so the file is self-contained. Groups are also saved and restored on import. Meant to be re-imported into this app later with everything intact.
- ZIP archive — one folder per preset with the raw .cur/.ani files, for use outside this app or with other tools. Locked roles, custom cursor size and groups aren't stored.

{img:DownloadIcon32} Both are saved to the Downloads folder, and File Explorer opens with the new file selected.