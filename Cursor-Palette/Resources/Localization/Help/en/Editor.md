# ✏️ Preset editor

## 🎯 Each slot — one cursor role (arrow, text, busy, etc.).
- Drag a .cur/.ani file onto a slot — fills that role.
- 🎯 on a slot — set pivot point (click position).
- 🖌 on a slot — edit sprite position and canvas size (Paint editor).
- ✕ on a slot — remove file, reverts to system cursor.
- {img:LockIcon26} on a slot — lock role (protect from folder import and drag-and-drop).
- {img:DownloadIcon32} on a slot — download cursor to Downloads folder (named by preset + role).
- 🧩 on a slot — borrow a role from another preset.
- {img:LinkIcon32} — role is borrowed from another preset (link).
- "auto" badge — cursor is auto-filled.

## 📁 "Choose folder" — import all cursors from a folder at once.
- Files are matched to roles by file name.

📝 Enter a preset name at the bottom.
{img:DownloadIcon32} Left of the name — download the whole preset as a folder to Downloads.
📏 Size slider — set cursor size for this preset.
{img:ExpandIcon32} Scaling checkbox — when enabled, cursors are scaled to the preset size using nearest-neighbor interpolation, keeping pixels sharp and crisp instead of blurry. Enabled by default for new presets.
💾 "Save" — save preset to gallery (size and scaling are saved together).