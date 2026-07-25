# Debug Log: Cursor Scaling & Preview Distortion

## Problem
After applying a size via the quick menu or switching presets, the cursor preview in the gallery appears distorted. Re-selecting the preset fixes the distortion. This means the first application doesn't correctly set up the system state.

## Attempts

### Attempt 1: Added RegistryCursorService.Refresh() after SetBaseSize
- **Files:** MainWindow.Size.cs, MainWindow.PresetActions.cs
- **Change:** Added `Refresh()` call after `SetBaseSize` in all apply methods
- **Result:** ❌ Did not fix. User still needs to re-select preset.

### Attempt 2: Added CursorPreviewService.ClearCache() before ReloadGallery
- **Files:** CursorPreviewService.cs, MainWindow.Size.cs, MainWindow.PresetActions.cs
- **Change:** Added `ClearCache()` method and called it before `ReloadGallery()` in all apply methods
- **Result:** ❌ Did not fix. Previews still distorted after apply.

### Attempt 3: Fixed premultiplied alpha in fallback paths
- **Files:** CursorCanvasService.cs (TryReadViaWin32), CursorPreviewService.cs (GetPreview), AniCursorReader.cs (LoadCursorAsFrozenBitmap)
- **Change:** Un-premultiply alpha in TryReadViaWin32, convert Bgra32→Pbgra32 in HIcon fallbacks
- **Result:** ❌ Did not fix. Black outline / distortion persists.

### Attempt 4: Changed TryReadAsBitmap to use Pbgra32
- **Files:** CursorCanvasService.cs (TryReadAsBitmap)
- **Change:** Re-premultiply alpha data and use `Pbgra32` instead of `Bgra32` for WPF rendering
- **Result:** ❌ Did not fix.

### Attempt 5: Fixed DecodeFrame in AniCursorReader
- **Files:** AniCursorReader.cs (DecodeFrame)
- **Change:** Same Pbgra32 + re-premultiply fix as TryReadAsBitmap
- **Result:** ❌ Did not fix.

### Attempt 6: Fixed Paint editor import paths
- **Files:** PaintEditorWindow.Import.cs, PaintEditorWindow.BgRef.cs, ImportImageDialog.xaml.cs
- **Change:** Same Pbgra32 + re-premultiply fix for all bitmap creation from TryRead
- **Result:** ❌ Did not fix.

### Attempt 7: Cache key includes file size + last write time
- **Files:** CursorPreviewService.cs
- **Change:** `BuildCacheKey` uses `path|size|lastWriteTime` instead of just path
- **Result:** ❌ Did not fix. Same-name files still conflict.

### Attempt 8: Replaced LoadCursorFromFile with LoadImage (natural size)
- **Files:** CursorCanvasService.cs, AniCursorReader.cs, CursorPreviewService.cs
- **Change:** Use `LoadImage` with `cxDesired=0, cyDesired=0` instead of `LoadCursorFromFile` to load cursors at original size, not system cursor size
- **Result:** ❌ Did not fix. (Fallback paths likely not reached for 32-bit .cur files)

### Attempt 9: Removed intermediate Refresh() from reset step
- **Files:** MainWindow.Size.cs, MainWindow.PresetActions.cs
- **Change:** Replaced `ApplyValues(defaults)` (which includes Refresh) with `WriteValuesWithoutRefresh(defaults)` to avoid Windows caching default cursors before scaled cursors are written
- **Result:** ✅ Part of final solution (prevents premature `SpiSetCursors` before final values are written)

### Attempt 10: Removed SpiSetCursorSize from SetBaseSize
- **Files:** RegistryCursorService.cs
- **Change:** Removed `SystemParametersInfo(SpiSetCursorSize, ...)` call from `SetBaseSize`. Only `SpiSetCursors` remains.
- **Result:** ❌ Cursor size stopped being applied entirely. `SpiSetCursorSize` is required.

### Attempt 11: Removed redundant Refresh() after SetBaseSize
- **Files:** MainWindow.Size.cs, MainWindow.PresetActions.cs
- **Change:** Removed `RegistryCursorService.Refresh()` calls after `SetBaseSize` in all apply methods. `SetBaseSize` already calls `SpiSetCursors` internally.
- **Result:** ✅ Part of final solution (eliminates double refresh)

### Attempt 12: Fixed double premultiplication in cursor reading/writing — **ROOT CAUSE FIX**
- **Files:** CursorCanvasService.cs, CursorScalerService.cs
- **Change:** Removed un-premultiply from `TryReadFromBytes` (raw .cur bytes are already straight/non-premultiplied alpha — un-premultiplying was wrong). Removed premultiply from `WriteToStream` (.cur files should contain straight alpha). Internal representation is now consistently straight alpha. Only `TryReadViaWin32` un-premultiplies (from HIcon premultiplied), and only preview rendering premultiplies (for Pbgra32/WPF). Bumped `ScaleAlgorithmVersion` to v8 to invalidate old cached scaled files.
- **Result:** ✅ **Fixed black outline distortion**

### Attempt 13: Removed redundant reset-to-defaults and cleanup steps
- **Files:** MainWindow.Size.cs, MainWindow.PresetActions.cs
- **Change:** Removed `WriteValuesWithoutRefresh(GetWindowsDefaultValues())` and `CursorScalerService.Cleanup()` from all apply methods.
- **Result:** ❌ Removing reset-to-defaults broke the workaround. Restored both steps. They are needed to clear Windows cursor handle cache.

### Attempt 14: Added extra Refresh() after SetBaseSize when scaling
- **Files:** MainWindow.Size.cs
- **Change:** Added `RegistryCursorService.Refresh()` (extra `SpiSetCursors`) after `SetBaseSize(cleanSize)` in `ApplyAndPersistSize` when scaling is enabled.
- **Result:** ❌ Did not fix. Extra SpiSetCursors doesn't help.

### Attempt 15: Swapped SPI order — SpiSetCursors before SpiSetCursorSize
- **Files:** RegistryCursorService.cs
- **Change:** Swapped order in `SetBaseSize`: `SpiSetCursors` first (load new scaled files from registry), then `SpiSetCursorSize` (set system size — no-op since files are already at target size). Attempt 11 tried this order but had the alpha bug; now that alpha is fixed (Attempt 12), this order should work.
- **Result:** ❌ Did not fix. Still need to re-select preset to clear distortion.

### Attempt 16: Delayed re-apply after 1 second
- **Files:** MainWindow.Size.cs
- **Change:** After initial apply, wait 1 second then re-apply the same scaled values + `SetBaseSize`.
- **Result:** ❌ Did not fix. Issue is not timing-related.

### Key discovery: ApplyPreset returns early when same preset is active
- `ApplyPreset(preset)` with `preset.Id == _activePresetId` returns immediately (`!force && preset.Id == _activePresetId` → `return`).
- This means "re-selecting the preset" doesn't actually re-apply anything — the distortion clears on its own over time, likely as Windows processes `WM_SETTINGCHANGE` and reloads cursors.

### Attempt 17: SpiSetCursorSize without SpifSendChange
- **Files:** RegistryCursorService.cs
- **Change:** `SpiSetCursorSize` called with `SpifUpdateIniFile` only (no `SpifSendChange`). `SpiSetCursors` still called with both flags.
- **Result:** ❌ Did not fix distortion. But system became faster (one less WM_SETTINGCHANGE broadcast). Kept this optimization.

### Key discovery: User selects DIFFERENT preset then back to clear distortion
- When switching presets, Windows loads completely different cursor handles (preset B), then loads preset A's handles fresh.
- When only changing size, Windows keeps old cursor handles loaded. `SpiSetCursorSize` scales the OLD handles in-place, causing distortion. `SpiSetCursors` may not fully reload if it considers the handles still valid.

### Attempt 18: Added Refresh() after reset-to-defaults to clear old cursor handles
- **Files:** MainWindow.Size.cs, MainWindow.PresetActions.cs
- **Change:** Added `RegistryCursorService.Refresh()` (SpiSetCursors) after `WriteValuesWithoutRefresh(GetWindowsDefaultValues())` and before `Cleanup()`/`ScaleValues`.
- **Result:** ❌ Did not fix. Removed the extra Refresh().

### Attempt 19: SetSystemCursor API to directly replace cursor handles
- **Files:** RegistryCursorService.cs, MainWindow.Size.cs
- **Change:** Added `SetSystemCursor` + `LoadCursorFromFile` P/Invoke and `ApplyCursorsDirect()`. After `SetBaseSize`, called `ApplyCursorsDirect(valuesToApply)`.
- **Result:** ❌ Cursor became huge. `LoadCursorFromFile` loads at 32px (system default), then `SetSystemCursor` sets 32px handle, then `SpiSetCursorSize` scales to 64px. Also `SpiSetCursors` in `SetBaseSize` reloads at 32px, double-scaling.

### Attempt 19b: Replaced LoadCursorFromFile with LoadImage (LR_LOADFROMFILE, no LR_DEFAULTSIZE)
- **Files:** RegistryCursorService.cs
- **Change:** Use `LoadImage(IntPtr.Zero, path, ImageCursor, 0, 0, LR_LOADFROMFILE)` to load at native file size (64px), not system default (32px).
- **Result:** ❌ Cursor became huge. `SetSystemCursor` sets 64px handle directly, but `SpiSetCursorSize(64)` in `SetBaseSize` scales it again → 128px.

### Attempt 20: SetBaseSizeDirect — ApplyCursorsDirect before SpiSetCursorSize, no SpiSetCursors
- **Files:** RegistryCursorService.cs, MainWindow.Size.cs
- **Change:** New `SetBaseSizeDirect(size, values)` method: 1) `ApplyCursorsDirect(values)` — `SetSystemCursor` replaces handles with correctly-sized files (64px), 2) `SpiSetCursorSize(64)` — scales current handles (already 64px) to 64px = no-op. **No `SpiSetCursors`** — which would reload files at 32px (SM_CXCURSOR) and cause double-scaling distortion.
- **Result:** ⏳ Pending verification

## Internet research findings

### How Windows loads cursors
1. **`SPI_SETCURSORS` (0x0057)**: Reloads system cursors from registry. Reads paths from `HKCU\Control Panel\Cursors` and loads via `LoadImage`. The size is determined by `CursorBaseSize` registry value (not `SM_CXCURSOR`).
2. **`SPI_SETCURSORSIZE` (0x2029)**: Sets system cursor size. Writes `CursorBaseSize` and `CursorSize` to registry, broadcasts `WM_SETTINGCHANGE` with `wParam=0x2029`.
3. **`LoadImage` with `LR_DEFAULTSIZE`**: Uses `SM_CXCURSOR` system metric (always 32, the driver default — does NOT change with `CursorBaseSize`).
4. **`LoadImage` without `LR_DEFAULTSIZE`**: Uses actual resource size from file.
5. **`LoadCursorFromFile`**: Loads at system cursor size (like `LR_DEFAULTSIZE`).
6. **Multi-size .cur files**: Can contain multiple sizes (32, 48, 128...). Windows picks the appropriate one based on `CursorBaseSize`/DPI.
7. **`SM_CXCURSOR`**: Always returns the DEFAULT cursor size (32x32), NOT the current `CursorBaseSize`. (Source: The Old New Thing blog by Raymond Chen)
8. **Qt implementation**: Reads `CursorBaseSize` directly from registry, not `SM_CXCURSOR`, to determine actual cursor size.

## Root cause
The black outline distortion was caused by **double premultiplication** of alpha channels:
1. `TryReadFromBytes` was un-premultiplying alpha on raw `.cur` file bytes that were already straight (non-premultiplied) alpha — corrupting the pixel data
2. `WriteToStream` was premultiplying alpha when writing `.cur` files — but `.cur` files should contain straight alpha

This caused semi-transparent pixels to have incorrect RGB values, appearing as black outlines around cursor edges.

## Final solution
1. **Alpha handling**: Internal representation is consistently straight (non-premultiplied) alpha. Only `TryReadViaWin32` un-premultiplies (HIcon → straight), and only preview rendering premultiplies (straight → Pbgra32 for WPF).
2. **SPI call order**: `SpiSetCursorSize` → `SpiSetCursors` (set size first, then reload cursors at new size).
3. **No redundant refresh**: `WriteValuesWithoutRefresh` used instead of `ApplyValues` to avoid premature `SpiSetCursors`. `SetBaseSize` already calls `SpiSetCursors` internally, so no extra `Refresh()` needed.
4. **No reset-to-defaults workaround**: Removed intermediate `WriteValuesWithoutRefresh(GetWindowsDefaultValues())` and `CursorScalerService.Cleanup()` — they were workarounds that are no longer needed.

---

## Session 2 (2026-07-25): SpiSetCursors ERROR_INVALID_HANDLE

### Problem
`SpiSetCursors` consistently returns `False (GetLastError=6)` (`ERROR_INVALID_HANDLE`). Cursor size changes don't apply immediately — user must re-select preset. Distortion persists.

### Attempt 21: Animated cursor scaling fix (KEPT)
- **Files:** CursorScalerService.cs (ScaleAniFile)
- **Change:** Added temp-file fallback when `TryReadFromBytes` fails for ANI frames. Writes raw frame bytes to temp `.cur` file, reads via `TryRead` (which has Win32 `LoadImage` fallback).
- **Result:** ✅ Animated cursors now scale correctly (32x32 → 112x112 confirmed in logs).

### Attempt 22: Moved SetBaseSize to UI thread
- **Files:** MainWindow.Size.cs, MainWindow.PresetActions.cs
- **Change:** Moved `SetBaseSize` call outside `Task.Run` to execute on UI thread (some Win32 APIs require UI thread).
- **Result:** ❌ `SpiSetCursors` still returns `False (GetLastError=6)`.

### Attempt 23: ApplyCursorsDirect fallback when SpiSetCursors fails
- **Files:** RegistryCursorService.cs, MainWindow.Size.cs, MainWindow.PresetActions.cs
- **Change:** Added optional `values` param to `SetBaseSize`. If `SpiSetCursors` fails, calls `ApplyCursorsDirect(values)` which uses `LoadImage` + `SetSystemCursor` per cursor.
- **Result:** ❌ Made things "significantly worse" per user.

### Attempt 24: ApplyCursorsDirect + SpiSetCursorSize + SpiSetCursors
- **Files:** RegistryCursorService.cs
- **Change:** New order: 1) `ApplyCursorsDirect` (set correct handles), 2) `SpiSetCursorSize` (no-op since already at target), 3) `SpiSetCursors` (notify).
- **Result:** ❌ `SpiSetCursorSize` overwrote `ApplyCursorsDirect` handles by scaling 32px system cursors to target — same distortion.

### Attempt 25: ApplyCursorsDirect only, no SpiSetCursorSize
- **Files:** RegistryCursorService.cs
- **Change:** Removed `SpiSetCursorSize`. Only `ApplyCursorsDirect` + `SpiSetCursors`.
- **Result:** ❌ Nothing worked at all. Even preset re-selection stopped helping.

### Attempt 26: Preset re-selection approach
- **Files:** MainWindow.Size.cs, MainWindow.PresetActions.cs, MainWindow.Gallery.cs
- **Change:** On size change: update preset's BaseSize, switch to a different preset, then switch back with `force: true`. Changed `ApplyPreset` from `async void` to `async Task`.
- **Result:** ❌ Broke `ApplyPreset` — preset re-selection stopped working entirely. Reverted.

### All changes reverted except Attempt 21 (animated cursor scaling).

### Key findings
- `SpiSetCursors` = `0x0057`, `SpiSetCursorSize` = `0x2029`
- `GetLastError=6` = `ERROR_INVALID_HANDLE` — happens on both UI thread and background thread
- `SpiSetCursorSize` always returns `True`
- `SpiSetCursors` always returns `False` regardless of thread context
- Manual preset re-selection works (distortion clears), but `ApplyAndPersistSize` doesn't
- `ApplyPreset` skips when `preset.Id == _activePresetId` (no force) — so "re-selecting" actually selects a DIFFERENT preset first, then back
- The difference between `ApplyAndPersistSize` and `ApplyPreset` paths is the key to the root cause
