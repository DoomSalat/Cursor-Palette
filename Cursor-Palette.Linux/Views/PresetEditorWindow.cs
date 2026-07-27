using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Ellipse = Avalonia.Controls.Shapes.Ellipse;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CursorPalette.Linux.Services;
using CursorPalette.Models;
using CursorPalette.Services;

namespace CursorPalette.Linux.Views;

public class PresetEditorWindow : Window
{
	private const string PixelSuffix = "px";
	private const double SlotWidth = 160;
	private const double SlotHeight = 180;
	private const double SlotMargin = 6;
	private const double SlotCornerRadius = 10;
	private const double SlotPreviewSize = 40;
	private const double HotspotDotSize = 8;
	private const double DialogMargin = 16;
	private const double DialogSpacing = 12;
	private const double ButtonSpacing = 8;
	private const double WindowMinWidth = 480;
	private const double WindowMinHeight = 480;
	private const double DropZoneCornerRadius = 10;
	private const double DropZoneBorderThickness = 2;
	private const double DropZoneMargin = 16;
	private const double SlotHintMargin = 16;

	private const string LocEditorTitleNew = "S.Editor.TitleNew";
	private const string LocEditorTitleEdit = "S.Editor.TitleEdit";
	private const string LocEditorSave = "S.Editor.Save";
	private const string LocEditorCancel = "S.Editor.Cancel";
	private const string LocEditorBrowse = "S.Editor.Browse";
	private const string LocEditorBrowseFolder = "S.Editor.BrowseFolder";
	private const string LocEditorEmptySlot = "S.Editor.EmptySlot";
	private const string LocEditorPlaceholderBadge = "S.Editor.PlaceholderBadge";
	private const string LocEditorPresetName = "S.Editor.PresetName";
	private const string LocEditorNoFiles = "S.Editor.NoFiles";
	private const string LocEditorSlotHint = "S.Editor.SlotHint";
	private const string LocEditorNoCursorInFolder = "S.Editor.NoCursorInFolder";
	private const string LocEditorNoMatchInFolder = "S.Editor.NoMatchInFolder";
	private const string LocEditorEmptySkipped = "S.Editor.EmptySkipped";
	private const string LocDefaultPresetName = "S.DefaultPresetName";
	private const string LocCursorSize = "S.CursorSize";
	private const string LocApplySize = "S.ApplySize";
	private const string LocScaleCursors = "S.ScaleCursors";
	private const string LocToastSizeApplied = "S.Toast.SizeApplied";
	private const string LocEditorPickExisting = "S.Editor.PickExisting";
	private const string LocEditorPivotTooltip = "S.Editor.Pivot.Tooltip";
	private const string LocEditorPivotDisabledTooltip = "S.Editor.Pivot.Disabled.Tooltip";
	private const string LocEditorLinkedRoleTooltip = "S.Editor.LinkedRole.Tooltip";
	private const string LocEditorEmptyCursorWarning = "S.Editor.EmptyCursorWarning";
	private const string LocToastDownloaded = "S.Toast.Downloaded";
	private const string LocToastPresetDownloaded = "S.Toast.PresetDownloaded";
	private const string LocToastReadmeDownloaded = "S.Toast.ReadmeDownloaded";
	private const string LocEditorDownloadPresetTooltip = "S.Editor.DownloadPreset.Tooltip";
	private const string LocExportMoreOptionsTooltip = "S.Export.MoreOptions.Tooltip";
	private const string LocExportAsXcursorTheme = "S.Export.AsXcursorTheme";
	private const string LocExportAsLinuxArchive = "S.Export.AsLinuxArchive";
	private const string LocDownloadReadme = "S.Export.DownloadReadme";
	private const string LocToastExportedXcursorTheme = "S.Toast.ExportedXcursorTheme";
	private const string LocToastExportedLinuxArchive = "S.Toast.ExportedLinuxArchive";
	private const string LocErrorArchiveExtractFailed = "S.Error.ArchiveExtractFailed";
	private const string LocEditorLockTooltip = "S.Editor.Lock.Tooltip";
	private const string LocEditorUnlockTooltip = "S.Editor.Unlock.Tooltip";
	private const string LocErrorSaveFailed = "S.Error.SaveFailed";
	private const string LocEditorAuthor = "S.Editor.Author";
	private const string LocEditorAuthorTooltip = "S.Editor.Author.Tooltip";
	private const string LocExportAsFullPackage = "S.Export.AsFullPackage";
	private const string LocToastExportedFullPackage = "S.Toast.ExportedFullPackage";

	private const string CursorFileFilterName = "Cursors";
	private const string EmptyValue = "";
	private const string PaintButtonText = "Paint";
	private const string PickExistingButtonContent = "🧩";
	private const string LinkIconFile = "LinkIcon32.png";
	private const string LockIconFile = "LockIcon26.png";
	private const string DownloadIconFile = "DownloadIcon32.png";
	private const string StairIconFile = "StairIcon24.png";
	private const string ExpandIconFile = "ExpandIcon32.png";
	private const double LinkBadgeIconSize = 16;
	private const double LockIconSize = 14;
	private const double DownloadIconSize = 16;
	private const double IconButtonSize = 28;
	private const string PaintTempDirName = "cursor-palette-paint";
	private const string CurExtension = ".cur";
	private const string PaintFileNameFormat = "{0}_{1:yyyyMMddHHmmss}.cur";
	private const string ConvertTempFilePrefix = "cursor-palette-convert-";
	private const string AllFilesPattern = "*.*";

	private static readonly HashSet<string> ConvertibleExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".cur", ".ani", ".png", ".jpg", ".jpeg", ".bmp", ".gif"
	};

	private const double WindowDefaultWidth = 760;
	private const double WindowDefaultHeight = 640;
	private const int NameMaxLength = 60;
	private const double SliderMinWidth = 90;
	private const double SizeValueTextWidth = 46;
	private const double SaveButtonMinWidth = 110;
	private const double CancelButtonMinWidth = 90;
	private const double DownloadButtonSize = 32;
	private const double DownloadMoreButtonWidth = 20;
	private const double ScaleModeButtonSize = 28;
	private const double ScaleModeButtonCornerRadius = 6;
	private const double SlotHintFontSize = 12;
	private const double RoleNameFontSize = 12;
	private const double FileTextFontSize = 11;
	private const double BrowseButtonFontSize = 11;
	private const double PivotButtonFontSize = 14;
	private const double ClearButtonSize = 22;
	private const double SlotIconButtonSize = 22;
	private const double HotspotDotStrokeThickness = 1.5;
	private const double DropIndicatorBorderThickness = 3;
	private const double SlotBorderThickness = 2;
	private const double PlaceholderBadgeCornerRadius = 4;
	private const string PivotButtonContent = "🎯";
	private const string ClearButtonContent = "✕";
	private const string DownloadMoreButtonContent = "▾";
	private const string AniExtension = ".ani";
	private const string GifExtension = ".gif";
	private const string HotspotTempFilePrefix = "cursor-palette-hotspot-";
	private const string PositionTempFilePrefix = "cursor-palette-position-";
	private const string XcursorTempFilePrefix = "cursor-palette-editor-xcursor-";
	private const double DefaultDpi = 96.0;
	private const int MaxCursorDimension = 256;
	private static readonly string[] CursorFilePickerPatterns = { "*.cur", "*.ani", "*.png", "*.jpg", "*.bmp", "*.gif" };

	private readonly List<Slot> _slots = new();
	private readonly string? _draftId;
	private int _baseSize;
	private bool _useScaling;
	private ScaleMode _scaleMode = ScaleMode.AreaWeighted;
	private Panel _rootPanel = null!;
	private string? _draftAuthor;

	private readonly TextBox _nameBox;
	private readonly TextBlock _authorDisplayText;
	private readonly Panel _authorDisplayPanel;
	private readonly TextBox _authorEditBox;

	public PresetDraft? Result { get; private set; }

	private readonly Slider _sizeSlider;
	private readonly TextBlock _sizeValueText;
	private readonly ItemsControl _slotsControl;

	public PresetEditorWindow(Preset? existing, IReadOnlyList<string> droppedFiles, string? suggestedName = null)
	{
		Title = Loc.Get(existing == null ? LocEditorTitleNew : LocEditorTitleEdit);
		Width = WindowDefaultWidth;
		Height = WindowDefaultHeight;
		MinWidth = WindowMinWidth;
		MinHeight = WindowMinHeight;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;

		_nameBox = new TextBox
		{
			Text = existing?.Name
				?? (string.IsNullOrWhiteSpace(suggestedName) ? null : suggestedName)
				?? Loc.Get(LocDefaultPresetName),
			MaxLength = NameMaxLength,
			Watermark = Loc.Get(LocEditorPresetName),
		};

		_draftAuthor = existing?.Author;

		_authorDisplayText = new TextBlock
		{
			Text = _draftAuthor ?? "",
			Foreground = Brushes.Gray,
			VerticalAlignment = VerticalAlignment.Center,
		};

		var authorEditButton = new Button
		{
			Content = "✏",
			FontSize = 10,
			Padding = new Avalonia.Thickness(2),
			Margin = new Avalonia.Thickness(4, 0, 0, 0),
			VerticalAlignment = VerticalAlignment.Center,
			Background = Brushes.Transparent,
			BorderThickness = new Avalonia.Thickness(0),
			Cursor = new Avalonia.Input.Cursor(StandardCursorType.Hand),
		};
		ToolTip.SetTip(authorEditButton, Loc.Get(LocEditorAuthorTooltip));

		_authorDisplayPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
		};
		_authorDisplayPanel.Children.Add(_authorDisplayText);
		_authorDisplayPanel.Children.Add(authorEditButton);

		_authorEditBox = new TextBox
		{
			Text = _draftAuthor ?? "",
			MaxLength = NameMaxLength,
			MinWidth = 100,
			IsVisible = false,
			Watermark = Loc.Get(LocEditorAuthor),
		};
		ToolTip.SetTip(_authorEditBox, Loc.Get(LocEditorAuthorTooltip));

		authorEditButton.Click += (_, _) =>
		{
			_authorEditBox.Text = _authorDisplayText.Text;
			_authorDisplayPanel.IsVisible = false;
			_authorEditBox.IsVisible = true;
			_authorEditBox.Focus();
		};

		_authorEditBox.KeyDown += (_, e) =>
		{
			if (e.Key == Key.Enter)
			{
				CommitAuthorEdit();
				e.Handled = true;
			}
			else if (e.Key == Key.Escape)
			{
				_authorEditBox.IsVisible = false;
				_authorDisplayPanel.IsVisible = true;
				e.Handled = true;
			}
		};

		_authorEditBox.LostFocus += (_, _) =>
		{
			if (_authorEditBox.IsVisible)
				CommitAuthorEdit();
		};

		_baseSize = existing?.BaseSize ?? AppState.GetDefaultBaseSize();
		_useScaling = existing?.UseScaling ?? false;
		_scaleMode = existing?.ScaleMode ?? ScaleMode.AreaWeighted;

		_sizeSlider = new Slider
		{
			Minimum = 1,
			Maximum = 15,
			Value = (_baseSize - CursorConstants.SizeStep) / (double)CursorConstants.SizeStep,
			TickFrequency = 1,
			IsSnapToTickEnabled = true,
			MinWidth = SliderMinWidth,
			Margin = new Avalonia.Thickness(8, 0),
		};

		_sizeValueText = new TextBlock
		{
			Width = SizeValueTextWidth,
			VerticalAlignment = VerticalAlignment.Center,
			Text = $"{_baseSize} {PixelSuffix}",
		};

		_sizeSlider.PropertyChanged += (_, e) =>
		{
			if (e.Property == Slider.ValueProperty)
			{
				var sizePx = CursorConstants.SizeStep + (int)_sizeSlider.Value * CursorConstants.SizeStep;
				_sizeValueText.Text = $"{sizePx} {PixelSuffix}";
				_baseSize = sizePx;
			}
		};

		_slotsControl = new ItemsControl();

		var saveButton = new Button
		{
			Content = Loc.Get(LocEditorSave),
			MinWidth = SaveButtonMinWidth,
		};

		var cancelButton = new Button
		{
			Content = Loc.Get(LocEditorCancel),
			MinWidth = CancelButtonMinWidth,
			Margin = new Avalonia.Thickness(12, 0, 8, 0),
		};

		saveButton.Click += OnSaveClick;
		cancelButton.Click += (_, _) => Close();

		var downloadPresetButton = new Button
		{
			Content = IconHelper.CreateIcon(DownloadIconFile, 16, Brushes.White),
			Width = DownloadButtonSize,
			Height = DownloadButtonSize,
			Padding = new Avalonia.Thickness(0),
			Margin = new Avalonia.Thickness(0, 0, 2, 0),
		};
		ToolTip.SetTip(downloadPresetButton, Loc.Get(LocEditorDownloadPresetTooltip));

		var downloadMoreButton = new Button
		{
			Content = DownloadMoreButtonContent,
			Width = DownloadMoreButtonWidth,
			Height = DownloadButtonSize,
			Padding = new Avalonia.Thickness(0),
			Margin = new Avalonia.Thickness(0, 0, 8, 0),
		};
		ToolTip.SetTip(downloadMoreButton, Loc.Get(LocExportMoreOptionsTooltip));

		downloadPresetButton.Click += OnDownloadPresetClick;
		downloadMoreButton.Click += OnDownloadPresetMoreClick;

		var authorBar = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
			Margin = new Avalonia.Thickness(DialogMargin, 0, DialogMargin, 0),
		};
		var authorLabel = new TextBlock
		{
			Text = Loc.Get(LocEditorAuthor) + ":",
			Foreground = Brushes.Gray,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Avalonia.Thickness(0, 0, 8, 0),
		};
		Grid.SetColumn(authorLabel, 0);
		authorBar.Children.Add(authorLabel);
		Grid.SetColumn(_authorDisplayPanel, 1);
		authorBar.Children.Add(_authorDisplayPanel);
		Grid.SetColumn(_authorEditBox, 1);
		authorBar.Children.Add(_authorEditBox);

		var bottomBar = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto,Auto,Auto,Auto"),
			Margin = new Avalonia.Thickness(DialogMargin),
		};
		var nameLabel = new TextBlock
		{
			Text = Loc.Get(LocEditorPresetName),
			Foreground = Brushes.Gray,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Avalonia.Thickness(0, 0, 8, 0),
		};
		Grid.SetColumn(nameLabel, 1);
		bottomBar.Children.Add(nameLabel);
		Grid.SetColumn(_nameBox, 2);
		bottomBar.Children.Add(_nameBox);
		Grid.SetColumn(downloadPresetButton, 3);
		bottomBar.Children.Add(downloadPresetButton);
		Grid.SetColumn(downloadMoreButton, 4);
		bottomBar.Children.Add(downloadMoreButton);
		Grid.SetColumn(cancelButton, 5);
		bottomBar.Children.Add(cancelButton);
		Grid.SetColumn(saveButton, 6);
		bottomBar.Children.Add(saveButton);

		var applySizeButton = new Button
		{
			Content = Loc.Get(LocApplySize),
			Margin = new Avalonia.Thickness(8, 0, 0, 0),
		};
		applySizeButton.Click += OnApplySizeClick;

		var useScalingCheckBox = new CheckBox
		{
			Content = Loc.Get(LocScaleCursors),
			IsChecked = _useScaling,
			Margin = new Avalonia.Thickness(8, 0, 0, 0),
			VerticalAlignment = VerticalAlignment.Center,
		};
		useScalingCheckBox.IsCheckedChanged += (_, _) =>
			_useScaling = useScalingCheckBox.IsChecked == true;

		var scaleModeIcon = IconHelper.CreateIcon(
			_scaleMode == ScaleMode.NearestNeighbor ? StairIconFile : ExpandIconFile,
			16, Brushes.White);
		scaleModeIcon.HorizontalAlignment = HorizontalAlignment.Center;
		scaleModeIcon.VerticalAlignment = VerticalAlignment.Center;

		var scaleModeButton = new Border
		{
			Width = ScaleModeButtonSize,
			Height = ScaleModeButtonSize,
			CornerRadius = new Avalonia.CornerRadius(ScaleModeButtonCornerRadius),
			Background = new SolidColorBrush(0x00000000),
			BorderBrush = Brushes.Gray,
			BorderThickness = new Avalonia.Thickness(1),
			Margin = new Avalonia.Thickness(4, 0, 0, 0),
			Cursor = new Avalonia.Input.Cursor(StandardCursorType.Hand),
			Child = scaleModeIcon,
		};
		scaleModeButton.PointerPressed += (_, _) =>
		{
			_scaleMode = _scaleMode == ScaleMode.NearestNeighbor
				? ScaleMode.AreaWeighted
				: ScaleMode.NearestNeighbor;
			scaleModeIcon.OpacityMask = new ImageBrush { Source = IconHelper.Load(_scaleMode == ScaleMode.NearestNeighbor ? StairIconFile : ExpandIconFile) };
		};

		var sizeBar = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto,Auto"),
			Margin = new Avalonia.Thickness(DialogMargin),
		};
		sizeBar.Children.Add(new TextBlock
		{
			Text = Loc.Get(LocCursorSize),
			Foreground = Brushes.Gray,
			VerticalAlignment = VerticalAlignment.Center,
		});
		Grid.SetColumn(_sizeSlider, 1);
		sizeBar.Children.Add(_sizeSlider);
		Grid.SetColumn(_sizeValueText, 2);
		sizeBar.Children.Add(_sizeValueText);
		Grid.SetColumn(applySizeButton, 3);
		sizeBar.Children.Add(applySizeButton);
		Grid.SetColumn(useScalingCheckBox, 4);
		sizeBar.Children.Add(useScalingCheckBox);
		Grid.SetColumn(scaleModeButton, 5);
		sizeBar.Children.Add(scaleModeButton);

		var scrollViewer = new ScrollViewer
		{
			Content = _slotsControl,
			Padding = new Avalonia.Thickness(10, 4),
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
		};

		var slotHint = new TextBlock
		{
			Text = Loc.Get(LocEditorSlotHint),
			Foreground = Brushes.Gray,
			FontSize = 12,
			Margin = new Avalonia.Thickness(SlotHintMargin, 12, SlotHintMargin, 4),
		};

		var browseFolderButton = new Button
		{
			Content = Loc.Get(LocEditorBrowseFolder),
			Padding = new Avalonia.Thickness(16, 10),
			HorizontalContentAlignment = HorizontalAlignment.Center,
		};
		browseFolderButton.Click += async (_, _) => await BrowseFolder();

		var dropZone = new Border
		{
			BorderBrush = Brushes.Gray,
			BorderThickness = new Avalonia.Thickness(DropZoneBorderThickness),
			CornerRadius = new Avalonia.CornerRadius(DropZoneCornerRadius),
			Margin = new Avalonia.Thickness(DropZoneMargin, 4, DropZoneMargin, 8),
			Padding = new Avalonia.Thickness(16),
			Child = browseFolderButton,
		};
		DragDrop.SetAllowDrop(dropZone, true);
		dropZone.AddHandler(DragDrop.DragOverEvent, OnDropZoneDragOver);
		dropZone.AddHandler(DragDrop.DropEvent, OnDropZoneDrop);

		var rootPanel = new Grid();
		rootPanel.RowDefinitions.Add(new RowDefinition(0, GridUnitType.Auto));
		rootPanel.RowDefinitions.Add(new RowDefinition(0, GridUnitType.Auto));
		rootPanel.RowDefinitions.Add(new RowDefinition(0, GridUnitType.Auto));
		rootPanel.RowDefinitions.Add(new RowDefinition(0, GridUnitType.Auto));
		rootPanel.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
		rootPanel.RowDefinitions.Add(new RowDefinition(0, GridUnitType.Auto));

		Grid.SetRow(sizeBar, 0);
		Grid.SetRow(slotHint, 1);
		Grid.SetRow(dropZone, 2);
		Grid.SetRow(authorBar, 3);
		Grid.SetRow(scrollViewer, 4);
		Grid.SetRow(bottomBar, 5);
		rootPanel.Children.Add(sizeBar);
		rootPanel.Children.Add(slotHint);
		rootPanel.Children.Add(dropZone);
		rootPanel.Children.Add(authorBar);
		rootPanel.Children.Add(scrollViewer);
		rootPanel.Children.Add(bottomBar);

		_rootPanel = rootPanel;
		Content = rootPanel;

		var uiScale = AppState.GetUiScale();
		if (uiScale != 1.0)
		{
			rootPanel.RenderTransform = new ScaleTransform(uiScale, uiScale);
			rootPanel.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
		}

		_draftId = existing?.Id;

		BuildSlots(existing, droppedFiles);

		AddHandler(DragDrop.DragEnterEvent, OnPresetWindowDragEnter);
		AddHandler(DragDrop.DragLeaveEvent, OnPresetWindowDragLeave);
		AddHandler(DragDrop.DropEvent, OnPresetWindowDrop);
	}

	private void CommitAuthorEdit()
	{
		_authorDisplayText.Text = _authorEditBox.Text;
		_draftAuthor = string.IsNullOrWhiteSpace(_authorEditBox.Text) ? null : _authorEditBox.Text.Trim();
		_authorEditBox.IsVisible = false;
		_authorDisplayPanel.IsVisible = true;
	}

	private string? GetAuthor() => string.IsNullOrWhiteSpace(_authorDisplayText.Text) ? null : _authorDisplayText.Text.Trim();

	private void OnPresetWindowDragEnter(object? sender, DragEventArgs e)
	{
		if (!e.Data.Contains(DataFormats.Files))
			return;

		var files = e.Data.GetFiles()?.Select(f => f.Path.LocalPath).ToArray();
		var hasFolder = files?.Any(Directory.Exists) == true;

		if (hasFolder)
		{
			HideAllDropIndicators();
		}
		else
		{
			ShowSlotDropIndicators();
		}
	}

	private void OnPresetWindowDragLeave(object? sender, DragEventArgs e)
	{
		HideAllDropIndicators();
	}

	private void OnPresetWindowDrop(object? sender, DragEventArgs e)
	{
		HideAllDropIndicators();
	}

	private void ShowSlotDropIndicators()
	{
		foreach (var slot in _slots)
			slot.DropIndicator.IsVisible = !slot.IsLocked;
	}

	private sealed class Slot
	{
		public required CursorRoleInfo Role { get; init; }
		public string? SourcePath { get; set; }
		public string? RefPresetId { get; set; }
		public string? RefFileName { get; set; }
		public bool IsLocked { get; set; }
		public required Image PreviewImage { get; init; }
		public required Canvas PreviewHost { get; init; }
		public required Ellipse HotspotDot { get; init; }
		public required TextBlock FileText { get; init; }
		public required Button BrowseButton { get; init; }
		public required Button PivotButton { get; init; }
		public required Button PaintButton { get; init; }
		public required Button PickExistingButton { get; init; }
		public required Button ClearButton { get; init; }
		public required Border Container { get; init; }
		public required Control LinkBadge { get; init; }
		public required Border PlaceholderBadge { get; init; }
		public required Button DownloadButton { get; init; }
		public required Button LockButton { get; init; }
		public required Border DropIndicator { get; init; }
	}

	private void BuildSlots(Preset? existing, IReadOnlyList<string> droppedFiles)
	{
		foreach (var role in CursorRoles.All)
		{
			var slot = CreateSlot(role);
			_slots.Add(slot);

			if (existing != null)
			{
				if (existing.RoleRefs.TryGetValue(role.RegistryName, out var roleRef))
				{
					SetSlotReference(slot, roleRef.PresetId, roleRef.FileName);
				}
				else
				{
					var path = PresetStore.GetRoleFilePath(existing, role.RegistryName);
					if (path != null && File.Exists(path))
						SetSlotSource(slot, path);
				}

				if (existing.LockedRoles.Contains(role.RegistryName))
					SetSlotLocked(slot, true);
			}

			_slotsControl.Items.Add(slot.Container);
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

	private Slot CreateSlot(CursorRoleInfo role)
	{
		var preview = new Image
		{
			Width = SlotPreviewSize,
			Height = SlotPreviewSize,
		};

		var hotspotDot = new Ellipse
		{
			Width = HotspotDotSize,
			Height = HotspotDotSize,
			Fill = Brushes.CornflowerBlue,
			Stroke = Brushes.White,
			StrokeThickness = HotspotDotStrokeThickness,
			IsVisible = false,
		};

		var previewHost = new Canvas
		{
			Width = SlotPreviewSize,
			Height = SlotPreviewSize,
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		previewHost.Children.Add(preview);
		previewHost.Children.Add(hotspotDot);

		var roleName = new TextBlock
		{
			Text = Loc.Get("S." + role.DisplayKey),
			FontWeight = FontWeight.SemiBold,
			TextAlignment = TextAlignment.Center,
			TextWrapping = TextWrapping.Wrap,
			FontSize = RoleNameFontSize,
			Margin = new Avalonia.Thickness(4, 6, 4, 0),
		};

		var fileText = new TextBlock
		{
			Text = Loc.Get(LocEditorEmptySlot),
			Foreground = Brushes.Gray,
			FontSize = FileTextFontSize,
			TextAlignment = TextAlignment.Center,
			TextTrimming = TextTrimming.CharacterEllipsis,
			Margin = new Avalonia.Thickness(4, 2, 4, 0),
			MaxWidth = SlotWidth - 20,
		};

		var browseButton = new Button
		{
			Content = Loc.Get(LocEditorBrowse),
			FontSize = BrowseButtonFontSize,
			Padding = new Avalonia.Thickness(6, 3),
			Margin = new Avalonia.Thickness(0, 6, 0, 0),
			HorizontalAlignment = HorizontalAlignment.Center,
		};

		var pivotButton = new Button
		{
			Content = PivotButtonContent,
			FontSize = PivotButtonFontSize,
			Width = IconButtonSize,
			Height = IconButtonSize,
			Padding = new Avalonia.Thickness(0),
			Margin = new Avalonia.Thickness(0, 4, 0, 0),
			HorizontalAlignment = HorizontalAlignment.Center,
			IsVisible = false,
		};

		var paintButton = new Button
		{
			Content = PaintButtonText,
			FontSize = BrowseButtonFontSize,
			Padding = new Avalonia.Thickness(6, 3),
			Margin = new Avalonia.Thickness(0, 4, 0, 0),
			HorizontalAlignment = HorizontalAlignment.Center,
		};

		var pickExistingButton = new Button
		{
			Content = PickExistingButtonContent,
			FontSize = BrowseButtonFontSize,
			Width = IconButtonSize,
			Height = IconButtonSize,
			Padding = new Avalonia.Thickness(0),
			Margin = new Avalonia.Thickness(0, 4, 0, 0),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		ToolTip.SetTip(pickExistingButton, Loc.Get(LocEditorPickExisting));

		var clearButton = new Button
		{
			Content = ClearButtonContent,
			FontSize = BrowseButtonFontSize,
			Width = ClearButtonSize,
			Height = ClearButtonSize,
			Padding = new Avalonia.Thickness(0),
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Avalonia.Thickness(0, 6, 6, 0),
			IsVisible = false,
		};

		var panel = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		panel.Children.Add(previewHost);
		panel.Children.Add(roleName);
		panel.Children.Add(fileText);
		panel.Children.Add(browseButton);
		panel.Children.Add(pivotButton);
		panel.Children.Add(paintButton);
		panel.Children.Add(pickExistingButton);

		var downloadButton = new Button
		{
			Content = IconHelper.CreateIcon(DownloadIconFile, DownloadIconSize, Brushes.White),
			Width = SlotIconButtonSize,
			Height = SlotIconButtonSize,
			Padding = new Avalonia.Thickness(0),
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Bottom,
			Margin = new Avalonia.Thickness(0, 0, 6, 6),
			IsVisible = false,
		};

		var lockButton = new Button
		{
			Content = IconHelper.CreateIcon(LockIconFile, LockIconSize, Brushes.White),
			Width = SlotIconButtonSize,
			Height = SlotIconButtonSize,
			Padding = new Avalonia.Thickness(0),
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Avalonia.Thickness(6, 6, 0, 0),
		};
		ToolTip.SetTip(lockButton, Loc.Get(LocEditorLockTooltip));

		var slotContent = new Grid();
		slotContent.Children.Add(panel);
		slotContent.Children.Add(clearButton);
		slotContent.Children.Add(downloadButton);
		slotContent.Children.Add(lockButton);

		var dropIndicator = new Border
		{
			BorderBrush = Brushes.CornflowerBlue,
			BorderThickness = new Avalonia.Thickness(DropIndicatorBorderThickness),
			CornerRadius = new Avalonia.CornerRadius(SlotCornerRadius),
			IsVisible = false,
			IsHitTestVisible = false,
		};
		slotContent.Children.Add(dropIndicator);

		var border = new Border
		{
			Width = SlotWidth,
			Height = SlotHeight,
			Margin = new Avalonia.Thickness(SlotMargin),
			CornerRadius = new Avalonia.CornerRadius(SlotCornerRadius),
			BorderThickness = new Avalonia.Thickness(SlotBorderThickness),
			BorderBrush = Brushes.DarkGray,
			Background = Brushes.Transparent,
			Child = slotContent,
		};

		var linkBadge = IconHelper.CreateIcon(LinkIconFile, LinkBadgeIconSize, Brushes.White);
		linkBadge.HorizontalAlignment = HorizontalAlignment.Center;
		linkBadge.Margin = new Avalonia.Thickness(0, 0, 0, 4);
		linkBadge.IsVisible = false;

		var placeholderBadge = new Border
		{
			Background = Brushes.DarkGray,
			CornerRadius = new Avalonia.CornerRadius(PlaceholderBadgeCornerRadius),
			Padding = new Avalonia.Thickness(8, 2),
			Margin = new Avalonia.Thickness(0, 0, 0, 4),
			HorizontalAlignment = HorizontalAlignment.Center,
			Child = new TextBlock
			{
				Text = Loc.Get(LocEditorPlaceholderBadge),
				FontSize = FileTextFontSize,
			},
		};

		panel.Children.Insert(0, linkBadge);
		panel.Children.Insert(0, placeholderBadge);

		var slot = new Slot
		{
			Role = role,
			PreviewImage = preview,
			PreviewHost = previewHost,
			HotspotDot = hotspotDot,
			FileText = fileText,
			BrowseButton = browseButton,
			PivotButton = pivotButton,
			PaintButton = paintButton,
			PickExistingButton = pickExistingButton,
			ClearButton = clearButton,
			Container = border,
			LinkBadge = linkBadge,
			PlaceholderBadge = placeholderBadge,
			DownloadButton = downloadButton,
			LockButton = lockButton,
			DropIndicator = dropIndicator,
		};

		browseButton.Click += async (_, _) => await BrowseForSlot(slot);
		pivotButton.Click += async (_, _) => await OpenHotspotEditor(slot);
		paintButton.Click += (_, _) => OpenPaintEditor(slot);
		pickExistingButton.Click += (_, _) => PickExistingForSlot(slot);
		clearButton.Click += (_, _) => ClearSlot(slot);
		downloadButton.Click += (_, _) => DownloadSlot(slot);
		lockButton.Click += (_, _) => SetSlotLocked(slot, !slot.IsLocked);

		border.AddHandler(DragDrop.DragOverEvent, OnSlotDragOver);
		border.AddHandler(DragDrop.DropEvent, OnSlotDrop);
		border.Tag = slot;

		return slot;
	}

	private async Task OpenHotspotEditor(Slot slot)
	{
		var resolvedPath = GetSlotResolvedPath(slot);
		if (resolvedPath == null)
			return;

		var hotspot = CursorHotspotService.Read(resolvedPath);
		if (hotspot == null)
			return;

		var editor = new HotspotEditorWindow(resolvedPath, hotspot);
		await editor.ShowDialog(this);

		if (editor.ResultX == hotspot.X && editor.ResultY == hotspot.Y)
			return;

		var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
			$"cursor-palette-hotspot-{Guid.NewGuid():N}{System.IO.Path.GetExtension(resolvedPath)}");

		try
		{
			CursorHotspotService.WriteWithHotspot(resolvedPath, tempPath, editor.ResultX, editor.ResultY);
			CursorPreviewService.Invalidate(tempPath);
			SetSlotSource(slot, tempPath);
		}
		catch
		{
		}
	}

	private const int AniOpenFrameLimit = 60;

	private void OpenPaintEditor(Slot slot)
	{
		var resolvedPath = GetSlotResolvedPath(slot);

		PaintEditorWindow editor;

		if (resolvedPath != null && string.Equals(Path.GetExtension(resolvedPath), AniExtension, StringComparison.OrdinalIgnoreCase))
		{
			var seed = ReadAniAsFrames(resolvedPath);
			if (seed == null)
				return;

			var existingIconImages = ReadAniIconImages(resolvedPath);

			editor = new PaintEditorWindow(seed.Value.Frames, seed.Value.DelaysMs, _nameBox.Text, slot.Role.RegistryName, existingIconImages);
		}
		else
		{
			CursorCanvasImage? image;
			List<CursorCanvasImage>? existingIconImages = null;

			if (resolvedPath != null)
			{
				image = CursorCanvasService.TryRead(resolvedPath);
				if (image == null)
					return;

				existingIconImages = CursorCanvasService.TryReadAllImages(resolvedPath);
			}
			else
			{
				image = new CursorCanvasImage(32, 32, 0, 0, new byte[32 * 32 * 4]);
			}

			editor = new PaintEditorWindow(image, _nameBox.Text, slot.Role.RegistryName, existingIconImages);
		}

		editor.ShowDialog(this);

		editor.Closed += (_, _) =>
		{
			if (editor.ResultFrames is { Count: > 1 } resultFrames)
			{
				var tempPath = Path.Combine(Path.GetTempPath(), $"{PositionTempFilePrefix}{Guid.NewGuid():N}{AniExtension}");
				try
				{
					AniCursorWriter.Save(tempPath, resultFrames, editor.ResultFrameDelaysMs!,
						editor.ResultIconSizes, editor.ResultIconSizeCustomImages,
						editor.ResultIconSizeScaleModeOverrides, editor.ResultIconSizesScaleMode);
					CursorPreviewService.Invalidate(tempPath);
					SetSlotSource(slot, tempPath);
				}
				catch (Exception ex)
				{
					ToastService.Show(_rootPanel, Loc.Format(LocErrorSaveFailed, ex.Message));
				}
			}
			else if (editor.Result != null)
			{
				var tempDir = Path.Combine(Path.GetTempPath(), PaintTempDirName);
				Directory.CreateDirectory(tempDir);
				var fileName = string.Format(PaintFileNameFormat, slot.Role.RegistryName, DateTime.Now);
				var tempPath = Path.Combine(tempDir, fileName);

				try
				{
					if (editor.ResultIconSizes is { Count: > 1 } iconSizes)
					{
						var overrides = editor.ResultIconSizeScaleModeOverrides;
						var customImages = editor.ResultIconSizeCustomImages;

						var images = iconSizes
							.Select(size =>
							{
								if (customImages != null && customImages.TryGetValue(size, out var custom))
									return custom;

								return size == editor.Result.Width
									? editor.Result
									: CursorScalerService.ScaleImage(editor.Result, size, size,
											overrides != null && overrides.TryGetValue(size, out var mode) ? mode : editor.ResultIconSizesScaleMode);
							})
							.ToList();

						CursorCanvasService.WriteMultiSize(tempPath, images);
					}
					else
					{
						CursorCanvasService.Write(tempPath, editor.Result);
					}

					SetSlotSource(slot, tempPath);
				}
				catch (Exception ex)
				{
					ToastService.Show(_rootPanel, Loc.Format(LocErrorSaveFailed, ex.Message));
				}
			}
		};
	}

	private static (List<CursorCanvasImage> Frames, List<int> DelaysMs)? ReadAniAsFrames(string path)
	{
		var animated = AniCursorReader.Read(path);
		if (animated == null || animated.Frames.Count == 0 || animated.StepFrameIndices.Count == 0)
			return null;

		var hotspot = CursorHotspotService.Read(path);
		var hotspotX = hotspot?.X ?? 0;
		var hotspotY = hotspot?.Y ?? 0;

		var frames = new List<CursorCanvasImage>();
		var delays = new List<int>();
		var stepCount = Math.Min(animated.StepFrameIndices.Count, AniOpenFrameLimit);

		for (var i = 0; i < stepCount; i++)
		{
			var frameIndex = Math.Clamp(animated.StepFrameIndices[i], 0, animated.Frames.Count - 1);
			frames.Add(animated.Frames[frameIndex]);
			delays.Add((int)animated.StepDurations[i].TotalMilliseconds);
		}

		return (frames, delays);
	}

	private static List<CursorCanvasImage>? ReadAniIconImages(string path)
	{
		try
		{
			var bytes = File.ReadAllBytes(path);
			var chunks = AniCursorReader.FindIconChunkRanges(bytes);

			if (chunks.Count == 0)
				return null;

			var (offset, length) = chunks[0];
			var frameBytes = new byte[length];
			Array.Copy(bytes, offset, frameBytes, 0, length);

			return CursorCanvasService.TryReadAllImagesFromBytes(frameBytes);
		}
		catch
		{
			return null;
		}
	}

	private void PickExistingForSlot(Slot slot)
	{
		var presets = PresetStore.LoadAll().Where(p => p.Id != _draftId).ToList();
		var presetPicker = new ExistingPresetPickerWindow(presets);
		presetPicker.ShowDialog(this);

		presetPicker.Closed += (_, _) =>
		{
			if (presetPicker.SelectedPreset == null)
				return;

			var rolePicker = new RolePickerWindow(presetPicker.SelectedPreset, slot.Role.RegistryName);
			rolePicker.ShowDialog(this);

			rolePicker.Closed += (_, _) =>
			{
				if (rolePicker.SelectedRole == null)
					return;

				var flatRef = PresetStore.ResolveLeafRef(presetPicker.SelectedPreset, rolePicker.SelectedRole);
				if (flatRef == null)
					return;

				SetSlotReference(slot, flatRef.PresetId, flatRef.FileName);
			};
		};
	}

	private void SetSlotReference(Slot slot, string presetId, string fileName)
	{
		slot.SourcePath = null;
		slot.RefPresetId = presetId;
		slot.RefFileName = fileName;

		var resolvedPath = Path.Combine(PresetStore.GetFilesDir(presetId), fileName);
		var label = BuildReferenceLabel(presetId, fileName);

		try
		{
			if (string.Equals(Path.GetExtension(resolvedPath), AniExtension, StringComparison.OrdinalIgnoreCase))
			{
				AnimatedPreviewManager.TryAttach(slot.PreviewImage, resolvedPath);
			}
			else
			{
				AnimatedPreviewManager.Detach(slot.PreviewImage);
				var preview = CursorPreviewService.GetPreview(resolvedPath);

				if (preview != null)
					slot.PreviewImage.Source = preview;
			}
		}
		catch
		{
		}

		slot.FileText.Text = label;
		slot.FileText.Foreground = Brushes.White;
		slot.ClearButton.IsVisible = true;
		slot.DownloadButton.IsVisible = true;
		slot.PivotButton.IsVisible = true;
		slot.PivotButton.IsEnabled = false;
		ToolTip.SetTip(slot.PivotButton, Loc.Get(LocEditorPivotDisabledTooltip));
		slot.LinkBadge.IsVisible = true;
		slot.PlaceholderBadge.IsVisible = false;
		ToolTip.SetTip(slot.LinkBadge, Loc.Format(LocEditorLinkedRoleTooltip, label));
		UpdateHotspotDot(slot);
	}

	private static string BuildReferenceLabel(string presetId, string fileName)
	{
		var sourceName = PresetStore.LoadAll().FirstOrDefault(p => p.Id == presetId)?.Name;

		return sourceName != null ? $"{sourceName} / {fileName}" : fileName;
	}

	private async Task BrowseForSlot(Slot slot)
	{
		var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
		{
			Title = Loc.Get(LocEditorBrowse),
			AllowMultiple = false,
			FileTypeFilter = new[]
			{
				new FilePickerFileType(CursorFileFilterName)
				{
					Patterns = CursorFilePickerPatterns,
				},
			},
		});

		if (files.Count == 0)
			return;

		var filePath = files[0].Path.LocalPath;
		var cursorPath = ConvertToCursorTempFile(filePath);

		if (cursorPath != null && IsFullyTransparent(cursorPath))
		{
			ToastService.Show(_rootPanel, Loc.Get(LocEditorEmptyCursorWarning));
			return;
		}

		SetSlotSource(slot, cursorPath ?? filePath);
	}

	private void SetSlotSource(Slot slot, string path)
	{
		slot.SourcePath = path;
		slot.RefPresetId = null;
		slot.RefFileName = null;
		slot.FileText.Text = Path.GetFileName(path);
		slot.FileText.Foreground = Brushes.White;
		slot.ClearButton.IsVisible = true;
		slot.DownloadButton.IsVisible = true;
		slot.PivotButton.IsVisible = true;
		slot.PivotButton.IsEnabled = true;
		ToolTip.SetTip(slot.PivotButton, Loc.Get(LocEditorPivotTooltip));
		slot.LinkBadge.IsVisible = false;
		slot.PlaceholderBadge.IsVisible = false;

		try
		{
			if (string.Equals(Path.GetExtension(path), AniExtension, StringComparison.OrdinalIgnoreCase))
			{
				AnimatedPreviewManager.TryAttach(slot.PreviewImage, path);
			}
			else
			{
				AnimatedPreviewManager.Detach(slot.PreviewImage);
				var preview = CursorPreviewService.GetPreview(path);
				if (preview != null)
					slot.PreviewImage.Source = preview;
			}
		}
		catch
		{
		}

		UpdateHotspotDot(slot);
	}

	private void ClearSlot(Slot slot)
	{
		slot.SourcePath = null;
		slot.RefPresetId = null;
		slot.RefFileName = null;
		slot.FileText.Text = Loc.Get(LocEditorEmptySlot);
		slot.FileText.Foreground = Brushes.Gray;
		slot.ClearButton.IsVisible = false;
		slot.DownloadButton.IsVisible = false;
		slot.PivotButton.IsVisible = false;
		AnimatedPreviewManager.Detach(slot.PreviewImage);
		slot.PreviewImage.Source = null;
		slot.LinkBadge.IsVisible = false;
		slot.PlaceholderBadge.IsVisible = true;
		slot.HotspotDot.IsVisible = false;
	}

	private void SetSlotLocked(Slot slot, bool locked)
	{
		slot.IsLocked = locked;

		slot.LockButton.Content = IconHelper.CreateIcon(LockIconFile, LockIconSize, locked ? Brushes.Gold : Brushes.White);
		ToolTip.SetTip(slot.LockButton, Loc.Get(locked ? LocEditorUnlockTooltip : LocEditorLockTooltip));

		slot.BrowseButton.IsEnabled = !locked;
		slot.PaintButton.IsEnabled = !locked;
		slot.PickExistingButton.IsEnabled = !locked;
		slot.ClearButton.IsEnabled = !locked;
	}

	private static string? GetSlotResolvedPath(Slot slot)
	{
		if (slot.SourcePath != null)
			return slot.SourcePath;

		if (slot.RefPresetId != null && slot.RefFileName != null)
			return System.IO.Path.Combine(PresetStore.GetFilesDir(slot.RefPresetId), slot.RefFileName);

		return null;
	}

	private void UpdateHotspotDot(Slot slot)
	{
		var resolvedPath = GetSlotResolvedPath(slot);
		var hotspot = resolvedPath != null ? CursorHotspotService.Read(resolvedPath) : null;

		if (hotspot == null)
		{
			slot.HotspotDot.IsVisible = false;
			return;
		}

		var displayX = (hotspot.X + 0.5) / hotspot.Width * SlotPreviewSize;
		var displayY = (hotspot.Y + 0.5) / hotspot.Height * SlotPreviewSize;

		Canvas.SetLeft(slot.HotspotDot, displayX - HotspotDotSize / 2);
		Canvas.SetTop(slot.HotspotDot, displayY - HotspotDotSize / 2);
		slot.HotspotDot.IsVisible = true;
	}

	private void DownloadSlot(Slot slot)
	{
		var resolvedPath = GetSlotResolvedPath(slot);

		if (resolvedPath == null || !File.Exists(resolvedPath))
			return;

		try
		{
			var downloadsDir = PathProvider.Current.DownloadsDir;
			Directory.CreateDirectory(downloadsDir);

			var baseName = ExportFileNaming.Build(_nameBox.Text, slot.Role.RegistryName, slot.Role.RegistryName);
			var extension = Path.GetExtension(resolvedPath);
			var destPath = Path.Combine(downloadsDir, $"{baseName}{extension}");

			var attempt = 1;
			while (File.Exists(destPath))
				destPath = Path.Combine(downloadsDir, $"{baseName} ({attempt++}){extension}");

			File.Copy(resolvedPath, destPath);
			var now = DateTime.Now;
			File.SetCreationTime(destPath, now);
			File.SetLastWriteTime(destPath, now);

			ToastService.Show(_rootPanel, Loc.Format(LocToastDownloaded, Path.GetFileName(destPath)));

			if (AppState.GetOpenFolderAfterDownload())
				FileExplorerProvider.Current?.RevealFile(destPath);
		}
		catch (Exception ex)
		{
			ToastService.Show(_rootPanel, Loc.Format(LocErrorSaveFailed, ex.Message));
		}
	}

	private void OnDownloadPresetClick(object? sender, RoutedEventArgs e)
	{
		var invalid = Path.GetInvalidPathChars();
		var presetName = string.Join("", (_nameBox.Text ?? "").Where(c => !invalid.Contains(c))).Trim();

		if (string.IsNullOrWhiteSpace(presetName))
			presetName = Loc.Get(LocDefaultPresetName);

		var roleFiles = new Dictionary<string, string>();

		foreach (var slot in _slots)
		{
			var resolvedPath = GetSlotResolvedPath(slot);
			if (resolvedPath != null && File.Exists(resolvedPath))
				roleFiles[slot.Role.RegistryName] = resolvedPath;
		}

		if (roleFiles.Count == 0)
			return;

		try
		{
			var path = PresetPackageService.DownloadPresetAsFolder(presetName, roleFiles, _baseSize, _useScaling, _scaleMode, author: GetAuthor());
			if (path == null)
				return;

			ToastService.Show(_rootPanel, Loc.Format(LocToastPresetDownloaded, presetName, roleFiles.Count));

			if (AppState.GetOpenFolderAfterDownload())
				FileExplorerProvider.Current?.RevealFile(path);
		}
		catch (Exception ex)
		{
			ToastService.Show(_rootPanel, Loc.Format(LocErrorSaveFailed, ex.Message));
		}
	}

	private void OnDownloadPresetMoreClick(object? sender, RoutedEventArgs e)
	{
		var menu = new ContextMenu
		{
			PlacementTarget = sender as Control,
		};

		var fullPackageItem = new MenuItem { Header = Loc.Get(LocExportAsFullPackage) };
		fullPackageItem.Click += (_, _) => ExportPresetFullPackage();
		menu.Items.Add(fullPackageItem);

		var xcursorItem = new MenuItem { Header = Loc.Get(LocExportAsXcursorTheme) };
		xcursorItem.Click += (_, _) => ExportPresetForLinux(asXcursorTheme: true);
		menu.Items.Add(xcursorItem);

		var linuxItem = new MenuItem { Header = Loc.Get(LocExportAsLinuxArchive) };
		linuxItem.Click += (_, _) => ExportPresetForLinux(asXcursorTheme: false);
		menu.Items.Add(linuxItem);

		var readmeItem = new MenuItem { Header = Loc.Get(LocDownloadReadme) };
		readmeItem.Click += (_, _) => DownloadReadme();
		menu.Items.Add(readmeItem);

		menu.Open(sender as Control);
	}

	private void DownloadReadme()
	{
		try
		{
			var path = PresetPackageService.DownloadReadme();
			ToastService.Show(_rootPanel, Loc.Format(LocToastReadmeDownloaded, Path.GetFileName(path)));

			if (AppState.GetOpenFolderAfterDownload())
				FileExplorerProvider.Current?.RevealFile(path);
		}
		catch (Exception ex)
		{
			ToastService.Show(_rootPanel, Loc.Format(LocErrorSaveFailed, ex.Message));
		}
	}

	private void ExportPresetFullPackage()
	{
		var invalid = Path.GetInvalidPathChars();
		var presetName = string.Join("", (_nameBox.Text ?? "").Where(c => !invalid.Contains(c))).Trim();

		if (string.IsNullOrWhiteSpace(presetName))
			presetName = Loc.Get(LocDefaultPresetName);

		var roleFiles = new Dictionary<string, string>();

		foreach (var slot in _slots)
		{
			var resolvedPath = GetSlotResolvedPath(slot);
			if (resolvedPath != null && File.Exists(resolvedPath))
				roleFiles[slot.Role.RegistryName] = resolvedPath;
		}

		if (roleFiles.Count == 0)
			return;

		try
		{
			var path = PresetPackageService.ExportFullPackageForFiles(presetName, roleFiles, _baseSize, _useScaling, _scaleMode, author: GetAuthor());

			if (path == null)
				return;

			ToastService.Show(_rootPanel, Loc.Format(LocToastExportedFullPackage, Path.GetFileName(path)));

			if (AppState.GetOpenFolderAfterDownload())
				FileExplorerProvider.Current?.RevealFile(path);
		}
		catch (Exception ex)
		{
			ToastService.Show(_rootPanel, Loc.Format(LocErrorSaveFailed, ex.Message));
		}
	}

	private void ExportPresetForLinux(bool asXcursorTheme)
	{
		var invalid = Path.GetInvalidPathChars();
		var presetName = string.Join("", (_nameBox.Text ?? "").Where(c => !invalid.Contains(c))).Trim();

		if (string.IsNullOrWhiteSpace(presetName))
			presetName = Loc.Get(LocDefaultPresetName);

		var roleFiles = new Dictionary<string, string>();

		foreach (var slot in _slots)
		{
			var resolvedPath = GetSlotResolvedPath(slot);
			if (resolvedPath != null && File.Exists(resolvedPath))
				roleFiles[slot.Role.RegistryName] = resolvedPath;
		}

		if (roleFiles.Count == 0)
			return;

		try
		{
			var path = asXcursorTheme
				? PresetPackageService.ExportXcursorThemeForFiles(presetName, roleFiles)
				: PresetPackageService.ExportLinuxArchiveForFiles(presetName, roleFiles);

			if (path == null)
				return;

			var toastKey = asXcursorTheme ? LocToastExportedXcursorTheme : LocToastExportedLinuxArchive;
			ToastService.Show(_rootPanel, Loc.Format(toastKey, 1, Path.GetFileName(path)));

			if (AppState.GetOpenFolderAfterDownload())
				FileExplorerProvider.Current?.RevealFile(path);
		}
		catch (Exception ex)
		{
			ToastService.Show(_rootPanel, Loc.Format(LocErrorSaveFailed, ex.Message));
		}
	}

	private void OnSaveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		if (_slots.All(slot => slot.SourcePath == null && slot.RefPresetId == null))
		{
			ToastService.Show(_rootPanel, Loc.Get(LocEditorNoFiles));
			return;
		}

		var draft = new PresetDraft
		{
			Id = _draftId,
			Name = _nameBox.Text ?? EmptyValue,
			Author = GetAuthor(),
			BaseSize = _baseSize,
			UseScaling = _useScaling,
			ScaleMode = _scaleMode,
		};

		foreach (var slot in _slots.Where(slot => slot.SourcePath != null))
			draft.RoleSources[slot.Role.RegistryName] = new RoleSourceDraft { OwnFilePath = slot.SourcePath };

		foreach (var slot in _slots.Where(slot => slot.RefPresetId != null && slot.RefFileName != null))
			draft.RoleSources[slot.Role.RegistryName] = new RoleSourceDraft
			{
				Ref = new RoleRef { PresetId = slot.RefPresetId!, FileName = slot.RefFileName! }
			};

		foreach (var slot in _slots.Where(slot => slot.IsLocked))
			draft.LockedRoles.Add(slot.Role.RegistryName);

		Result = draft;
		Close();
	}

	private void OnApplySizeClick(object? sender, RoutedEventArgs e)
	{
		if (Owner is not MainWindow mainWindow)
			return;

		mainWindow.ApplyPresetSize(_baseSize);
		mainWindow.SetScaleCursorsCheckbox(_useScaling);
	}

	private async Task BrowseFolder()
	{
		var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
		{
			Title = Loc.Get(LocEditorBrowseFolder),
			AllowMultiple = false,
		});

		if (folders.Count == 0)
			return;

		var path = folders[0].Path.LocalPath;
		ImportFolder(path);
	}

	private void OnDropZoneDragOver(object? sender, DragEventArgs e)
	{
		if (e.Data.Contains(DataFormats.Files))
			e.DragEffects = DragDropEffects.Copy;
		else
			e.DragEffects = DragDropEffects.None;
	}

	private void OnDropZoneDrop(object? sender, DragEventArgs e)
	{
		HideAllDropIndicators();

		if (!e.Data.Contains(DataFormats.Files))
			return;

		var files = e.Data.GetFiles()?.Select(f => f.Path.LocalPath).ToArray();
		if (files == null || files.Length == 0)
			return;

		var folder = files[0];
		if (Directory.Exists(folder))
		{
			if (!TryImportXcursorTheme(folder, Path.GetFileName(folder)))
				ImportFolder(folder);

			return;
		}

		if (!File.Exists(folder))
			return;

		if (ArchiveImportService.IsArchiveFile(folder))
		{
			try
			{
				var extractedDir = ArchiveImportService.ExtractToTempFolder(folder);
				var displayName = Path.GetFileNameWithoutExtension(folder);

				if (!TryImportXcursorTheme(extractedDir, displayName))
					ImportFolder(extractedDir);
			}
			catch (Exception ex)
			{
				ToastService.Show(_rootPanel, Loc.Format(LocErrorArchiveExtractFailed, ex.Message));
			}

			return;
		}

		ImportFolder(Path.GetDirectoryName(folder)!);
	}

	private bool TryImportXcursorTheme(string folder, string? displayName)
	{
		var themeDir = PresetPackageService.LooksLikeXcursorTheme(folder)
			? folder
			: Directory.GetDirectories(folder).FirstOrDefault(PresetPackageService.LooksLikeXcursorTheme);

		if (themeDir == null)
			return false;

		var reconstructedDir = Path.Combine(Path.GetTempPath(), $"{XcursorTempFilePrefix}{Guid.NewGuid():N}");
		var roleFiles = PresetPackageService.ReconstructXcursorThemeRoles(themeDir, reconstructedDir);

		if (roleFiles.Count == 0)
			return false;

		var nameHint = PresetPackageService.ReadXcursorThemeName(themeDir) ?? displayName;
		if (!string.IsNullOrWhiteSpace(nameHint) && string.IsNullOrWhiteSpace(_draftId))
			_nameBox.Text = nameHint;

		var matched = 0;
		var emptySkipped = 0;

		foreach (var slot in _slots)
		{
			if (!roleFiles.TryGetValue(slot.Role.RegistryName, out var cursorPath))
				continue;

			matched++;

			if (slot.IsLocked)
				continue;

			if (IsFullyTransparent(cursorPath))
			{
				emptySkipped++;
				continue;
			}

			SetSlotSource(slot, cursorPath);
		}

		if (emptySkipped > 0)
			ToastService.Show(_rootPanel, Loc.Format(LocEditorEmptySkipped, emptySkipped));

		if (matched == 0)
			ToastService.Show(_rootPanel, Loc.Format(LocEditorNoMatchInFolder, roleFiles.Count));

		return true;
	}

	private void OnSlotDragOver(object? sender, DragEventArgs e)
	{
		if (sender is not Border border || border.Tag is not Slot slot)
			return;

		if (slot.IsLocked || !e.Data.Contains(DataFormats.Files))
		{
			e.DragEffects = DragDropEffects.None;
			return;
		}

		var file = e.Data.GetFiles()?.FirstOrDefault();
		if (file == null)
		{
			e.DragEffects = DragDropEffects.None;
			return;
		}

		e.DragEffects = DragDropEffects.Copy;
		HideAllDropIndicators();
		slot.DropIndicator.IsVisible = true;
	}

	private void OnSlotDrop(object? sender, DragEventArgs e)
	{
		if (sender is not Border border || border.Tag is not Slot slot)
			return;

		HideAllDropIndicators();

		if (slot.IsLocked || !e.Data.Contains(DataFormats.Files))
			return;

		var file = e.Data.GetFiles()?.FirstOrDefault();
		if (file == null)
			return;

		var filePath = file.Path.LocalPath;
		if (!File.Exists(filePath))
			return;

		var cursorPath = ConvertToCursorTempFile(filePath);
		if (cursorPath != null && IsFullyTransparent(cursorPath))
		{
			ToastService.Show(_rootPanel, Loc.Get(LocEditorEmptyCursorWarning));
			return;
		}

		SetSlotSource(slot, cursorPath ?? filePath);
	}

	private void HideAllDropIndicators()
	{
		foreach (var slot in _slots)
			slot.DropIndicator.IsVisible = false;
	}

	private void ImportFolder(string folder)
	{
		if (!Directory.Exists(folder))
			return;

		var folderName = Path.GetFileName(folder);
		if (!string.IsNullOrWhiteSpace(folderName) && string.IsNullOrWhiteSpace(_draftId))
			_nameBox.Text = folderName;

		var convertibleFiles = Directory.EnumerateFiles(folder, AllFilesPattern, SearchOption.AllDirectories)
			.Where(file => ConvertibleExtensions.Contains(Path.GetExtension(file)))
			.ToList();

		if (convertibleFiles.Count == 0)
		{
			ToastService.Show(_rootPanel, Loc.Get(LocEditorNoCursorInFolder));
			return;
		}

		var matched = 0;
		var emptySkipped = 0;

		foreach (var file in convertibleFiles)
		{
			var role = CursorRoles.MatchByFileName(file);
			if (role == null)
				continue;

			var slot = _slots.First(slot => slot.Role.RegistryName == role.RegistryName);

			var cursorPath = ConvertToCursorTempFile(file);
			if (cursorPath == null)
				continue;

			if (IsFullyTransparent(cursorPath))
			{
				emptySkipped++;
				continue;
			}

			SetSlotSource(slot, cursorPath);
			matched++;
		}

		if (matched == 0 && emptySkipped == 0)
			ToastService.Show(_rootPanel, Loc.Format(LocEditorNoMatchInFolder, convertibleFiles.Count));
		else if (emptySkipped > 0)
			ToastService.Show(_rootPanel, Loc.Format(LocEditorEmptySkipped, emptySkipped));
	}

	private static bool IsFullyTransparent(string path)
	{
		var ext = Path.GetExtension(path).ToLowerInvariant();

		if (ext == CurExtension)
		{
			var image = CursorCanvasService.TryRead(path);
			if (image == null)
				return false;

			for (var i = 3; i < image.Bgra.Length; i += 4)
			{
				if (image.Bgra[i] != 0)
					return false;
			}

			return true;
		}

		if (ext == AniExtension)
		{
			var frames = AniCursorReader.Read(path);
			if (frames == null || frames.Frames.Count == 0)
				return false;

			foreach (var frame in frames.Frames)
			{
				if (!IsBgraTransparent(frame.Bgra))
					return false;
			}

			return true;
		}

		return false;
	}

	private static bool IsBgraTransparent(byte[] bgra)
	{
		for (var i = 3; i < bgra.Length; i += 4)
		{
			if (bgra[i] != 0)
				return false;
		}

		return true;
	}

	private static string? ConvertToCursorTempFile(string path)
	{
		var extension = Path.GetExtension(path).ToLowerInvariant();

		if (extension == CurExtension || extension == AniExtension)
			return path;

		if (extension == GifExtension)
		{
			var aniPath = TryConvertAnimatedGif(path);
			if (aniPath != null)
				return aniPath;
		}

		try
		{
			using var bitmap = new Bitmap(path);
			var width = Math.Min(bitmap.PixelSize.Width, MaxCursorDimension);
			var height = Math.Min(bitmap.PixelSize.Height, MaxCursorDimension);

			var tempBitmap = new WriteableBitmap(
				new PixelSize(width, height),
				new Vector(DefaultDpi, DefaultDpi),
				Avalonia.Platform.PixelFormat.Bgra8888,
				Avalonia.Platform.AlphaFormat.Unpremul);

			var bgra = new byte[width * height * 4];

			using (var framebuffer = tempBitmap.Lock())
			{
				bitmap.CopyPixels(
					new PixelRect(0, 0, width, height),
					framebuffer.Address,
					width * height * 4,
					width * 4);

				System.Runtime.InteropServices.Marshal.Copy(framebuffer.Address, bgra, 0, bgra.Length);
			}

			var image = new CursorCanvasImage(width, height, 0, 0, bgra);
			var tempPath = Path.Combine(Path.GetTempPath(), ConvertTempFilePrefix + Guid.NewGuid() + CurExtension);
			CursorCanvasService.Write(tempPath, image);

			return tempPath;
		}
		catch
		{
			return null;
		}
	}

	private const int MaxDimension = 256;
	private const int MinFrameDurationMs = 20;
	private const int MaxFrameDurationMs = 10000;
	private const string ConvertAniTempFilePrefix = "cursor-palette-convert-";

	private static string? TryConvertAnimatedGif(string path)
	{
		var frames = GifDecoderService.Decode(path);
		if (frames == null || frames.Count <= 1)
			return null;

		var clampedWidth = Math.Clamp(frames[0].Width, 1, MaxDimension);
		var clampedHeight = Math.Clamp(frames[0].Height, 1, MaxDimension);

		var cursorFrames = new List<CursorCanvasImage>(frames.Count);
		var delays = new List<int>(frames.Count);

		foreach (var frame in frames)
		{
			var bgra = frame.Bgra;

			if (frame.Width != clampedWidth || frame.Height != clampedHeight)
				bgra = CropBgra(bgra, frame.Width, frame.Height, clampedWidth, clampedHeight);

			var duration = Math.Clamp(frame.DelayMs, MinFrameDurationMs, MaxFrameDurationMs);
			cursorFrames.Add(new CursorCanvasImage(clampedWidth, clampedHeight, 0, 0, bgra));
			delays.Add(duration);
		}

		var tempPath = Path.Combine(Path.GetTempPath(), ConvertAniTempFilePrefix + Guid.NewGuid() + ".ani");
		AniCursorWriter.Save(tempPath, cursorFrames, delays);

		return tempPath;
	}

	private static byte[] CropBgra(byte[] source, int srcWidth, int srcHeight, int dstWidth, int dstHeight)
	{
		var destination = new byte[dstWidth * dstHeight * 4];

		for (var y = 0; y < dstHeight; y++)
		{
			for (var x = 0; x < dstWidth; x++)
			{
				var sourceIndex = (y * srcWidth + x) * 4;
				var destinationIndex = (y * dstWidth + x) * 4;
				destination[destinationIndex] = source[sourceIndex];
				destination[destinationIndex + 1] = source[sourceIndex + 1];
				destination[destinationIndex + 2] = source[sourceIndex + 2];
				destination[destinationIndex + 3] = source[sourceIndex + 3];
			}
		}

		return destination;
	}
}
