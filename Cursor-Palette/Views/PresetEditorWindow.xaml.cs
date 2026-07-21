using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Ellipse = System.Windows.Shapes.Ellipse;
using Rectangle = System.Windows.Shapes.Rectangle;
using CursorPalette.Models;
using CursorPalette.Services;
using Microsoft.Win32;

namespace CursorPalette.Views;

public partial class PresetEditorWindow : Window
{
	[DllImport("user32.dll")]
	private static extern IntPtr WindowFromPoint(POINT cursorPoint);

	[DllImport("user32.dll")]
	private static extern IntPtr GetAncestor(IntPtr windowHandle, uint gaFlags);

	[DllImport("user32.dll")]
	private static extern bool GetCursorPos(out POINT cursorPosition);

	private const uint GaRoot = 2;

	[StructLayout(LayoutKind.Sequential)]
	private struct POINT { public int X; public int Y; }

	private sealed class Slot
	{
		public required CursorRoleInfo Role { get; init; }
		public required string? DefaultPath { get; init; }
		public string? SourcePath { get; set; }
		public string? RefPresetId { get; set; }
		public string? RefFileName { get; set; }
		public Image PreviewImage { get; init; } = null!;
		public TextBlock FileText { get; init; } = null!;
		public Button ClearButton { get; init; } = null!;
		public Button PivotButton { get; init; } = null!;
		public Button LockButton { get; init; } = null!;
		public Rectangle LockIcon { get; init; } = null!;
		public Border DownloadButton { get; init; } = null!;
		public StackPanel PrimaryButtons { get; init; } = null!;
		public Rectangle DropIndicator { get; init; } = null!;
		public FrameworkElement PlaceholderBadge { get; init; } = null!;
		public FrameworkElement HotspotDot { get; init; } = null!;
		public FrameworkElement LinkBadge { get; init; } = null!;
		public bool IsLocked { get; set; }
	}

	private const string PixelSuffix = "px";
	private const string CurExtension = ".cur";
	private const string AniExtension = ".ani";
	private const double SlotWidth = 160;
	private const double SlotHeight = 204;
	private const double SlotMargin = 6;
	private const double SlotCornerRadius = 10;
	private const double SlotBorderThickness = 2;
	private const double SlotPreviewSize = 40;
	private const double RoleNameFontSize = 12;
	private const double FileTextFontSize = 11;
	private const double ButtonFontSize = 11;
	private const double PlaceholderBadgeFontSize = 9;
	private const double PlaceholderOpacity = 0.45;
	private const double HotspotDotSize = 8;
	private const string ClearButtonContent = "✕";
	private const string PivotButtonContent = "🎯";
	private const string PickExistingButtonContent = "🧩";
	private const string LinkIconUri = "pack://application:,,,/Resources/LinkIcon32.png";
	private const double IconButtonSize = 28;
	private const double CornerBadgeSize = 22;
	private const double PreviewTopMargin = 10;
	private const double LinkBadgeIconSize = 16;
	private const string LockIconUri = "pack://application:,,,/Resources/LockIcon26.png";
	private const double LockIconSize = 14;
	private const string DownloadIconUri = "pack://application:,,,/Resources/DownloadIcon32.png";
	private const double DownloadIconSize = 16;

	private const string BrushAccent = "Brush.Accent";
	private const string BrushBorder = "Brush.Border";
	private const string BrushSurface = "Brush.Surface";
	private const string BrushSurfaceHover = "Brush.SurfaceHover";
	private const string BrushText = "Brush.Text";
	private const string BrushTextDim = "Brush.TextDim";
	private const string StyleAccentButton = "Style.AccentButton";
	private const string StyleButton = "Style.Button";
	private const string StyleDangerButton = "Style.DangerButton";

	private const string LocInfoTitle = "S.Info.Title";
	private const string LocInfoEditor = "S.Info.Editor";
	private const string LocEditorTitleNew = "S.Editor.TitleNew";
	private const string LocEditorTitleEdit = "S.Editor.TitleEdit";
	private const string LocDefaultPresetName = "S.DefaultPresetName";
	private const string LocEditorPlaceholderBadge = "S.Editor.PlaceholderBadge";
	private const string LocEditorEmptySlot = "S.Editor.EmptySlot";
	private const string LocEditorBrowse = "S.Editor.Browse";
	private const string LocEditorBrowseFolder = "S.Editor.BrowseFolder";
	private const string LocEditorPickExisting = "S.Editor.PickExisting";
	private const string LocEditorPivotTooltip = "S.Editor.Pivot.Tooltip";
	private const string LocEditorPivotDisabledTooltip = "S.Editor.Pivot.Disabled.Tooltip";
	private const string LocEditorClearSlot = "S.Editor.ClearSlot";
	private const string LocEditorLockTooltip = "S.Editor.Lock.Tooltip";
	private const string LocEditorUnlockTooltip = "S.Editor.Unlock.Tooltip";
	private const string LocEditorDownloadTooltip = "S.Editor.Download.Tooltip";
	private const string LocEditorFileFilter = "S.Editor.FileFilter";
	private const string LocEditorLinkedRoleTooltip = "S.Editor.LinkedRole.Tooltip";
	private const string LocEditorNoCursorInFolder = "S.Editor.NoCursorInFolder";
	private const string LocEditorNoMatchInFolder = "S.Editor.NoMatchInFolder";
	private const string LocEditorNoFiles = "S.Editor.NoFiles";
	private const string LocToastSizeApplied = "S.Toast.SizeApplied";
	private const string LocToastDownloaded = "S.Toast.Downloaded";
	private const string LocToastPresetDownloaded = "S.Toast.PresetDownloaded";
	private const string LocErrorArchiveExtractFailed = "S.Error.ArchiveExtractFailed";

	private readonly List<Slot> _slots = new();

	public PresetDraft? Result { get; private set; }

	public PresetEditorWindow(Preset? existing, IReadOnlyList<string> droppedFiles, string? suggestedName = null)
	{
		InitializeComponent();

		Title = Loc.Get(existing == null ? LocEditorTitleNew : LocEditorTitleEdit);
		NameBox.Text = existing?.Name
			?? (string.IsNullOrWhiteSpace(suggestedName) ? null : suggestedName)
			?? Loc.Get(LocDefaultPresetName);

		Result = null;
		_draftId = existing?.Id;
		_appliedPreviewSizePx = RegistryCursorService.GetBaseSize();
		_baseSize = existing?.BaseSize ?? _appliedPreviewSizePx;

		var uiScale = AppState.GetUiScale();
		UiScaleTransform.ScaleX = uiScale;
		UiScaleTransform.ScaleY = uiScale;

		EditorSizeSlider.Value = (_baseSize - RegistryCursorService.SizeStep) / (double)RegistryCursorService.SizeStep;
		EditorSizeValueText.Text = $"{_baseSize} {PixelSuffix}";
		_sizeSliderReady = true;
		UpdateApplySizeButtonHighlight();

		var systemDefaults = RegistryCursorService.GetWindowsDefaultValues();

		foreach (var role in CursorRoles.All)
		{
			var defaultPath = systemDefaults.GetValueOrDefault(role.RegistryName);
			if (string.IsNullOrWhiteSpace(defaultPath))
				defaultPath = PlaceholderCursorDefaults.GetPath(role.RegistryName);

			var slot = CreateSlot(role, defaultPath);
			_slots.Add(slot);

			if (existing != null)
			{
				var path = PresetStore.GetRoleFilePath(existing, role.RegistryName);
				if (path != null && File.Exists(path))
				{
					if (existing.RoleRefs.TryGetValue(role.RegistryName, out var reference))
						SetSlotReference(slot, reference.PresetId, reference.FileName);
					else
						SetSlotSource(slot, path);
				}

				if (existing.LockedRoles.Contains(role.RegistryName))
					SetSlotLocked(slot, true);
			}
		}

		foreach (var file in droppedFiles)
		{
			var role = CursorRoles.MatchByFileName(file);

			if (role == null)
				continue;

			var slot = _slots.First(slot => slot.Role.RegistryName == role.RegistryName);
			SetSlotSource(slot, file);
		}
	}

	private readonly string? _draftId;
	private int _baseSize;
	private int _appliedPreviewSizePx;
	private bool _sizeSliderReady;

	private void OnInfoButtonClick(object sender, RoutedEventArgs e)
	{
		new InfoHelpWindow(Loc.Get(LocInfoTitle), Loc.Get(LocInfoEditor)) { Owner = this }.ShowDialog();
	}

	private void OnEditorSizeSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (EditorSizeValueText == null)
			return;

		var sizePx = RegistryCursorService.SizeStep + (int)e.NewValue * RegistryCursorService.SizeStep;
		EditorSizeValueText.Text = $"{sizePx} {PixelSuffix}";

		if (!_sizeSliderReady)
			return;

		_baseSize = sizePx;

		(Owner as MainWindow)?.SyncSizeSlider(sizePx);
		UpdateApplySizeButtonHighlight();
	}

	private void UpdateApplySizeButtonHighlight() =>
		EditorApplySizeButton.Style = (Style)Application.Current.Resources[
			_baseSize != _appliedPreviewSizePx ? StyleAccentButton : StyleButton];

	private void OnEditorApplySizeClick(object sender, RoutedEventArgs e)
	{
		RegistryCursorService.SetBaseSize(_baseSize);
		_appliedPreviewSizePx = _baseSize;
		UpdateApplySizeButtonHighlight();
		ToastService.Show(EditorRootGrid, Loc.Get(LocToastSizeApplied));
	}

	private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

	private Slot CreateSlot(CursorRoleInfo role, string? defaultPath)
	{
		var preview = new Image { Width = SlotPreviewSize, Height = SlotPreviewSize, SnapsToDevicePixels = true };
		RenderOptions.SetBitmapScalingMode(preview, BitmapScalingMode.NearestNeighbor);

		var hotspotDot = new Ellipse
		{
			Width = HotspotDotSize,
			Height = HotspotDotSize,
			Fill = Brush(BrushAccent),
			Stroke = System.Windows.Media.Brushes.White,
			StrokeThickness = 1.5,
			IsHitTestVisible = false,
			Visibility = Visibility.Collapsed,
		};

		var previewHost = new Canvas
		{
			Width = SlotPreviewSize,
			Height = SlotPreviewSize,
			Margin = new Thickness(0, PreviewTopMargin, 0, 0),
		};
		Canvas.SetLeft(preview, 0);
		Canvas.SetTop(preview, 0);
		previewHost.Children.Add(preview);
		previewHost.Children.Add(hotspotDot);

		var placeholderBadge = new Border
		{
			Background = Brush(BrushSurfaceHover),
			BorderBrush = Brush(BrushBorder),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(4),
			Padding = new Thickness(4, 1, 4, 1),
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0, 0, 0, 3),
			Child = new TextBlock
			{
				Text = Loc.Get(LocEditorPlaceholderBadge),
				FontSize = PlaceholderBadgeFontSize,
				Foreground = Brush(BrushTextDim),
			},
		};

		var roleName = new TextBlock
		{
			Text = Loc.Get("S." + role.DisplayKey),
			FontWeight = FontWeights.SemiBold,
			TextAlignment = TextAlignment.Center,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(4, 6, 4, 0),
			FontSize = RoleNameFontSize,
		};

		var fileText = new TextBlock
		{
			Text = Loc.Get(LocEditorEmptySlot),
			Foreground = Brush(BrushTextDim),
			FontSize = FileTextFontSize,
			TextAlignment = TextAlignment.Center,
			TextTrimming = TextTrimming.CharacterEllipsis,
			Margin = new Thickness(4, 2, 4, 0),
		};

		var browseButton = new Button
		{
			Content = Loc.Get(LocEditorBrowse),
			Style = (Style)Application.Current.Resources[StyleButton],
			FontSize = ButtonFontSize,
			Padding = new Thickness(6, 3, 6, 3),
			Margin = new Thickness(0, 6, 0, 0),
			HorizontalAlignment = HorizontalAlignment.Center,
		};

		var pickExistingButton = new Button
		{
			Content = PickExistingButtonContent,
			Style = (Style)Application.Current.Resources[StyleButton],
			FontSize = ButtonFontSize,
			Width = IconButtonSize,
			Height = IconButtonSize,
			Padding = new Thickness(0),
			ToolTip = Loc.Get(LocEditorPickExisting),
		};
		var pivotButton = new Button
		{
			Content = PivotButtonContent,
			Style = (Style)Application.Current.Resources[StyleButton],
			FontSize = ButtonFontSize,
			Width = IconButtonSize,
			Height = IconButtonSize,
			Padding = new Thickness(0),
			Margin = new Thickness(6, 0, 0, 0),
			ToolTip = Loc.Get(LocEditorPivotTooltip),
			Visibility = Visibility.Collapsed,
		};

		var iconButtonsRow = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0, 6, 0, 0),
		};
		iconButtonsRow.Children.Add(pickExistingButton);
		iconButtonsRow.Children.Add(pivotButton);

		var primaryButtons = new StackPanel
		{
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		primaryButtons.Children.Add(browseButton);
		primaryButtons.Children.Add(iconButtonsRow);

		var clearButton = new Button
		{
			Content = ClearButtonContent,
			Style = (Style)Application.Current.Resources[StyleDangerButton],
			FontSize = ButtonFontSize,
			Width = CornerBadgeSize,
			Height = CornerBadgeSize,
			Padding = new Thickness(0),
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(0, 6, 6, 0),
			ToolTip = Loc.Get(LocEditorClearSlot),
			Visibility = Visibility.Collapsed,
		};

		var linkBadge = new Rectangle
		{
			Width = LinkBadgeIconSize,
			Height = LinkBadgeIconSize,
			Fill = Brush(BrushAccent),
			OpacityMask = new ImageBrush(new BitmapImage(new Uri(LinkIconUri))),
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0, 0, 0, 4),
			Visibility = Visibility.Collapsed,
		};

		var lockIcon = new Rectangle
		{
			Width = LockIconSize,
			Height = LockIconSize,
			Fill = Brush(BrushText),
			OpacityMask = new ImageBrush(new BitmapImage(new Uri(LockIconUri))),
			IsHitTestVisible = false,
		};

		var lockButton = new Button
		{
			Content = lockIcon,
			Style = (Style)Application.Current.Resources[StyleButton],
			Width = CornerBadgeSize,
			Height = CornerBadgeSize,
			Padding = new Thickness(0),
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(6, 6, 0, 0),
			ToolTip = Loc.Get(LocEditorLockTooltip),
		};

		var downloadIcon = new Rectangle
		{
			Width = DownloadIconSize,
			Height = DownloadIconSize,
			Fill = Brush(BrushTextDim),
			OpacityMask = new ImageBrush(new BitmapImage(new Uri(DownloadIconUri))),
			IsHitTestVisible = false,
		};

		var downloadButton = new Border
		{
			Width = CornerBadgeSize,
			Height = CornerBadgeSize,
			Background = Brushes.Transparent,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Bottom,
			Margin = new Thickness(0, 0, 6, 6),
			Cursor = Cursors.Hand,
			ToolTip = Loc.Get(LocEditorDownloadTooltip),
			Visibility = Visibility.Collapsed,
			Child = downloadIcon,
		};
		downloadButton.MouseEnter += (_, _) => downloadIcon.Fill = Brush(BrushAccent);
		downloadButton.MouseLeave += (_, _) => downloadIcon.Fill = Brush(BrushTextDim);

		var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
		panel.Children.Add(placeholderBadge);
		panel.Children.Add(linkBadge);
		panel.Children.Add(previewHost);
		panel.Children.Add(roleName);
		panel.Children.Add(fileText);
		panel.Children.Add(primaryButtons);

		var dropIndicator = new Rectangle
		{
			Stroke = Brush(BrushAccent),
			StrokeThickness = 3,
			StrokeDashArray = new DoubleCollection { 4, 2 },
			RadiusX = SlotCornerRadius,
			RadiusY = SlotCornerRadius,
			Margin = new Thickness(2),
			IsHitTestVisible = false,
			Visibility = Visibility.Collapsed,
		};

		var slotContent = new Grid();
		slotContent.Children.Add(panel);
		slotContent.Children.Add(clearButton);
		slotContent.Children.Add(lockButton);
		slotContent.Children.Add(downloadButton);
		slotContent.Children.Add(dropIndicator);

		var border = new Border
		{
			Width = SlotWidth,
			Height = SlotHeight,
			Margin = new Thickness(SlotMargin),
			CornerRadius = new CornerRadius(SlotCornerRadius),
			Background = Brush(BrushSurface),
			BorderThickness = new Thickness(SlotBorderThickness),
			BorderBrush = Brush(BrushBorder),
			Child = slotContent,
			AllowDrop = true,
		};

		var slot = new Slot
		{
			Role = role,
			DefaultPath = defaultPath,
			PreviewImage = preview,
			FileText = fileText,
			ClearButton = clearButton,
			PivotButton = pivotButton,
			LockButton = lockButton,
			LockIcon = lockIcon,
			DownloadButton = downloadButton,
			PrimaryButtons = primaryButtons,
			DropIndicator = dropIndicator,
			PlaceholderBadge = placeholderBadge,
			HotspotDot = hotspotDot,
			LinkBadge = linkBadge,
		};

		browseButton.Click += (_, _) => BrowseForSlot(slot);
		pickExistingButton.Click += (_, _) => PickExistingForSlot(slot);
		pivotButton.Click += (_, _) => OpenHotspotEditor(slot);
		clearButton.Click += (_, _) => ClearSlot(slot);
		lockButton.Click += (_, _) => SetSlotLocked(slot, !slot.IsLocked);
		downloadButton.MouseLeftButtonUp += (_, e) =>
		{
			DownloadSlot(slot);
			e.Handled = true;
		};

		border.DragOver += (_, e) =>
		{
			e.Effects = !slot.IsLocked && GetSingleCursorFile(e) != null ? DragDropEffects.Copy : DragDropEffects.None;
			e.Handled = true;
		};
		border.Drop += (_, e) =>
		{
			HideAllDropIndicators();
			if (slot.IsLocked)
			{
				e.Handled = true;
				return;
			}

			var file = GetSingleCursorFile(e);
			if (file != null)
			{
				SetSlotSource(slot, file);
				e.Handled = true;
			}
		};

		Slots.Items.Add(border);

		SetSlotPlaceholder(slot);

		return slot;
	}

	private void BrowseForSlot(Slot slot)
	{
		var dialog = new OpenFileDialog
		{
			Filter = Loc.Get(LocEditorFileFilter),
			CheckFileExists = true,
		};
		if (dialog.ShowDialog(this) == true)
			SetSlotSource(slot, dialog.FileName);
	}

	private void SetSlotSource(Slot slot, string path)
	{
		slot.SourcePath = path;
		slot.RefPresetId = null;
		slot.RefFileName = null;
		CursorPreviewService.ApplyPreview(slot.PreviewImage, path);
		slot.PreviewImage.Opacity = 1;
		slot.PlaceholderBadge.Visibility = Visibility.Collapsed;
		slot.LinkBadge.Visibility = Visibility.Collapsed;
		slot.FileText.Text = Path.GetFileName(path);
		slot.FileText.Foreground = Brush(BrushText);
		slot.ClearButton.Visibility = Visibility.Visible;
		slot.PivotButton.Visibility = Visibility.Visible;
		slot.PivotButton.IsEnabled = true;
		slot.PivotButton.ToolTip = Loc.Get(LocEditorPivotTooltip);
		slot.DownloadButton.Visibility = Visibility.Visible;
		UpdateHotspotDot(slot);
	}

	private void SetSlotReference(Slot slot, string presetId, string fileName)
	{
		slot.SourcePath = null;
		slot.RefPresetId = presetId;
		slot.RefFileName = fileName;

		var resolvedPath = Path.Combine(PresetStore.GetFilesDir(presetId), fileName);
		var label = BuildReferenceLabel(presetId, fileName);
		CursorPreviewService.ApplyPreview(slot.PreviewImage, resolvedPath);
		slot.PreviewImage.Opacity = 1;
		slot.PlaceholderBadge.Visibility = Visibility.Collapsed;
		slot.LinkBadge.Visibility = Visibility.Visible;
		slot.LinkBadge.ToolTip = Loc.Format(LocEditorLinkedRoleTooltip, label);
		slot.FileText.Text = label;
		slot.FileText.Foreground = Brush(BrushText);
		slot.ClearButton.Visibility = Visibility.Visible;
		slot.PivotButton.Visibility = Visibility.Visible;
		slot.PivotButton.IsEnabled = false;
		slot.PivotButton.ToolTip = Loc.Get(LocEditorPivotDisabledTooltip);
		slot.DownloadButton.Visibility = Visibility.Visible;
		UpdateHotspotDot(slot);
	}

	private static string BuildReferenceLabel(string presetId, string fileName)
	{
		var sourceName = PresetStore.LoadAll().FirstOrDefault(p => p.Id == presetId)?.Name;
		return sourceName != null ? $"{sourceName} / {fileName}" : fileName;
	}

	private void SetSlotPlaceholder(Slot slot)
	{
		slot.SourcePath = null;
		slot.RefPresetId = null;
		slot.RefFileName = null;
		CursorPreviewService.ApplyPreview(slot.PreviewImage, slot.DefaultPath);
		slot.PreviewImage.Opacity = PlaceholderOpacity;
		slot.PlaceholderBadge.Visibility = string.IsNullOrWhiteSpace(slot.DefaultPath)
			? Visibility.Collapsed
			: Visibility.Visible;
		slot.FileText.Text = Loc.Get(LocEditorEmptySlot);
		slot.FileText.Foreground = Brush(BrushTextDim);
		slot.ClearButton.Visibility = Visibility.Collapsed;
		slot.PivotButton.Visibility = Visibility.Collapsed;
		slot.DownloadButton.Visibility = Visibility.Collapsed;
		slot.HotspotDot.Visibility = Visibility.Collapsed;
		slot.LinkBadge.Visibility = Visibility.Collapsed;
	}

	private void ClearSlot(Slot slot) => SetSlotPlaceholder(slot);

	private void SetSlotLocked(Slot slot, bool locked)
	{
		slot.IsLocked = locked;

		var accent = Brush(BrushAccent);
		slot.LockButton.BorderBrush = locked ? accent : Brush(BrushBorder);
		slot.LockButton.BorderThickness = new Thickness(locked ? 2 : 1);
		slot.LockIcon.Fill = locked ? accent : Brush(BrushText);
		slot.LockButton.ToolTip = Loc.Get(locked ? LocEditorUnlockTooltip : LocEditorLockTooltip);

		slot.PrimaryButtons.IsEnabled = !locked;
		slot.ClearButton.IsEnabled = !locked;
	}

	private static string? GetSlotResolvedPath(Slot slot) =>
		slot.SourcePath ?? (slot.RefPresetId != null && slot.RefFileName != null
			? Path.Combine(PresetStore.GetFilesDir(slot.RefPresetId), slot.RefFileName)
			: null);

	private void UpdateHotspotDot(Slot slot)
	{
		var resolvedPath = GetSlotResolvedPath(slot);
		var hotspot = resolvedPath != null ? CursorHotspotService.Read(resolvedPath) : null;

		if (hotspot == null)
		{
			slot.HotspotDot.Visibility = Visibility.Collapsed;
			return;
		}

		var displayX = (hotspot.X + 0.5) / hotspot.Width * SlotPreviewSize;
		var displayY = (hotspot.Y + 0.5) / hotspot.Height * SlotPreviewSize;

		Canvas.SetLeft(slot.HotspotDot, displayX - HotspotDotSize / 2);
		Canvas.SetTop(slot.HotspotDot, displayY - HotspotDotSize / 2);
		slot.HotspotDot.Visibility = Visibility.Visible;
	}

	private void DownloadSlot(Slot slot)
	{
		var resolvedPath = GetSlotResolvedPath(slot);
		if (resolvedPath == null || !File.Exists(resolvedPath))
			return;

		Directory.CreateDirectory(AppPaths.DownloadsDir);

		var baseName = slot.Role.RegistryName;
		var extension = Path.GetExtension(resolvedPath);
		var destPath = Path.Combine(AppPaths.DownloadsDir, $"{baseName}{extension}");

		var attempt = 1;
		while (File.Exists(destPath))
			destPath = Path.Combine(AppPaths.DownloadsDir, $"{baseName} ({attempt++}){extension}");

		File.Copy(resolvedPath, destPath);
		var now = DateTime.Now;
		File.SetCreationTime(destPath, now);
		File.SetLastWriteTime(destPath, now);
		ToastService.Show(EditorRootGrid, Loc.Format(LocToastDownloaded, Path.GetFileName(destPath)));
	}

	private void OnDownloadPresetClick(object sender, RoutedEventArgs e)
	{
		var invalid = Path.GetInvalidPathChars();
		var presetName = string.Join("", NameBox.Text.Where(character => !invalid.Contains(character))).Trim();
		if (string.IsNullOrWhiteSpace(presetName))
			presetName = Loc.Get(LocDefaultPresetName);

		var destDir = Path.Combine(AppPaths.DownloadsDir, presetName);

		var attempt = 1;
		while (Directory.Exists(destDir))
			destDir = Path.Combine(AppPaths.DownloadsDir, $"{presetName} ({attempt++})");

		Directory.CreateDirectory(destDir);

		var count = 0;
		foreach (var slot in _slots)
		{
			var resolvedPath = GetSlotResolvedPath(slot);
			if (resolvedPath == null || !File.Exists(resolvedPath))
				continue;

			var extension = Path.GetExtension(resolvedPath);
			var destPath = Path.Combine(destDir, $"{slot.Role.RegistryName}{extension}");
			File.Copy(resolvedPath, destPath);
			var now = DateTime.Now;
			File.SetCreationTime(destPath, now);
			File.SetLastWriteTime(destPath, now);
			count++;
		}

		if (count == 0)
		{
			Directory.Delete(destDir);
			return;
		}

		ToastService.Show(EditorRootGrid, Loc.Format(LocToastPresetDownloaded, presetName, count));
	}

	private void OpenHotspotEditor(Slot slot)
	{
		var resolvedPath = GetSlotResolvedPath(slot);
		if (resolvedPath == null)
			return;

		var hotspot = CursorHotspotService.Read(resolvedPath);
		if (hotspot == null)
			return;

		var editor = new HotspotEditorWindow(resolvedPath, hotspot) { Owner = this };
		if (editor.ShowDialog() != true)
			return;

		var tempPath = Path.Combine(Path.GetTempPath(),
			$"cursor-palette-hotspot-{Guid.NewGuid():N}{Path.GetExtension(resolvedPath)}");
		CursorHotspotService.WriteWithHotspot(resolvedPath, tempPath, editor.ResultX, editor.ResultY);
		CursorPreviewService.Invalidate(tempPath);

		SetSlotSource(slot, tempPath);
	}

	private void PickExistingForSlot(Slot slot)
	{
		var presetPicker = new ExistingPresetPickerWindow(PresetStore.LoadAll()) { Owner = this };
		if (presetPicker.ShowDialog() != true || presetPicker.SelectedPreset == null)
			return;

		var rolePicker = new RolePickerWindow(presetPicker.SelectedPreset, slot.Role.RegistryName) { Owner = this };
		if (rolePicker.ShowDialog() != true || rolePicker.SelectedRole == null)
			return;

		var flatRef = PresetStore.ResolveLeafRef(presetPicker.SelectedPreset, rolePicker.SelectedRole);
		if (flatRef == null)
			return;

		SetSlotReference(slot, flatRef.PresetId, flatRef.FileName);
	}

	private void OnBrowseFolderClick(object sender, RoutedEventArgs e)
	{
		var dialog = new OpenFolderDialog
		{
			Title = Loc.Get(LocEditorBrowseFolder),
		};

		if (dialog.ShowDialog(this) == true)
			ImportFolder(dialog.FolderName);
	}

	private void OnPresetWindowDragEnter(object sender, DragEventArgs e)
	{
		if (HasDroppableFolderSource(e))
		{
			FolderDropIndicator.Visibility = Visibility.Visible;
			SetSlotIndicatorsVisibility(Visibility.Collapsed);
		}
		else if (GetSingleCursorFile(e) != null)
		{
			FolderDropIndicator.Visibility = Visibility.Collapsed;
			SetSlotIndicatorsVisibility(Visibility.Visible);
		}
		else
		{
			HideAllDropIndicators();
		}
	}

	private void OnPresetWindowDragLeave(object sender, DragEventArgs e)
	{
		if (IsMouseOverThisWindow())
			return;

		HideAllDropIndicators();
	}

	private bool IsMouseOverThisWindow()
	{
		GetCursorPos(out var cursorPoint);
		var handleFromPoint = WindowFromPoint(cursorPoint);

		if (handleFromPoint == IntPtr.Zero)
			return false;

		var windowHandle = new WindowInteropHelper(this).Handle;
		var rootHandle = GetAncestor(handleFromPoint, GaRoot);

		return rootHandle == windowHandle;
	}

	private void OnPresetWindowDrop(object sender, DragEventArgs e)
	{
		HideAllDropIndicators();
	}

	private void HideAllDropIndicators()
	{
		FolderDropIndicator.Visibility = Visibility.Collapsed;
		SetSlotIndicatorsVisibility(Visibility.Collapsed);
	}

	private void SetSlotIndicatorsVisibility(Visibility visibility)
	{
		foreach (var slot in _slots)
			slot.DropIndicator.Visibility = visibility == Visibility.Visible && slot.IsLocked
				? Visibility.Collapsed
				: visibility;
	}

	private void OnFolderDragOver(object sender, DragEventArgs e)
	{
		e.Effects = HasDroppableFolderSource(e) ? DragDropEffects.Copy : DragDropEffects.None;
		e.Handled = true;
	}

	private void OnFolderDrop(object sender, DragEventArgs e)
	{
		HideAllDropIndicators();

		if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
			return;

		e.Handled = true;

		// Deferred: MessageBox.Show synchronously inside Drop confuses the OS
		// OLE drag-drop loop and leaves the cursor stuck in a "still dragging" state.
		Dispatcher.BeginInvoke(new Action(() => HandleDroppedFolderPaths(paths)), DispatcherPriority.Input);
	}

	private void HandleDroppedFolderPaths(string[] paths)
	{
		var folder = paths.FirstOrDefault(Directory.Exists);
		if (folder != null)
		{
			ImportFolder(folder);
			return;
		}

		var archive = paths.FirstOrDefault(ArchiveImportService.IsArchiveFile);
		if (archive == null)
			return;

		try
		{
			ImportFolder(ArchiveImportService.ExtractToTempFolder(archive), recursive: true,
				displayName: Path.GetFileNameWithoutExtension(archive));
		}
		catch (Exception ex)
		{
			MessageBox.Show(Loc.Format(LocErrorArchiveExtractFailed, ex.Message), Title,
				MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	private static bool HasDroppableFolderSource(DragEventArgs e)
	{
		if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
			return false;

		return paths.Any(path => Directory.Exists(path) || ArchiveImportService.IsArchiveFile(path));
	}

	private void ImportFolder(string folder, bool recursive = false, string? displayName = null)
	{
		if (!Directory.Exists(folder))
			return;

		var folderName = displayName ?? Path.GetFileName(folder);
		if (!string.IsNullOrWhiteSpace(folderName) && string.IsNullOrWhiteSpace(_draftId))
			NameBox.Text = folderName;

		var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
		var cursorFiles = Directory.EnumerateFiles(folder, "*.*", searchOption)
			.Where(IsCursorFile)
			.ToList();

		if (cursorFiles.Count == 0)
		{
			MessageBox.Show(Loc.Get(LocEditorNoCursorInFolder), Title,
				MessageBoxButton.OK, MessageBoxImage.Information);
			return;
		}

		var matched = 0;
		foreach (var file in cursorFiles)
		{
			var role = CursorRoles.MatchByFileName(file);
			if (role == null)
				continue;

			var slot = _slots.First(slot => slot.Role.RegistryName == role.RegistryName);
			matched++;

			if (slot.IsLocked)
				continue;

			SetSlotSource(slot, file);
		}

		if (matched == 0)
		{
			MessageBox.Show(Loc.Format(LocEditorNoMatchInFolder, cursorFiles.Count), Title,
				MessageBoxButton.OK, MessageBoxImage.Information);
		}
	}

	private static bool IsCursorFile(string path)
	{
		var extension = Path.GetExtension(path).ToLowerInvariant();

		return extension is CurExtension or AniExtension;
	}

	private void OnSaveButtonClick(object sender, RoutedEventArgs e)
	{
		if (_slots.All(slot => slot.SourcePath == null && slot.RefPresetId == null))
		{
			MessageBox.Show(Loc.Get(LocEditorNoFiles), Title,
				MessageBoxButton.OK, MessageBoxImage.Information);
			return;
		}

		var draft = new PresetDraft { Id = _draftId, Name = NameBox.Text, BaseSize = _baseSize };
		foreach (var slot in _slots.Where(slot => slot.SourcePath != null || slot.RefPresetId != null))
		{
			draft.RoleSources[slot.Role.RegistryName] = slot.RefPresetId != null
				? new RoleSourceDraft { Ref = new RoleRef { PresetId = slot.RefPresetId, FileName = slot.RefFileName! } }
				: new RoleSourceDraft { OwnFilePath = slot.SourcePath };
		}

		foreach (var slot in _slots.Where(slot => slot.IsLocked))
			draft.LockedRoles.Add(slot.Role.RegistryName);

		Result = draft;
		DialogResult = true;
	}

	private static string? GetSingleCursorFile(DragEventArgs e)
	{
		if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
			return null;

		var file = paths.FirstOrDefault(File.Exists);

		if (file == null)
			return null;

		var extension = Path.GetExtension(file).ToLowerInvariant();

		return extension is CurExtension or AniExtension ? file : null;
	}
}
