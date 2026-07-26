# WPF vs Linux Port — Detailed Comparison

This document provides a systematic file-by-file comparison of the WPF (Cursor-Palette) and Linux/Avalonia (Cursor-Palette.Linux) versions of Cursor Palette. Discrepancies and confirmations of identical behavior are noted for each component.

---

## 1. MainWindow

### 1.1 Architecture

| Aspect | WPF | Linux |
|---|---|---|
| Framework | WPF (.NET 8) | Avalonia UI |
| File structure | Partial classes across 9 files: `MainWindow.xaml.cs`, `.Gallery.cs`, `.DragDrop.cs`, `.Settings.cs`, `.Size.cs`, `.Groups.cs`, `.ImportExport.cs`, `.PresetActions.cs`, `.Updates.cs` | Single monolithic `MainWindow.cs` (1857 lines) + `MainWindow.xaml` |
| UI layout | XAML (`MainWindow.xaml`) | XAML (`MainWindow.xaml`) |
| ViewModel | Code-behind directly manipulates UI | `MainWindowViewModel` (MVVM pattern, 870 lines) |
| Gallery data | Direct manipulation of `ItemsControl` with `Border` cells | `ObservableCollection<BoardItem>` bound to `ItemsControl` |

### 1.2 MainWindow Initialization

**WPF** (`MainWindow.xaml.cs:161-199`):
- Uses `InitializeComponent()` (WPF auto-generated)
- Reads window size from `AppState`
- Sets up slider, UI scale, cell scale, theme, language, open-folder toggle
- Calls `BuildGroupColorSwatches()`, `ReloadGallery()`, `UpdateUndoButton()`
- Calls `CheckForUpdatesAsync(version)`
- `OnSourceInitialized` → `SingleInstanceService.ListenForActivation`
- `OnClosed` → persists window size

**Linux** (`MainWindow.cs:1-200`):
- Uses `AvaloniaXamlLoader.Load` or `InitializeComponent`
- Reads window size from `AppState`
- Sets `DataContext` to `MainWindowViewModel`
- Uses `FindControl<T>` to access UI elements (Avalonia pattern)
- Calls `_viewModel.Initialize()`, `ApplyUiScale`, `ApplyCellScale`
- Calls `CheckForUpdatesAsync(version)`
- No `OnSourceInitialized` equivalent; `OnClosed` persists window size

**Discrepancies:**
- **WPF** uses `SingleInstanceService.ListenForActivation` — Linux uses `LinuxSingleInstance` (different implementation).
- **WPF** directly accesses named XAML controls (e.g., `SizeSlider`, `FooterRun`). **Linux** uses `FindControl<T>()` lookups stored in private fields.
- **WPF** has `RelayUiCommand` class defined in `MainWindow.xaml.cs` — not present in Linux.

### 1.3 Gallery (MainWindow.Gallery.cs vs MainWindow.cs)

**WPF** (`MainWindow.Gallery.cs`, 773 lines):
- `ReloadGallery()` — loads presets/groups, reconciles board order, creates cells directly as `Border` elements
- `CreateDefaultCell()`, `CreatePresetCell()`, `CreateGroupCell()`, `CreateAddCell()` — all return `Border` with inline UI
- `UpdateActiveCellHighlight()` — highlights active preset cell
- Context menus built dynamically with `MenuItem` objects
- "Download System Cursors" submenu has two options: PNG+GIF and CUR+ANI
- Cell constants: `CellSize=148`, `CellPreviewSize=48`, `CellNameFontSize=13`

**Linux** (`MainWindowViewModel.cs` + `MainWindow.cs`):
- `ReloadGallery()` in ViewModel — creates `BoardItem` objects (data, not UI)
- `CreateDefaultCell()`, `CreatePresetCell()`, `CreateGroupCell()`, `CreateAddCell()` — return `BoardItem` records
- UI rendering done via XAML `DataTemplate` in `MainWindow.xaml`
- Context menu items defined in XAML, wired to event handlers
- "Download System Cursors" submenu has three options: Xcursor, PNG+GIF, CUR+ANI

**Discrepancies:**
- **Linux adds `OnMenuDownloadSystemXcursor`** — downloads current system cursors as `.xcursor` files. No WPF equivalent (WPF doesn't have xcursor format).
- **Cell size**: WPF `CellSize=148`, Linux XAML `Width="148" Height="148"` — **confirmed identical**.
- **WPF builds context menus dynamically** in C# code. **Linux** defines them in XAML with event handlers.
- **WPF** has `UpdateActiveCellHighlight()` that directly modifies cell borders. **Linux** uses `BoardItem.IsActive` property and XAML binding.
- **WPF** has `StartInlineGroupRename` — inline rename of groups in gallery. **Linux** does not appear to have this feature.
- **WPF** has `MixedBadgeText = "🧩"` and `MixedBadgeFontSize = 15` on preset cells. **Linux** has `IsMixed` property on `BoardItem` — badge rendering via XAML.

### 1.4 Drag and Drop (MainWindow.DragDrop.cs vs MainWindow.cs)

**WPF** (`MainWindow.DragDrop.cs`, 476 lines):
- Uses `MouseLeftButtonDown`, `MouseMove`, `DragEnter`, `DragOver`, `Drop` events
- `PresetDragFormat = "CursorPalette.PresetId"`, `GroupDragFormat = "CursorPalette.GroupId"`
- `BeginDragGhost()` — creates a ghost `Border` that follows cursor
- `UpdateReorderIndicator()` — shows insertion line
- `ReorderBoardItem()` — reorders in board order list
- `OnWindowDragOver`/`OnWindowDrop` — handles file drops from OS
- `HandleDroppedPaths()` — detects packages, archives, cursor files
- `GroupAttachZoneMargin = 0.25`

**Linux** (`MainWindow.cs:566-974`):
- Uses `PointerPressed`, `PointerMoved`, `PointerReleased` events (Avalonia pattern)
- Same drag format strings
- `BeginDragGhost()`, `EndDragGhost()`, `UpdateDragGhostPosition()` — same concept
- `UpdateReorderIndicator()` — same logic with `TranslateTransform`
- `FindGroupHover()`, `SetGroupAttachIndicator()` — group attach zone
- `GroupAttachZoneMargin = 0.2` (different from WPF's `0.25`)

**Discrepancies:**
- **GroupAttachZoneMargin**: WPF = `0.25`, Linux = `0.2` — minor difference in group attach sensitivity.
- **WPF** uses `MouseLeftButtonDown`/`MouseMove`. **Linux** uses `PointerPressed`/`PointerMoved`/`PointerReleased` — different event model.
- **WPF** uses `DragDrop.DoDragDrop` (synchronous). **Linux** uses `await DragDrop.DoDragDrop` (async).
- **WPF** has `_justDraggedPreset`/`_justDraggedGroup` flags to prevent click-after-drag. **Linux** clears `_presetDragStartPoint` instead — functionally equivalent.
- **WPF** `HandleDroppedPaths` is in `MainWindow.DragDrop.cs`. **Linux** delegates to `_viewModel.HandleDroppedPathsAsync`.

### 1.5 Settings (MainWindow.Settings.cs vs MainWindow.cs)

**WPF** (`MainWindow.Settings.cs`, 113 lines):
- `ApplyUiScale()` — applies `ScaleTransform` to `LayoutTransform`
- `AdjustUiZoom()` — increments/decrements UI zoom
- `OnCellScaleSliderValueChanged()` — adjusts cell scale
- `OnThemeToggleClick()` — toggles theme, calls `ReplaceWindowToApplyNewTheme()`
- `ReplaceWindowToApplyNewTheme()` — **recreates the entire MainWindow** to apply theme
- `SwitchLanguage()` — changes language, calls `UpdateLanguageButtonText()` + `ReloadGallery()` (does NOT recreate window)

**Linux** (`MainWindow.cs:1717-1774`):
- `ApplyUiScale()` — applies `ScaleTransform` to `LayoutTransform`
- `AdjustUiZoom()` — same
- `OnThemeToggle()` — toggles `RequestedThemeVariant` (Avalonia built-in)
- `SwitchLanguage()` — changes language, calls `ApplyLocalization()` and `_viewModel.ReloadGallery()`

**Discrepancies:**
- **WPF** recreates the entire window for **theme changes only** (`ReplaceWindowToApplyNewTheme` in `OnThemeToggleClick`). Language changes do NOT recreate the window — they just update text and reload gallery. **Linux** applies both theme and language inline without window recreation — **Linux approach is better for theme changes**.
- **WPF** uses `ThemeIconDark = "🌙"` and `ThemeIconLight = "☀"`. **Linux** uses same constants.

### 1.6 Size (MainWindow.Size.cs vs MainWindow.cs)

**WPF** (`MainWindow.Size.cs`, 203 lines):
- `OnSizeSliderValueChanged()` — updates size text
- `UpdateApplySizeButtonHighlight()` — highlights apply button when size differs from baseline
- `OnApplySizeButtonClick()` — calls `ApplyAndPersistSize()`
- `ApplyAndPersistSize()` — uses `RegistryCursorService.ApplyValuesAndBaseSize`
- `InitScaleCursorsCheckBox()` — initializes checkbox from `AppState`
- `OnScaleModeIconClick()` — toggles scale mode

**Linux** (`MainWindow.cs:500-564`):
- `UpdateSizeText()` — updates size text
- `OnApplySizeClick()` — calls `ApplySizeInternal()`
- `ApplySizeInternal()` — calls `_viewModel.ApplySizeAsync(sizePx, useScaling, _activeScaleMode)`
- `OnScaleCursorsClick()` — sets `AppState.SetScaleCursorsEnabled`
- `OnScaleModeIconClick()` — toggles scale mode via ViewModel
- `SetScaleCursorsCheckbox()` — public method for external setting

**Discrepancies:**
- **WPF** uses `RegistryCursorService.ApplyValuesAndBaseSize()`. **Linux** uses `_viewModel.ApplySizeAsync()` which calls `CursorServiceProvider.Current.ApplyValues()` + `SetBaseSize()` separately.
- **WPF** has `UpdateApplySizeButtonHighlight()` — highlights apply button. **Linux** does not appear to have this visual feedback.
- **WPF** uses `RegistryCursorService.SizeStep` for slider step. **Linux** uses `CursorConstants.SizeStep`.
- **WPF** `ApplyAndPersistSize` calls `RegistryCursorService.ApplyValuesAndBaseSize` (combined). **Linux** calls `cursorService.ApplyValues` then `cursorService.SetBaseSize` (separate).

### 1.7 Groups (MainWindow.Groups.cs vs MainWindow.cs)

**WPF** (`MainWindow.Groups.cs`, 291 lines):
- `BuildGroupColorSwatches()` — creates color swatch `Border` elements
- `EditGroup()` — opens `GroupEditWindow`
- `DeleteGroup()` — deletes group with confirmation
- `CreateEmptyGroup()` — creates group via `GroupEditWindow`
- `ClearGroupSelection()` — clears selection state
- `ToggleSelection()` — toggles preset selection for grouping
- `UpdateGroupToolbar()` — shows/hides group toolbar
- `OnGroupCreateClick()` — creates group from selected presets
- `OnGroupCancelClick()` — cancels group selection
- `OnGalleryRightClick()` — right-click context menu
- `StartInlineGroupRename()` — inline rename of groups

**Linux** (`MainWindow.cs:1401-1517`):
- `BuildGroupColorSwatches()` — same concept, creates swatch `Border` elements
- `ClearGroupSelection()` — same
- `ToggleSelection()` — same, also calls `_viewModel.SetSelectedPresetIds`
- `UpdateGroupToolbar()` — same
- `OnGroupCreateClick()` — calls `_viewModel.CreateGroupFromSelection`
- `OnGroupCancelClick()` — calls `_viewModel.SetSelectedPresetIds(null)`
- `OnMenuCreateGroup()` — opens `GroupEditWindow` dialog
- `OnMenuEditGroup()` — opens `GroupEditWindow` for editing
- `OnMenuConsolidateGroup()` — consolidates group
- `OnMenuUngroup()` — ungroups
- `OnMenuRandomPreset()` — applies random preset from group

**Discrepancies:**
- **WPF** has `StartInlineGroupRename()` — inline rename of groups in gallery. **Linux** does not have this feature.
- **WPF** `DeleteGroup()` shows a MessageBox confirmation. **Linux** `OnMenuDelete` for groups calls `_viewModel.DeleteGroup()` directly without confirmation — **potential behavioral difference**.
- **WPF** `EditGroup()` uses `GroupEditWindow` with `ShowDialog()`. **Linux** uses `await dialog.ShowDialog<bool?>(this)` — different return pattern.
- **WPF** `OnGalleryRightClick` builds context menus dynamically. **Linux** uses XAML-defined context menus with `OnContextMenuOpening` handler.

### 1.8 Import/Export (MainWindow.ImportExport.cs vs MainWindow.cs)

**WPF** (`MainWindow.ImportExport.cs`, 66 lines):
- `OnExportButtonClick()` — opens `ExportWindow` with presets and groups
- `OnImportButtonClick()` — opens `OpenFileDialog`, detects package, opens `ImportPickerWindow` for selection
- `ImportPackage()` — calls `PresetPackageService.ImportSelected`

**Linux** (`MainWindow.cs:1782-1823`):
- `OnExportClick()` — opens `ExportWindow` with presets, groups, and toast host
- `OnImportClick()` — opens file picker via `StorageProvider`, detects package, calls `_viewModel.ImportAllFromPackage`

**Discrepancies:**
- **WPF** uses `ImportPickerWindow` to let user select which presets/groups to import, with options for `IgnoreIndividualSizes` and `UniformSize`. **Linux** imports all entries without user selection — **missing feature**.
- **WPF** `ExportWindow` constructor takes `(presets, groups)`. **Linux** takes `(presets, groups, toastHost)` — needs toast host because Avalonia windows don't share resources.
- **WPF** import supports `.cursorpalette`, `.zip`. **Linux** supports `.cursorpalette`, `.zip`, `.tar.gz` — **Linux has additional format support**.
- **WPF** `ImportPackage` shows toast with count of imported presets. **Linux** shows generic "Saved" toast — **less informative**.
- **WPF** catches `PackageVersionUnsupportedException` and shows detailed error with version numbers. **Linux** silently returns — **no user feedback on version errors**.

### 1.9 Preset Actions (MainWindow.PresetActions.cs vs MainWindow.cs)

**WPF** (`MainWindow.PresetActions.cs`, 514 lines):
- `ApplyPreset()` — applies preset with loading overlay, error handling via MessageBox
- `ApplyDefault()` — applies Windows default cursors
- `ApplyRandomFromGroup()` / `ApplyRandomFromBoard()` — random preset selection
- `OnUndoButtonClick()` — undo last cursor change
- `DeletePreset()` — with MessageBox confirmation
- `MovePreset()` — reorders in board
- `DownloadPreset()` — downloads preset as folder
- `StartInlineRename()` — inline rename of presets
- `DownloadSystemCursors()` — downloads system cursors as images or cursor files
- `EditPreset()` / `OpenEditor()` — opens PresetEditorWindow
- `ShowLoadingOverlay()` / `HideLoadingOverlay()` — uses Storyboard for spinner animation

**Linux** (`MainWindow.cs` various sections):
- `ApplyPresetFromClick()` — calls `_viewModel.ApplyPresetAsync()`
- `ApplyDefault()` — calls `_viewModel.ApplyDefaultAsync()`
- `OnAppLogoClick()` — calls `_viewModel.ApplyRandomFromBoardAsync()`
- `OnMenuRandomPreset()` — calls `_viewModel.ApplyRandomFromGroupAsync()`
- `OnUndoClick()` — calls `_viewModel.UndoAsync()`
- `OnMenuDelete()` — with dialog confirmation (custom Window, not MessageBox)
- `OnMenuMoveLeft()` / `OnMenuMoveRight()` — calls `_viewModel.MovePreset()`
- `OnMenuDownload()` — downloads preset as `.cursorpalette` bundle
- `OnMenuRename()` — shows custom rename dialog (not inline)
- `OnMenuDownloadSystemXcursor()` / `OnMenuDownloadSystemImages()` / `OnMenuDownloadSystemCurAni()` — three download options
- `ShowLoadingOverlay()` / `HideLoadingOverlay()` — dynamically creates overlay `Border` with `DispatcherTimer`-based rotating `Ellipse` spinner (16ms, 6°/tick), adds/removes from `RootGrid`

**Discrepancies:**
- **WPF** has `StartInlineRename()` — inline rename in gallery. **Linux** uses a modal dialog (`OnMenuRename`) — **different UX**.
- **WPF** `DownloadPreset()` downloads as folder. **Linux** `OnMenuDownload()` downloads as `.cursorpalette` bundle — **different format**.
- **WPF** `DownloadSystemCursors(asImages)` has two modes (images/cursor files). **Linux** has three modes (xcursor/images/cur+ani) — **Linux has additional xcursor mode**.
- **WPF** error handling uses `MessageBox.Show()`. **Linux** uses empty `catch` blocks — **missing error feedback**.
- **WPF** `ApplyPreset` calls `UpdateActiveCellHighlight()`, `UpdateScaleIcon()`, `UpdateUndoButton()`, `UpdateApplySizeButtonHighlight()` after apply. **Linux** `ApplyPresetFromClick` calls `UpdateScaleIcon()` but not the others — **missing UI updates**.
- **WPF** `OnUndoButtonClick` has full undo logic with snapshot loading. **Linux** `OnUndoClick` delegates to `_viewModel.UndoAsync()`.
- **WPF** `ShowLoadingOverlay` uses WPF Storyboard for spinner. **Linux** uses `DispatcherTimer` (16ms, 6°/tick) with manual `RotateTransform` — **different animation approach, both animated**.
- **WPF** `DownloadSystemCursors(asImages)` converts `.ani` to `.gif` via `AnimatedGifWriter.Save()` and `.cur` to `.png` via `PngBitmapEncoder`. **Linux** `OnMenuDownloadSystemImages` uses `XcursorWriter.LoadFrames()` + `SaveFrameAsPng()`/`SaveFramesAsGif()` — **different image export implementation**.
- **WPF** `DownloadPreset` passes `preset.LockedRoles` to `PresetPackageService.DownloadPresetAsFolder`. **Linux** `OnMenuDownload` uses `PresetPackageService.ExportBundle()` which does not pass `LockedRoles` — **missing locked roles in download**.
- **WPF** `OpenEditor` calls `CursorPreviewService.Invalidate()` for all role files after save. **Linux** does not — **stale preview cache after edit**.
- **WPF** `ApplyPreset` uses `RegistryCursorService.SaveSnapshotToDisk`/`TakeSnapshot` for undo. **Linux** delegates to `_viewModel.ApplyPresetAsync()` — **different undo snapshot mechanism**.

### 1.10 Updates (MainWindow.Updates.cs vs MainWindow.cs)

**WPF** (`MainWindow.Updates.cs`, 96 lines):
- `CheckForUpdatesAsync()` — uses Storyboard for spinner, shows `UpdateIndicator` or `UpToDateLabel`
- `OnUpdateIndicatorClick()` — opens `UpdateWindow`
- `OnUpToDateLabelClick()` — re-checks for updates
- `OnFooterClick()` — opens `AboutWindow`
- `OnGitHubIconClick()` — opens GitHub URL
- `OnInfoButtonClick()` — opens `InfoHelpWindow`
- `OnAppLogoClick()` — applies random preset
- `OnOpenFolderToggleClick()` — toggles open-folder setting
- `UpdateOpenFolderToggleIcon()` — changes brush key

**Linux** (`MainWindow.cs:200-499` + `1832-1855`):
- `CheckForUpdatesAsync()` — similar, uses `UpdateSpinner` visibility
- `OnUpdateIndicatorClick()` — opens `UpdateWindow`
- `OnUpToDateLabelClick()` — re-checks
- `OnFooterClick()` — opens `AboutWindow`
- `OnGitHubLinkClick()` — opens GitHub URL
- `OnInfoClick()` — opens `InfoHelpWindow`
- `OnAppLogoClick()` — applies random preset
- `OnOpenFolderToggleClick()` — toggles setting
- `UpdateOpenFolderToggleIcon()` — changes `Opacity` (0.4–1.0)

**Discrepancies:**
- **WPF** `UpdateOpenFolderToggleIcon` changes brush color (accent/textDim). **Linux** changes `Opacity` (1.0/0.4) — **different visual approach**.
- **WPF** uses WPF `Storyboard` for update spinner animation. **Linux** uses `DispatcherTimer` (16ms intervals) with manual `RotateTransform` angle increment — **different animation approach**.
- **WPF** update toast uses `Loc.Get(LocToastUpdateAvailable)` (no version). **Linux** uses `Loc.Format(LocToastUpdateAvailable, version)` — **Linux includes version number in toast**.
- **WPF** version comparison is inline (`Version.TryParse` + `>`). **Linux** delegates to `UpdateChecker.IsUpdateAvailableAsync()` — **different version comparison approach**.
- **WPF** `CheckForUpdatesAsync` takes `currentVersion` as parameter. **Linux** reads version internally from assembly — **different parameter passing**.

---

## 2. PresetEditorWindow

### 2.1 Architecture

| Aspect | WPF | Linux |
|---|---|---|
| File structure | XAML + 7 partials: `.xaml.cs`, `.DragDrop.cs`, `.Import.cs`, `.Save.cs`, `.Size.cs`, `.SlotActions.cs`, `.SlotState.cs`, `.SlotUI.cs` | Single `PresetEditorWindow.cs` (786 lines) |
| UI layout | XAML (`PresetEditorWindow.xaml`) | Code-only (no XAML, builds UI in constructor) |
| Slot class | `Slot` with 14 properties | `Slot` with 10 properties |

### 2.2 Constructor & Initialization

**WPF** (`PresetEditorWindow.xaml.cs:126-210`):
- `InitializeComponent()` (XAML)
- Gets base size from `RegistryCursorService.GetBaseSize()`
- Default `_useScaling = existing?.UseScaling ?? true`
- Gets system defaults from `RegistryCursorService.GetWindowsDefaultValues()`
- Uses `PlaceholderCursorDefaults.GetPath()` as fallback
- Processes dropped files with `ImageToCursorService.ConvertToCursorTempFile()`
- Checks `ImageToCursorService.IsFullyTransparent()` — skips empty cursors
- Shows `MessageBox` for empty skipped files

**Linux** (`PresetEditorWindow.cs:87-303`):
- Builds entire UI in constructor (no XAML)
- Gets base size from `AppState.GetDefaultBaseSize()`
- Default `_useScaling = existing?.UseScaling ?? false`
- No system defaults lookup (no Windows registry)
- Processes dropped files directly (local `ConvertToCursorTempFile` method, not a separate service)
- No transparency check for dropped files

**Discrepancies:**
- **Default `_useScaling`**: WPF = `true`, Linux = `false` — **different default scaling behavior**.
- **WPF** uses `ImageToCursorService` (453 lines) to convert image files to cursor temp files, including animated GIF decoding with disposal methods. **Linux** has `ConvertToCursorTempFile` in `PresetEditorWindow.cs` + `GifDecoderService.cs` — handles static images and animated GIF-to-`.ani` conversion with disposal methods and alpha compositing.
- **WPF** checks for fully transparent cursors and skips them with a warning. **Linux** does not — **missing validation**.
- **WPF** uses `PlaceholderCursorDefaults.GetPath()` for missing defaults. **Linux** has no equivalent.
- **WPF** `SlotHeight = 204`. **Linux** `SlotHeight = 180` — **different slot dimensions**.
- **WPF** slot has `PivotButton`, `PositionButton`, `LockButton`, `LockIcon`, `DownloadButton`, `PrimaryButtons`, `DropIndicator`, `PlaceholderBadge`, `HotspotDot`, `LinkBadge`. **Linux** slot has `BrowseButton`, `PaintButton`, `PickExistingButton`, `ClearButton`, `LinkBadge` — **simplified slot UI**.
- **WPF** has `PivotButtonContent = "🎯"`, `PositionButtonContent = "🖌"` — pivot and position (hotspot editor) buttons. **Linux** has `PaintButton` instead — **different editing approach**.
- **WPF** `OpenPaintEditor` supports animated cursors (`.ani`) — reads frames via `AniCursorReader.Read()` with `ReadAniAsFrames()` (up to 60 frames), opens PaintEditorWindow with frames+delays, saves result via `AniCursorWriter.Save()`. **Linux** `OpenPaintEditor` only handles static cursors via `CursorCanvasService.TryRead()` — **no animated cursor support in paint editor**.
- **WPF** `BrowseForSlot` checks `IsFullyTransparent` and warns user. **Linux** `BrowseForSlot` does not — **missing validation**.
- **WPF** `DownloadSlot` copies cursor file to Downloads with unique naming, shows toast, reveals in Explorer. **Linux** has no per-slot download — **missing feature**.
- **WPF** `OpenHotspotEditor` opens standalone `HotspotEditorWindow` with live preview. **Linux** has no hotspot editor button in PresetEditorWindow (hotspot editing is in PaintEditorWindow only).

### 2.3 Slot UI

**WPF** (`PresetEditorWindow.SlotUI.cs`, 337 lines):
- Rich slot with preview image (in `Canvas` host with hotspot dot overlay), role name, file text
- `PlaceholderBadge` — shows "Placeholder" badge for slots using default cursors
- `LinkBadge` — `Rectangle` with `OpacityMask` icon (link icon image)
- `PivotButton` ("🎯") — opens `HotspotEditorWindow` for visual hotspot editing
- `PositionButton` ("🖌") — opens `PaintEditorWindow` for full paint editing
- `PickExistingButton` ("🧩") — picks from existing presets
- `LockButton` — lock icon image button, prevents slot overwrite
- `DownloadButton` — download icon, bottom-right corner, copies cursor to Downloads
- `ClearButton` — "✕" danger-style button, top-right corner
- `DropIndicator` — dashed accent rectangle for drag-and-drop onto slots
- `DropZoneService.AttachManaged` — per-slot drag-drop with `IsLocked` guard
- Slot drop converts via `ImageToCursorService`, checks `IsFullyTransparent`
- Uses resource brushes (`Brush.Accent`, `Brush.Surface`, `Brush.Border`, etc.)
- Uses style resources (`Style.AccentButton`, `Style.Button`, `Style.DangerButton`)

**Linux** (`PresetEditorWindow.cs:356-486`):
- Simpler slot with preview image, role name, file text
- `BrowseButton` — opens file picker for cursor/image files
- `PaintButton` — opens `PaintEditorWindow` (static cursors only)
- `PickExistingButton` ("🧩") — picks from existing presets
- `ClearButton` — "✕" button, top-right corner
- `LinkBadge` — `TextBlock` with "🔗" emoji (not image icon like WPF)
- No lock button, no hotspot dot, no placeholder badge, no drop indicator, no download button
- Uses hardcoded `Brushes.DarkGray`, `Brushes.Gray` — no theme support
- No `DropZoneService` — per-slot drag-drop not supported (window-level drop zone only)

**Discrepancies:**
- **WPF** has lock functionality (`IsLocked`, `LockedRoles`). **Linux** does not — **missing feature**.
- **WPF** has per-slot download button. **Linux** does not — **missing feature**.
- **WPF** has drag-and-drop onto individual slots. **Linux** has a drop zone for the entire window — **different DnD approach**.
- **WPF** has hotspot dot and placeholder badge visualizations. **Linux** does not — **missing visual feedback**.
- **Linux** has `PaintButton` to open PaintEditorWindow. **WPF** has `PositionButton` (hotspot editor) — **different editing tools**.

### 2.4 Save (PresetEditorWindow.Save.cs vs PresetEditorWindow.cs)

**WPF** (`PresetEditorWindow.Save.cs`, 138 lines):
- `OnSaveButtonClick()` — validates slots, creates `PresetDraft` with `LockedRoles`, sets `DialogResult = true`
- `OnDownloadPresetClick()` — downloads preset as folder via `PresetPackageService.DownloadPresetAsFolder`, shows toast, reveals in Explorer
- `OnDownloadPresetMoreClick()` — context menu with Xcursor Theme, Linux Archive, Download Readme options
- `DownloadReadme()` — downloads readme file, shows toast, reveals in Explorer
- `ExportPresetForLinux()` — exports as Xcursor theme or Linux archive

**Linux** (`PresetEditorWindow.cs:629-657`):
- `OnSaveClick()` — validates slots, creates `PresetDraft` (no `LockedRoles`), calls `Close()`
- No download preset button
- No download readme
- No export as Xcursor/Linux archive from editor

**Discrepancies:**
- **WPF** saves `LockedRoles` in draft. **Linux** does not — **missing locked roles persistence**.
- **WPF** has download preset as folder. **Linux** does not — **missing feature**.
- **WPF** has "Download More" menu (Xcursor, Linux Archive, Readme). **Linux** does not — **missing features**.
- **WPF** `OnSaveButtonClick` shows `MessageBox` if no files selected. **Linux** `OnSaveClick` silently returns — **no user feedback**.

### 2.5 Import (PresetEditorWindow.Import.cs vs PresetEditorWindow.cs)

**WPF** (`PresetEditorWindow.Import.cs`, 83 lines):
- `OnBrowseFolderClick()` — opens `OpenFolderDialog`
- `ImportFolder()` — supports `recursive` parameter, uses `ImageToCursorService.IsConvertibleFile`, checks `IsLocked`, checks `IsFullyTransparent`, shows `MessageBox` for empty skipped / no match / no cursors

**Linux** (`PresetEditorWindow.cs:668-740`):
- `BrowseFolder()` — opens `FolderPickerOpenOptions`
- `ImportFolder()` — `SearchOption.TopDirectoryOnly` only, uses `ConvertibleExtensions` set, no `IsLocked` check, no `IsFullyTransparent` check, no user feedback for empty/no match

**Discrepancies:**
- **WPF** supports recursive folder import. **Linux** does not — **missing feature**.
- **WPF** checks `IsLocked` before importing into slot. **Linux** does not — **missing lock guard**.
- **WPF** checks `IsFullyTransparent` and skips with count. **Linux** does not — **missing validation**.
- **WPF** shows `MessageBox` for "no cursors found", "empty skipped", "no match". **Linux** silently returns — **no user feedback**.

### 2.6 Drag-and-Drop (PresetEditorWindow.DragDrop.cs vs PresetEditorWindow.cs)

**WPF** (`PresetEditorWindow.DragDrop.cs`, 179 lines):
- `DropZoneService.StartLeaveWatchdog` + `HandleWindowDragLeave` — watchdog timer for unreliable WPF DragLeave
- `OnPresetWindowDragEnter` — distinguishes folder vs single file, shows `FolderDropIndicator` or per-slot `DropIndicator`
- `SetSlotIndicatorsVisibility` — respects `IsLocked` (hides indicator for locked slots)
- `OnFolderDrop` — deferred `HandleDroppedFolderPaths` via `Dispatcher.BeginInvoke`
- `HandleDroppedFolderPaths` — supports folders + archives (`.zip`, `.rar`, `.7z` via `ArchiveImportService`)
- `TryImportXcursorTheme` — detects Xcursor theme folders, reconstructs roles, checks `IsFullyTransparent`, shows warnings
- Per-slot drop via `DropZoneService.AttachManaged` in `SlotUI.cs` — converts via `ImageToCursorService`, checks `IsFullyTransparent`

**Linux** (`PresetEditorWindow.cs:683-705`):
- `OnDropZoneDragOver` — simple file check
- `OnDropZoneDrop` — drops file or folder, calls `ImportFolder`
- No per-slot drag-drop indicators
- No archive extraction support
- No Xcursor theme detection
- No `IsLocked` guard
- No `IsFullyTransparent` check

**Discrepancies:**
- **WPF** has per-slot drag-drop with visual indicators. **Linux** has window-level drop zone only — **missing per-slot DnD**.
- **WPF** supports archive extraction in drag-drop. **Linux** does not — **missing archive support**.
- **WPF** detects and imports Xcursor themes via drag-drop. **Linux** does not — **missing Xcursor import**.
- **WPF** uses `DropZoneService` watchdog for reliable DragLeave. **Linux** does not need it (Avalonia events reliable) — **platform-appropriate difference**.
- **WPF** distinguishes folder vs single file drop with different indicators. **Linux** does not — **simpler UX**.

### 2.7 Size (PresetEditorWindow.Size.cs vs PresetEditorWindow.cs)

**WPF** (`PresetEditorWindow.Size.cs`, 48 lines):
- `OnEditorSizeSliderValueChanged()` — updates `_baseSize` using `RegistryCursorService.SizeStep`, guarded by `_sizeSliderReady`
- `OnEditorUseScalingCheckedChanged()` — updates `_useScaling` from checkbox
- `OnEditorScaleModeIconClick()` — toggles `ScaleMode` between `NearestNeighbor` and `AreaWeighted`
- `UpdateEditorScaleIcon()` — swaps icon between `StairIconUri` and `ExpandIconUri` (image resources)
- No `OnApplySizeClick` in this file — defined in XAML code-behind (`PresetEditorWindow.xaml.cs`)

**Linux** (`PresetEditorWindow.cs:109-135, 193-219, 659-666`):
- Size slider `PropertyChanged` handler — updates `_baseSize` using `CursorConstants.SizeStep`
- `useScalingCheckBox.IsCheckedChanged` — updates `_useScaling`
- `scaleModeButton.PointerPressed` — toggles `ScaleMode`, updates emoji text ("📐"/"📏")
- `OnApplySizeClick` — calls `mainWindow.ApplyPresetSize(_baseSize)` + `mainWindow.SetScaleCursorsCheckbox(_useScaling)`

**Discrepancies:**
- **WPF** uses `RegistryCursorService.SizeStep`. **Linux** uses `CursorConstants.SizeStep` — **different service for constants**.
- **WPF** scale mode icon uses image resources (`StairIconUri`, `ExpandIconUri`). **Linux** uses emoji ("📐"/"📏") — **different visual representation**.
- **WPF** has `_sizeSliderReady` guard. **Linux** does not — **potential initialization race** (though Avalonia's property change timing may differ).
- **Linux** has `OnApplySizeClick` delegating to parent `MainWindow`. **WPF** apply size is not in `Size.cs` — **different organization**.

### 2.8 Slot State (PresetEditorWindow.SlotState.cs vs PresetEditorWindow.cs)

**WPF** (`PresetEditorWindow.SlotState.cs`, 134 lines):
- `SetSlotSource()` — sets path, applies preview via `CursorPreviewService.ApplyPreview`, hides `PlaceholderBadge`/`LinkBadge`, shows `ClearButton`/`PivotButton`/`DownloadButton`, conditionally shows `PositionButton` based on `CursorCanvasService.IsSupportedFile`, calls `UpdateHotspotDot`
- `SetSlotReference()` — sets ref, applies preview, shows `LinkBadge` with tooltip, disables `PivotButton`/`PositionButton` with disabled tooltips
- `SetSlotPlaceholder()` — applies default preview at `PlaceholderOpacity`, shows `PlaceholderBadge` if default path exists, hides all action buttons except `PositionButton`
- `SetSlotLocked()` — toggles `IsLocked`, updates lock button visual (border color/thickness, icon fill), disables `PrimaryButtons` and `ClearButton`
- `GetSlotResolvedPath()` — resolves source path or ref path
- `UpdateHotspotDot()` — reads hotspot via `CursorHotspotService.Read`, positions dot on preview canvas
- `BuildReferenceLabel()` — resolves preset name + filename

**Linux** (`PresetEditorWindow.cs:543-627`):
- `SetSlotSource()` — sets path, applies preview via `CursorPreviewService.GetPreview`, shows `ClearButton`, hides `LinkBadge`. No `PlaceholderBadge`, no `PivotButton`, no `DownloadButton`, no `PositionButton`, no `UpdateHotspotDot`
- `SetSlotReference()` — sets ref, applies preview (in try/catch), shows `LinkBadge` with tooltip, shows `ClearButton`. No disabled button states
- `ClearSlot()` — clears state, sets empty text, hides `ClearButton`/`LinkBadge`, clears preview image. No `PlaceholderBadge`, no default preview at `PlaceholderOpacity`
- `BuildReferenceLabel()` — identical logic
- No `SetSlotLocked()` — no lock functionality
- No `SetSlotPlaceholder()` — `ClearSlot` is simpler (clears preview entirely vs WPF showing default at reduced opacity)
- No `UpdateHotspotDot()` — no hotspot visualization
- No `GetSlotResolvedPath()` helper

**Discrepancies:**
- **WPF** `SetSlotSource` shows `PivotButton`, `DownloadButton`, conditionally `PositionButton`, and calls `UpdateHotspotDot`. **Linux** does none of these — **missing visual feedback and actions**.
- **WPF** `SetSlotReference` disables `PivotButton`/`PositionButton` with specific disabled tooltips. **Linux** has no equivalent buttons — **N/A**.
- **WPF** `SetSlotPlaceholder` shows default cursor at `PlaceholderOpacity` with `PlaceholderBadge`. **Linux** `ClearSlot` clears preview entirely — **different placeholder behavior**.
- **WPF** has `SetSlotLocked` with visual feedback. **Linux** has no lock — **missing feature**.
- **WPF** has `UpdateHotspotDot` showing hotspot position on preview. **Linux** does not — **missing visual feedback**.
- **WPF** preview uses `CursorPreviewService.ApplyPreview` (handles caching + animated preview). **Linux** uses `CursorPreviewService.GetPreview` in try/catch (static only) — **different API, error handling differs**.
- **WPF** `SetSlotSource` sets `FileText.Foreground = Brush(BrushText)`. **Linux** sets `Brushes.White` — **hardcoded color vs theme brush**.

---

## 3. PaintEditorWindow

### 3.1 Architecture

| Aspect | WPF | Linux |
|---|---|---|
| File structure | XAML + 15 partials: `.xaml.cs`, `.Actions.cs`, `.BgRef.cs`, `.Canvas.cs`, `.Eyedropper.cs`, `.GifImport.cs`, `.History.cs`, `.Hotspot.cs`, `.Import.cs`, `.Paint.cs`, `.Render.cs`, `.Resize.cs`, `.Sprite.cs`, `.Timeline.cs`, `.Tools.cs`, `.Zoom.cs` | `PaintEditorWindow.cs` + 10 partials: `.BgRef.cs`, `.Canvas.cs`, `.Eyedropper.cs`, `.History.cs`, `.Hotspot.cs`, `.Import.cs`, `.Keyboard.cs`, `.Paint.cs`, `.Timeline.cs`, `.Tools.cs` |
| Missing Linux partials | — | `.Actions.cs`, `.GifImport.cs`, `.Render.cs`, `.Resize.cs`, `.Sprite.cs`, `.Zoom.cs` |
| Missing Linux features | — | Animated GIF import, Canvas resize via drag handles, CanvasSizeDialog, ImportImageDialog |
| Reorganized (not missing) | — | Zoom (in main `.cs`), Sprite move/snap (in `.Tools.cs`), Render (in `.Canvas.cs`) |

**Discrepancies:**
- **Animated GIF import** — WPF `PaintEditorWindow.GifImport.cs` has `TryImportAnimatedGif()` which decodes multi-frame GIFs into timeline frames with proper disposal methods (2=restore-to-bg, 3=restore-to-previous), per-frame delays, and alpha compositing. Linux can import `.gif` files but only as **single-frame static images** via `WriteableBitmap.Decode` — **animated GIF import is missing**.
- **Canvas resize via drag handles** — WPF `PaintEditorWindow.Resize.cs` has 8 working drag thumb handlers (left, right, top, bottom, 4 corners) with pan shifting, shadow rect, and live size label. Linux has `StartResizeDrag()` and `UpdateResizeDrag()` in `PaintEditorWindow.Timeline.cs` but **`UpdateResizeDrag()` is an empty stub** — canvas resize via dragging is **not functional**.
- **CanvasSizeDialog** — WPF has `CanvasSizeDialog.xaml.cs` (164 lines) — a proper dialog with width/height text input, preset sizes (16×16 through 256×256), 9-point anchor selection grid, and live preview with old/new rect overlay. Linux `OnCanvasSizeClick` just **clamps current dimensions without any dialog** — no user input, no anchor selection, no preview.
- **ImportImageDialog** — WPF has `ImportImageDialog.xaml.cs` (221 lines) — a dialog with image preview, drag-and-drop support, "Over" vs "Replace" import mode selection, cursor file (.cur/.ani) loading as bitmaps, and scaling mode auto-selection. Linux just opens a file picker and directly applies the image — **no import mode selection, no preview, no drag-and-drop onto dialog**.
- **Zoom** — WPF has separate `.Zoom.cs` partial. Linux has zoom **fully implemented** in main `PaintEditorWindow.cs` — `ZoomAtPoint()`, `OnCanvasZoomIn()`, `OnCanvasZoomOut()`, ctrl+scroll zoom, `_zoomTransform`. **Not missing, just organized differently.**
- **Sprite move/snap** — WPF has separate `.Sprite.cs` partial. Linux has sprite move/snap **fully implemented** in `PaintEditorWindow.Tools.cs` — `OnMoveLeftClick`, `OnMoveRightClick`, `OnMoveUpClick`, `OnMoveDownClick`, `OnSnapClick`, `SnapOffset`, `ParseFraction`, 9-point snap grid. **Not missing, just organized differently.**
- **Render** — WPF has separate `.Render.cs` partial. Linux handles rendering in `.Canvas.cs`. **Not missing, just organized differently.**
- **HotspotEditorWindow** — WPF has a standalone `HotspotEditorWindow.xaml.cs` (170 lines) with visual hotspot editing (drag marker, 9-point preset positions, live preview, coords display, window size persistence). Linux does **not** have a separate window but integrates hotspot editing into `PaintEditorWindow` (`_hotspotMarker`, `_hotspotMarkerGlow`, `_isDraggingHotspot`, `PaintEditorWindow.Hotspot.cs`). **Different architecture, same functionality.**
- **Linux** has `.Keyboard.cs` — **additional keyboard handling** not in WPF.
- **DropZoneService** — WPF has `DropZoneService.cs` for drag-leave watchdog timer (handles WPF's unreliable DragLeave events). Linux does not have this — Avalonia's drag-drop events are more reliable. **Platform-appropriate difference.**

---

## 4. ExportWindow

### 4.1 Architecture

| Aspect | WPF | Linux |
|---|---|---|
| UI layout | XAML (`ExportWindow.xaml`) | Code-only (builds UI in constructor) |
| Groups support | Yes — creates group tiles | No — only preset tiles |
| File structure | XAML + code-behind | Single `.cs` file |

**WPF** (`ExportWindow.xaml.cs`, 399 lines):
- Creates group tiles with color-coded backgrounds
- `SyncGroupTileSelections()` — syncs group tile selection with member presets
- Export options: Bundle, Archive, Linux Archive, Xcursor Theme, Download Readme
- Error handling with `MessageBox`
- `RevealInExplorer()` after export if setting enabled
- Uses resource brushes (`Brush.Accent`, `Brush.Border`, etc.)

**Linux** (`ExportWindow.cs`, 383 lines):
- No group tiles — only preset tiles
- Export options: Bundle, Archive, Linux Archive, Xcursor Theme
- No "Download Readme" option
- Error handling with empty `catch` blocks
- No `RevealInExplorer` after export
- Uses hardcoded `Brushes.CornflowerBlue` / `Brushes.Gray`

**Discrepancies:**
- **WPF** has group tile support in export. **Linux** does not — **missing feature**.
- **WPF** has "Download Readme" option. **Linux** does not — **missing feature**.
- **WPF** has `SyncGroupTileSelections()`. **Linux** does not — **missing feature**.
- **WPF** uses resource brushes for theming. **Linux** uses hardcoded colors — **no theme support**.
- **WPF** has error handling with `MessageBox`. **Linux** uses empty `catch` — **missing error feedback**.
- **WPF** calls `RevealInExplorer` after export. **Linux** does not — **missing convenience feature**.
- **WPF** has `EmptyHint` visibility based on preset count. **Linux** does not show empty hint.
- **WPF** has `ExportNameBox` from XAML. **Linux** creates `_nameBox` in code — functionally equivalent.
- **WPF** export buttons: `ExportBundleButton`, `ExportArchiveButton`, `ExportArchiveMoreButton`. **Linux**: `bundleButton`, `archiveButton`, `moreButton` — same structure.
- **WPF** scaling icon uses image resources (`ExpandIconUri`, `StairIconUri`). **Linux** uses emoji `"📐"` — **different visual representation**.

---

## 5. UpdateWindow

### 5.1 Architecture

| Aspect | WPF | Linux |
|---|---|---|
| UI layout | XAML | Code-only |
| Update method | Auto-update (BAT file) + Manual download | Manual download only |
| Download format | `.exe` | `.tar.gz` |

**WPF** (`UpdateWindow.xaml.cs`, 149 lines):
- Two buttons: "Manual Download" and "Auto Update"
- `OnAutoUpdateClick()` — downloads `.exe`, creates `.bat` script to replace running executable, restarts app
- `OnManualDownloadClick()` — downloads `.exe` to Downloads folder
- `DownloadManualAsync()` — uses `HttpClient`, shows toast, reveals in Explorer
- `GetDownloadsFolder()` — falls back to Desktop

**Linux** (`UpdateWindow.cs`, 233 lines):
- One button: "Download" (manual only)
- `OnDownloadClick()` — downloads `.tar.gz` to Downloads folder
- `DownloadAsync()` — uses `HttpClient`, shows status text and toast
- `GetDownloadsFolder()` — falls back to home directory (not Desktop)

**Discrepancies:**
- **WPF** has auto-update functionality (BAT file replaces executable). **Linux** does not — **missing feature** (appropriate for Linux where package managers handle updates).
- **WPF** downloads `.exe` (`FileNameFormat = "Cursor-Palette-v{0}.exe"`). **Linux** downloads `.tar.gz` (`ArchiveFileNameFormat = "Cursor-Palette-v{0}.tar.gz"`) — **platform-appropriate difference**.
- **WPF** `GetDownloadsFolder` falls back to `Desktop`. **Linux** falls back to `UserProfile` home directory — **platform-appropriate difference**.
- **WPF** uses `ExplorerService.RevealFile()`. **Linux** does not reveal file — **missing convenience feature**.
- **Linux** has inline toast implementation in `ShowToast()` (creates Border, adds to `_toastHost`, auto-removes after 3s). **WPF** uses `ToastService.Show()`.

---

## 6. AboutWindow

### 6.1 Architecture

| Aspect | WPF | Linux |
|---|---|---|
| UI layout | XAML | Code-only |
| License display | XAML-defined | Hardcoded `LicenseText` constant |

**WPF** (`AboutWindow.xaml.cs`, 21 lines):
- `InitializeComponent()` (XAML)
- Sets `VersionText.Text`
- Info button opens `InfoHelpWindow`

**Linux** (`AboutWindow.cs`, 146 lines):
- Builds entire UI in constructor
- Full MIT license text as `LicenseText` constant
- Scrollable license text
- Info button opens `InfoHelpWindow`
- Title "Cursor Palette" with "Palette" in CornflowerBlue

**Discrepancies:**
- **WPF** uses XAML for layout. **Linux** builds UI in code — functionally equivalent but different approach.
- **Linux** has hardcoded license text as `LicenseText` constant. **WPF** has it in XAML — same content, different location.
- Both show same version format: `"{AppInfo.Author}  ·  v{version}  ·  {AppInfo.LicenseName}"`.
- Both have info button that opens `InfoHelpWindow` with `HelpTextService.Get("About")`.

---

## 7. InfoHelpWindow

### 7.1 Architecture

| Aspect | WPF | Linux |
|---|---|---|
| File structure | XAML + `.xaml.cs` + `.Rendering.cs` | Single `.cs` file (400 lines) |
| UI layout | XAML | Code-only |
| Text zoom | Yes | Yes |
| Image rendering | `ImageIconRegex` / `ImageIconInlineRegex` | Same regex patterns |

**WPF** (`InfoHelpWindow.xaml.cs`, 165 lines + `InfoHelpWindow.Rendering.cs`):
- `InitializeComponent()` (XAML)
- `LayoutTransform` with `ScaleTransform` for UI scaling
- `BuildBody()` — parses body text into paragraphs, renders as UI elements
- `RenderTitle()`, `RenderStandalone()`, `RenderSectionCard()`, `RenderTipsCard()` (in `.Rendering.cs`)
- Text zoom with `AdjustTextZoom()` and `ApplyTextZoom()`
- Uses WPF resource brushes

**Linux** (`InfoHelpWindow.cs`, 400 lines):
- Builds UI in `BuildContent()`
- Same `BuildBody()` logic — paragraph parsing identical
- `RenderTitle()`, `RenderStandalone()`, `RenderSectionCard()`, `RenderTipsCard()` — same rendering pattern
- Text zoom with same constants (`TextZoomStep = 0.1`)
- Uses `Brushes.CornflowerBlue`, `Brushes.Gray` — hardcoded

**Discrepancies:**
- **WPF** uses `LayoutTransform` for UI scaling. **Linux** does not apply UI scale — **missing UI scaling**.
- **WPF** uses resource brushes. **Linux** uses hardcoded colors — **no theme support**.
- Both have identical `BuildBody()` parsing logic — **confirmed identical**.
- Both have identical text zoom behavior — **confirmed identical**.
- Both use same regex patterns for image tokens — **confirmed identical**.

---

## 8. GroupEditWindow

### 8.1 Architecture

| Aspect | WPF | Linux |
|---|---|---|
| UI layout | XAML | Code-only |
| Result | `DialogResult = true` | `Close(true)` |

**WPF** (`GroupEditWindow.xaml.cs`, 87 lines):
- `InitializeComponent()` (XAML)
- UI scale transform
- `BuildColorSwatches()` — creates swatch borders with `ColorConverter.ConvertFromString`
- `OnSaveClick()` — sets `DialogResult = true`
- Swatch border brush uses `Brush.Text` resource
- `SwatchSize = 24`, `SwatchRingThickness = 2.5`

**Linux** (`GroupEditWindow.cs`, 142 lines):
- Builds UI in constructor
- No UI scale transform
- `BuildColorSwatches()` — creates swatch borders with `Color.Parse`
- `OnSave()` — calls `Close(true)`
- Swatch border brush uses `Brushes.White`
- Swatch size includes ring thickness (`SwatchSize + SwatchRingThickness * 2`)

**Discrepancies:**
- **WPF** applies UI scale transform. **Linux** does not — **missing UI scaling**.
- **WPF** swatch border brush = `Brush.Text` (theme-aware). **Linux** = `Brushes.White` (hardcoded) — **no theme support**.
- **WPF** swatch size = `SwatchSize` (24). **Linux** swatch size = `SwatchSize + SwatchRingThickness * 2` (29) — **different visual size**.
- **WPF** uses `MouseLeftButtonUp` for swatch selection. **Linux** uses `PointerPressed` — different event model, same behavior.
- Both have same `GroupName` and `ColorKey` properties — **confirmed identical**.
- Both default to `GroupColors.Palette.First().Key` — **confirmed identical**.

---

## 9. RolePickerWindow

### 9.1 Architecture

| Aspect | WPF | Linux |
|---|---|---|
| UI layout | XAML | Code-only |
| Result | `DialogResult = true` | `Close()` (no dialog result) |
| Preview | `CursorPreviewService.ApplyPreview(image, path)` | `CursorPreviewService.GetPreview(path)` |

**WPF** (`RolePickerWindow.xaml.cs`, 135 lines):
- `InitializeComponent()` (XAML)
- UI scale transform
- `OnlyCurrentRoleCheck` checkbox filters roles
- `CreateTile()` — uses `CursorPreviewService.ApplyPreview()` (supports animated cursors)
- Hover effects: `MouseEnter`/`MouseLeave` change background
- Click: `MouseLeftButtonUp` sets `SelectedRole` and `DialogResult = true`
- Uses resource brushes

**Linux** (`RolePickerWindow.cs`, 247 lines):
- Builds UI in constructor
- No UI scale transform
- `onlyCurrentRoleCheck` checkbox filters roles
- `CreateTile()` — uses `CursorPreviewService.GetPreview()` (static preview only)
- Hover effects: `PointerEntered`/`PointerExited` change background
- Click: `PointerReleased` sets `SelectedRole` and `Close()`
- Uses hardcoded brushes

**Discrepancies:**
- **WPF** `ApplyPreview` supports animated cursor previews (ANI files show animation). **Linux** `GetPreview` only shows static first frame — **missing animated preview**.
- **WPF** uses resource brushes (theme-aware). **Linux** uses hardcoded `Brushes.CornflowerBlue`, `Brushes.DarkGray` — **no theme support**.
- **WPF** applies UI scale transform. **Linux** does not — **missing UI scaling**.
- Both have same tile constants (`TileSize=96`, `TilePreviewSize=40`, etc.) — **confirmed identical**.
- Both filter by `OnlyCurrentRoleCheck` — **confirmed identical**.
- Both show `EmptyHint` when no roles available — **confirmed identical**.

---

## 10. ExistingPresetPickerWindow

### 10.1 Architecture

| Aspect | WPF | Linux |
|---|---|---|
| UI layout | XAML | Code-only |
| Result | `DialogResult = true` | `Close()` (no dialog result) |
| Preview | `CursorPreviewService.ApplyPreview(image, path)` | `CursorPreviewService.GetPreview(path)` |

**WPF** (`ExistingPresetPickerWindow.xaml.cs`, 125 lines):
- `InitializeComponent()` (XAML)
- UI scale transform
- `CreateCell()` — uses `CursorPreviewService.ApplyPreview()` (animated)
- Hover: `MouseEnter`/`MouseLeave` change border brush
- Click: `MouseLeftButtonUp` sets `SelectedPreset` and `DialogResult = true`
- Mixed badge (`🧩`) for presets with `RoleRefs`
- Uses resource brushes

**Linux** (`ExistingPresetPickerWindow.cs`, 214 lines):
- Builds UI in constructor
- No UI scale transform
- `CreateCell()` — uses `CursorPreviewService.GetPreview()` (static)
- Hover: `PointerEntered`/`PointerExited` change border brush
- Click: `PointerReleased` sets `SelectedPreset` and `Close()`
- Mixed badge (`🧩`) for presets with `RoleRefs`
- Uses hardcoded brushes

**Discrepancies:**
- Same as RolePickerWindow: **WPF** has animated previews, **Linux** has static only.
- **WPF** uses resource brushes. **Linux** uses hardcoded — **no theme support**.
- **WPF** applies UI scale. **Linux** does not.
- Both have same cell constants — **confirmed identical**.
- Both show mixed badge for `RoleRefs.Count > 0` — **confirmed identical**.

---

## 10.5 WPF-Only Windows (No Standalone Linux Equivalent)

These windows exist only in WPF. Some functionality is partially replicated elsewhere in Linux.

| Window | WPF File | Linux Equivalent |
|---|---|---|
| `ImportPickerWindow` | `ImportPickerWindow.xaml.cs` (125 lines) | **None** — Linux imports all entries without selection |
| `CanvasSizeDialog` | `CanvasSizeDialog.xaml.cs` (164 lines) | **None** — Linux `OnCanvasSizeClick` just clamps dimensions |
| `ImportImageDialog` | `ImportImageDialog.xaml.cs` (221 lines) | **None** — Linux opens file picker directly |
| `HotspotEditorWindow` | `HotspotEditorWindow.xaml.cs` (170 lines) | **Integrated** into `PaintEditorWindow.Hotspot.cs` (same functionality) |

---

## 11. Services Comparison

### 11.1 CursorPreviewService

**WPF** (`CursorPreviewService.cs`, 146 lines):
- Uses `LoadImage` from `user32.dll` (P/Invoke) to load cursor files
- `Imaging.CreateBitmapSourceFromHIcon` to convert to `BitmapSource`
- `ApplyPreview()` — supports animated cursors with `ObjectAnimationUsingKeyFrames`
- `GetAnimatedFrames()` — returns `AnimatedCursorFrames` with `BitmapSource` frames
- Cache: `Dictionary<string, ImageSource?>`

**Linux** (`CursorPreviewService.cs`, 144 lines):
- No P/Invoke — uses `CursorCanvasService.TryRead()` for `.cur` files
- `CreateBitmap()` — manually constructs BMP header and pixel data for Avalonia `Bitmap`
- No `ApplyPreview()` — only `GetPreview()` (static)
- `GetAnimatedFrames()` — returns `List<CursorCanvasImage>` (raw frame data, not bitmaps)
- Cache: `Dictionary<string, Bitmap?>`

**Discrepancies:**
- **WPF** has `ApplyPreview()` with animation support. **Linux** does not — **no animated cursor previews in UI**.
- **WPF** uses Windows API (`LoadImage`, `CreateBitmapSourceFromHIcon`). **Linux** uses manual BMP construction — **platform-appropriate difference**.
- **WPF** `GetAnimatedFrames` returns `AnimatedCursorFrames` (with `StepFrameIndices`, `StepDurations`). **Linux** returns `List<CursorCanvasImage>` — **different data structure**.
- Both have `Invalidate()` method — **confirmed identical**.
- Both cache previews by expanded file path — **confirmed identical**.

### 11.2 UpdateChecker

**WPF** (`UpdateChecker.cs`, 97 lines):
- `FindExeAssetUrl()` — searches for `.exe` asset in GitHub release
- Falls back to `AppInfo.GitHubReleasesUrl` if no `.exe` found

**Linux** (`UpdateChecker.cs`, 72 lines):
- No `FindExeAssetUrl()` method
- Always uses `AppInfo.GitHubReleasesUrl` as download URL

**Discrepancies:**
- **WPF** finds the `.exe` download URL from GitHub assets. **Linux** just links to releases page — **Linux doesn't auto-find download asset**.
- Both use same API URL, UserAgent, and version comparison logic — **confirmed identical**.

### 11.3 ToastService

**WPF** (`ToastService.cs`, 90 lines):
- Uses WPF animation system (`DoubleAnimationUsingKeyFrames`, `EasingDoubleKeyFrame`)
- `BackEase` with `Amplitude=0.3` for slide-in
- `CubicEase` for slide-out
- Uses resource brushes (`Brush.Surface`, `Brush.Accent`, `Brush.Text`)

**Linux** (`ToastService.cs`, 123 lines):
- Uses manual `DispatcherTimer` for animation (16ms steps)
- No easing functions — linear interpolation
- Uses hardcoded `Brushes.DimGray`, `Brushes.CornflowerBlue`, `Brushes.White`

**Discrepancies:**
- **WPF** has smooth easing animations (`BackEase`, `CubicEase`). **Linux** has linear animation — **less polished animation**.
- **WPF** uses resource brushes (theme-aware). **Linux** uses hardcoded colors — **no theme support**.
- Both have same timing constants (`FadeIn=0.25s`, `Hold=2.5s`, `FadeOut=0.25s`) — **confirmed identical**.
- Both have same `Active` dictionary pattern for single-toast-per-host — **confirmed identical**.

### 11.4 Shared Core Services (Cursor-Palette.Core)

The following services are shared between WPF and Linux via `Cursor-Palette.Core`:
- `AniCursorReader.cs`
- `AniCursorWriter.cs`
- `AppState.cs`
- `ArchiveImportService.cs`
- `BoardOrderStore.cs`
- `CursorCanvasService.cs`
- `CursorHotspotService.cs`
- `CursorScalerService.cs`
- `ExportFileNaming.cs`
- `GroupColors.cs`
- `GroupStore.cs`
- `HelpTextService.cs`
- `LocalizationManager.cs`
- `PlaceholderCursorDefaults.cs`
- `PresetPackageService.cs`
- `PresetStore.cs`
- `XcursorWriter.cs`
- `IPlatformServices.cs` — shared interfaces: `IScreenColorPicker`, `IFileExplorer`, `ISingleInstance`, `IAssetLoader`
- `ICursorService.cs` — shared interface for cursor manipulation (`GetBaseSize`, `ApplyCursor`, etc.)
- `IPlatformPaths.cs` — shared interface for platform-specific paths
- `Loc.cs` — localization string access helper

These are **confirmed identical** as they share the same source files.

### 11.5 Linux-Specific Services

- `LinuxCursorService.cs` — replaces `RegistryCursorService`, reads/writes Xcursor files
- `CursorServiceProvider.cs` — abstraction over `ICursorService`
- `LinuxAssetLoader.cs` — loads assets from filesystem (replaces WPF pack URIs)
- `LinuxFileExplorer.cs` — replaces `ExplorerService` (implements `IFileExplorer`, uses `xdg-open`) — **exists but unused in Linux codebase**
- `LinuxPaths.cs` — replaces `AppPaths`
- `LinuxScreenColorPicker.cs` — replaces `NativeColorPicker`
- `LinuxSingleInstance.cs` — replaces `SingleInstanceService`
- `PlatformBootstrapper.cs` — platform initialization

### 11.6 WPF-Only Services (No Linux Equivalent)

The following WPF services have **no Linux equivalent** at all:

- `DebugLogger.cs` — writes debug logs to `~/Downloads/cursor-debug-log.txt` with timestamps. Linux has no debug logging.
- `DropZoneService.cs` — drag-leave watchdog timer for WPF's unreliable `DragLeave` events. Linux doesn't need this (Avalonia events are reliable).
- `ImageToCursorService.cs` (453 lines) — converts image files (PNG, JPG, BMP, GIF) to `.cur` or `.ani` temp files. Handles animated GIF decoding with disposal methods, alpha compositing, frame extraction, `IsFullyTransparent` check, `IsConvertibleFile`/`IsImageFile` helpers. Linux has `ConvertToCursorTempFile` + `GifDecoderService.cs` in `PresetEditorWindow.cs` — handles static images and animated GIF-to-`.ani` conversion.
- `ThemeManager.cs` — manages dark/light theme switching with resource dictionary swapping. Linux uses Avalonia's built-in `RequestedThemeVariant`.
- `ExplorerService.cs` — reveals files in Windows Explorer via `Process.Start`. Used 9 times across 6 WPF files. Linux has `LinuxFileExplorer.cs` (35 lines, uses `xdg-open`) but it is **unused** — no Linux code calls it.
- `NativeColorPicker.cs` — Windows native color picker dialog. Linux has `LinuxScreenColorPicker.cs` as replacement.
- `SingleInstanceService.cs` — Windows single-instance via named pipe. Linux has `LinuxSingleInstance.cs` as replacement.
- `RegistryCursorService.cs` — Windows registry cursor manipulation. Linux has `LinuxCursorService.cs` as replacement.

### 11.7 Duplicate Services (Both WPF and Linux, Different Implementations)

These services exist in both projects but are **not shared via Core** — they're separate implementations:

| Service | WPF | Linux |
|---|---|---|
| `ToastService.cs` | WPF animations, resource brushes | Manual timer, hardcoded colors |
| `UpdateChecker.cs` | Finds `.exe` asset URL | Links to releases page |
| `CursorPreviewService.cs` | Windows API, animated previews | Manual BMP, animated previews via `AnimatedPreviewManager` |
| `AnimatedGifWriter.cs` | WPF (4979 bytes) | Linux (7384 bytes) — different implementation |

---

## 12. Summary of Key Discrepancies

### Missing Features in Linux Port

1. ✅ **ImportPickerWindow** — Implemented: selective import dialog with preset/group selection
2. ✅ **Inline rename** — Implemented: inline TextBox swap in gallery cell (Enter to commit, Escape to cancel, LostFocus to commit)
3. ✅ **Lock roles** — Implemented: slot lock functionality in PresetEditorWindow
4. ✅ **Per-slot download** — Implemented: download button on individual slots
5. ✅ **Hotspot dot visualization** — Implemented: visual hotspot indicator in preset editor slots
6. ✅ **Placeholder badge** — Implemented: empty slot placeholder badge
7. ✅ **Drop indicator per slot** — Implemented: per-slot drop indicators with window-level DragEnter showing all slot indicators (respecting IsLocked), DragLeave/Drop hiding them
8. ✅ **Group tiles in ExportWindow** — Implemented: group selection in export
9. ✅ **Download Readme** — Implemented: readme download option in export
10. N/A **Auto-update** — No auto-update functionality (appropriate for Linux)
11. ✅ **Animated cursor previews** — Implemented: `AnimatedPreviewManager.cs` with `DispatcherTimer` cycling ANI frames at correct durations; attached in both gallery (via `CollectionChanged` + visual tree scan) and PresetEditor (via `SetSlotSource`/`SetSlotReference`)
12. ✅ **PaintEditor: Animated GIF import** — Implemented: raw GIF decoder with LZW, disposal methods, multi-frame timeline import
13. ✅ **PaintEditor: Canvas resize via drag handles** — Implemented: 8-direction edge detection in `UpdateResizeDrag`
14. ✅ **PaintEditor: CanvasSizeDialog** — Implemented: full dialog with width/height input, presets, anchor grid, live preview
15. ✅ **PaintEditor: ImportImageDialog** — Implemented: dialog with preview, drag-drop, Over/Replace mode, cursor file loading
16. N/A **DropZoneService** — Not needed: Avalonia drag-drop events are more reliable than WPF
17. ✅ **UI scaling** — Implemented: `ScaleTransform` applied to all dialog windows
18. ✅ **Theme support** — Implemented: `ThemeManager.cs` with persisted theme choice via `AppState`, `RequestedThemeVariant` + `ThemeDictionaries` in App.xaml for Dark/Light brushes
19. ✅ **Error handling** — Implemented: catch blocks show toast feedback to user
20. ✅ **RevealInExplorer** — Implemented: `FileExplorerProvider.Current?.RevealFile` after export/download
21. ✅ **Apply size button highlight** — Implemented: `UpdateApplySizeButtonHighlight`
22. ✅ **UpdateActiveCellHighlight after apply** — Implemented: via MVVM `isActive` property + `ReloadGallery`
23. ✅ **UpdateUndoButton after apply** — Implemented: `UpdateUndoButton()` called after apply
24. ✅ **Empty cursor transparency check** — Implemented: `IsFullyTransparent` check during import
25. ✅ **Image-to-cursor conversion (full)** — Implemented: `ConvertToCursorTempFile` handles static images + animated GIF-to-`.ani` via `GifDecoderService` (LZW decode, disposal methods, alpha compositing) + `AniCursorWriter.Save`
26. ✅ **DebugLogger** — Removed: debug logging removed from Linux port (not needed)
27. ✅ **CanvasSizeDialog** — Implemented (duplicate of #14)
28. ✅ **ImportImageDialog** — Implemented (duplicate of #15)
29. ✅ **Archive drag-drop support** — Implemented: `ResolveCursorFiles` supports archives via `ArchiveImportService.ExtractToTempFolder`
30. ✅ **Preset context menu: UseScaling toggle** — Implemented: `OnMenuUseScaling` with checkmark
31. ✅ **Preset context menu: ScaleMode toggle** — Implemented: `OnMenuScaleMode` (Nearest/Smooth)
32. ✅ **Import toast feedback** — Implemented: shows count of imported presets via `LocToastImported`
33. ✅ **Import version error handling** — Implemented: `PackageVersionUnsupportedException` with version numbers
34. ✅ **Animated cursor (.ani) in PaintEditor** — Implemented: `OpenPaintEditor` reads `.ani` via `AniCursorReader`, saves via `AniCursorWriter`
35. ✅ **Per-slot browse transparency check** — Implemented: `IsFullyTransparent` check in `BrowseForSlot`
36. ✅ **HotspotEditorWindow from PresetEditor** — Implemented: `HotspotEditorWindow` dialog + pivot button (🎯) in PresetEditor
37. ✅ **PresetEditor: Download preset as folder** — Implemented: `OnDownloadPresetClick` via `DownloadPresetAsFolder`
38. ✅ **PresetEditor: Download More menu** — Implemented: Xcursor/Linux Archive/Readme export from editor
39. ✅ **PresetEditor: Recursive folder import** — Implemented: `SearchOption.AllDirectories`
40. ✅ **PresetEditor: Xcursor theme import via drag-drop** — Implemented: `TryImportXcursorTheme`
41. ✅ **PresetEditor: Per-slot drag-drop indicators** — Implemented: per-slot `DropIndicator` borders with `OnSlotDragOver`/`OnSlotDrop` handlers, window-level `DragEnter` showing all indicators (respecting `IsLocked`), `DragLeave`/`Drop` hiding them
42. ✅ **PresetEditor: Save validation feedback** — Implemented: toast notification if no files
43. ✅ **PresetEditor: Import folder feedback** — Implemented: messages for "no cursors", "empty skipped", "no match"
44. ✅ **Locked roles in preset download** — Implemented: `ExportBundle` reads `preset.LockedRoles` directly
45. ✅ **Preview cache invalidation after edit** — Implemented: `InvalidatePresetPreviewCache` after save
46. ✅ **Save error handling in editor** — Implemented: toast on save exception
47. ✅ **UpdateApplySizeButtonHighlight** — Implemented (duplicate of #21)

### Behavioral Differences

1. **Default `_useScaling`** — WPF: `true`, Linux: `false`
2. **GroupAttachZoneMargin** — WPF: `0.25`, Linux: `0.2`
3. **Slot dimensions** — WPF: `160×204`, Linux: `160×180`
4. **Download format** — WPF: `.exe`, Linux: `.tar.gz`
5. **System cursor download** — WPF: 2 modes, Linux: 3 modes (adds xcursor)
6. **Import formats** — WPF: `.cursorpalette`, `.zip`; Linux: adds `.tar.gz`
7. **Theme application** — WPF: recreates window for theme toggle; Linux: inline `RequestedThemeVariant` via `ThemeManager` (both persist choice via `AppState`; both reload gallery for language changes without recreation)
8. **Preset download** — WPF: as folder; Linux: as `.cursorpalette` bundle
9. **Delete confirmation** — WPF: MessageBox for both presets and groups; Linux: dialog for presets, none for groups
10. **OpenFolderToggle visual** — WPF: brush color change; Linux: opacity change
11. **Toast animation** — WPF: easing functions; Linux: linear interpolation
12. **UpdateChecker** — WPF: finds `.exe` asset URL; Linux: links to releases page
13. **AnimatedGifWriter** — WPF: 4979 bytes; Linux: 7384 bytes — separate implementations, Linux version is larger (likely includes LZW encoding logic that WPF delegates to framework)
14. **GetSuggestedPresetName** — Both check folders and archives; implementation now consistent
15. **ResolveCursorFiles directory enumeration** — Both use `SearchOption.AllDirectories` for recursive search
16. **ResolveCursorFiles archive support** — Both extract archives (`.zip`, `.rar`, `.7z`) via `ArchiveImportService`
17. **LinkBadge rendering** — WPF: `Rectangle` with `OpacityMask` image icon (`LinkIcon32.png`); Linux: `Rectangle` with `OpacityMask` image icon via `IconHelper.CreateIcon`
18. **PresetEditor editing buttons** — Both have `PivotButton` (🎯, hotspot editor) + `PaintButton` (🖌, paint editor); Linux also has `BrowseButton`, `PickExistingButton`, `ClearButton`, `DownloadButton`, `LockButton`
19. **PresetEditor scale mode icon** — Both use image resources (`StairIcon24.png`, `ExpandIcon32.png`) via `IconHelper`
20. **PresetEditor slot placeholder** — WPF: shows default cursor at `PlaceholderOpacity` (0.45) with `PlaceholderBadge`; Linux: clears preview entirely (no visual placeholder)
21. **PresetEditor size constant source** — WPF: `RegistryCursorService.SizeStep`; Linux: `CursorConstants.SizeStep`
22. **Loc class implementation** — WPF: inline static class in `App.xaml.cs`, uses `Application.Current.TryFindResource`; Linux: separate `Loc.cs` in Core, uses `LocalizationManager.Get`
23. **Update toast** — WPF: `Loc.Get(LocToastUpdateAvailable)` (no version); Linux: `Loc.Format(LocToastUpdateAvailable, version)` (includes version)
24. **Version comparison** — WPF: inline `Version.TryParse` + `>` comparison; Linux: delegates to `UpdateChecker.IsUpdateAvailableAsync()`
25. **Loading overlay spinner** — WPF: `Storyboard` animation; Linux: `DispatcherTimer` (16ms, 6°/tick) with manual `RotateTransform`

### Reorganized (Not Missing, Just Different File Structure)

1. **PaintEditor Zoom** — WPF: `.Zoom.cs` partial; Linux: in main `PaintEditorWindow.cs` — fully functional (`ZoomAtPoint`, ctrl+scroll, zoom in/out buttons)
2. **PaintEditor Sprite move/snap** — WPF: `.Sprite.cs` partial; Linux: in `PaintEditorWindow.Tools.cs` — fully functional (move buttons, 9-point snap grid)
3. **PaintEditor Render** — WPF: `.Render.cs` partial; Linux: in `PaintEditorWindow.Canvas.cs`
4. **Hotspot editing** — WPF: standalone `HotspotEditorWindow.xaml.cs`; Linux: standalone `HotspotEditorWindow.cs` (mirrors WPF) + integrated hotspot tool in `PaintEditorWindow.Hotspot.cs`
5. **Linux has `.Keyboard.cs`** — additional keyboard handling partial not present in WPF
6. **Linux has `.GifImport.cs`** — raw GIF decoder with LZW (WPF uses framework `GifBitmapDecoder`)
7. **Linux has `.Timeline.cs`** — timeline/animation playback partial (WPF has `.Timeline.xaml` + code-behind)

### Confirmed Identical Behavior

1. All shared Core services (PresetStore, GroupStore, CursorScalerService, etc.)
2. Localization key strings and usage patterns
3. Help text service content
4. Group color palette definitions
5. Cursor role definitions
6. Board order reconciliation logic
7. Toast timing constants
8. Text zoom behavior in InfoHelpWindow
9. Body parsing logic in InfoHelpWindow
10. Version comparison logic in UpdateChecker
11. GitHub API URL and UserAgent
12. MIT license text
13. AppInfo constants (Author, LicenseName, GitHubUrl)
14. PaintEditor sprite move/snap logic (identical `SnapOffset`, `ParseFraction`, `HorizontalRange`, `VerticalRange`)
15. PaintEditor zoom constants (`CanvasZoomStep`, `MinCanvasDimension`, `MaxCanvasDimension`)
16. PaintEditor hotspot marker visual concept (marker + glow ellipse, drag-to-set)
17. Gallery cell size — both use `148×148` with `6px` margin and `10px` corner radius
18. Theme toggle icons — both use "🌙" (dark) and "☀" (light)
19. Mixed badge — both use "🧩" emoji for presets with `RoleRefs`
20. Drag format strings — both use `"CursorPalette.PresetId"` and `"CursorPalette.GroupId"`
21. `BuildReferenceLabel()` logic — both resolve preset name + filename identically
22. `CursorRoles.MatchByFileName()` — shared Core logic for role matching
23. Export tile constants (`CellSize=120`, `CellPreviewSize=40`, `CellCornerRadius=10`) — identical
