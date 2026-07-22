using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CursorPalette.Models;
using CursorPalette.Services;
using Microsoft.Win32;

namespace CursorPalette.Views;

public partial class MainWindow : Window
{
	private const string PixelSuffix = "px";
	private const string CurExtension = ".cur";
	private const string AniExtension = ".ani";
	private const string FooterFormat = "{0}  ·  v{1}  ·  {2}";
	private const string AddCellPlusText = "+";
	private const string EmptyValue = "";
	private const string FileSearchPattern = "*.*";
	private const string PresetDragFormatName = "CursorPalette.PresetId";
	private const string GroupDragFormatName = "CursorPalette.GroupId";
	private const double ReorderIndicatorWidth = 4;
	private const double ReorderRowGroupingTolerance = 4;

	private const string BrushTextDim = "Brush.TextDim";
	private const string BrushBg = "Brush.Bg";
	private const string BrushAccent = "Brush.Accent";
	private const string BrushBorder = "Brush.Border";
	private const string BrushSurface = "Brush.Surface";
	private const string BrushSurfaceHover = "Brush.SurfaceHover";

	private const string LocWindowsDefault = "S.WindowsDefault";
	private const string LocMenuEdit = "S.Menu.Edit";
	private const string LocMenuRename = "S.Menu.Rename";
	private const string LocMenuMoveLeft = "S.Menu.MoveLeft";
	private const string LocMenuMoveRight = "S.Menu.MoveRight";
	private const string LocMenuDownload = "S.Menu.Download";
	private const string LocMenuDelete = "S.Menu.Delete";
	private const string LocPresetContextHint = "S.Preset.ContextHint";
	private const string LocAddPreset = "S.AddPreset";
	private const string LocAddPresetHint = "S.AddPreset.Hint";
	private const string LocErrorApplyFailed = "S.Error.ApplyFailed";
	private const string LocErrorTitle = "S.Error.Title";
	private const string LocErrorSaveFailed = "S.Error.SaveFailed";
	private const string LocConfirmDeleteText = "S.ConfirmDelete.Text";
	private const string LocConfirmDeleteTitle = "S.ConfirmDelete.Title";
	private const string LocToastSaved = "S.Toast.Saved";
	private const string LocToastSizeApplied = "S.Toast.SizeApplied";
	private const string LocToastPresetDownloaded = "S.Toast.PresetDownloaded";
	private const string LocDefaultPresetName = "S.DefaultPresetName";
	private const string LocToastUpdateAvailable = "S.Toast.UpdateAvailable";
	private const string LocToastImported = "S.Toast.Imported";
	private const string LocImportFileFilter = "S.Import.FileFilter";
	private const string LocErrorImportUnrecognized = "S.Error.ImportUnrecognized";
	private const string LocErrorImportVersionUnsupported = "S.Error.ImportVersionUnsupported";

	private const string SpinnerStoryboardKey = "SpinnerStoryboard";
	private const string UpdateSpinnerStoryboardKey = "UpdateSpinnerStoryboard";

	private const string StyleAccentButton = "Style.AccentButton";
	private const string StyleButton = "Style.Button";
	private const string StyleTextBox = "Style.TextBox";

	private const double CellSize = 148;
	private const double CellMargin = 6;
	private const double CellCornerRadius = 10;
	private const double CellBorderThickness = 2;
	private const double CellPreviewSize = 48;
	private const double CellNameFontSize = 13;
	private const double CellCountFontSize = 11;
	private const double CellSizeFontSize = 11;
	private const double AddCellPlusFontSize = 34;
	private const string MixedBadgeText = "🧩";
	private const double MixedBadgeFontSize = 15;
	private const string LocMixedBadgeTooltip = "S.Gallery.MixedBadgeTooltip";

	private const double GroupOutlineThickness = 1.5;
	private const double GroupOutlinePadding = 7;
	private const double GroupOutlineOpacity = 0.65;
	private const double SelectionBorderThickness = 4;
	private const string SelectionBadgeText = "✓";
	private const double SelectionBadgeSize = 20;
	private const double SelectionBadgeFontSize = 12;
	private const double GroupSwatchSize = 22;
	private const double GroupSwatchRingThickness = 2.5;
	private const double GroupDeckPeekOffsetX = 9;
	private const double GroupDeckPeekOffsetY = 6;
	private const int GroupDeckMaxPeek = 3;
	private const double GroupAttachZoneMargin = 0.25;
	private const string LocMenuRemoveFromGroup = "S.Menu.RemoveFromGroup";
	private const string LocMenuAssignToGroup = "S.Menu.AssignToGroup";
	private const string LocMenuRenameGroup = "S.Menu.RenameGroup";
	private const string LocMenuUngroup = "S.Menu.Ungroup";
	private const string LocMenuConsolidateGroup = "S.Menu.ConsolidateGroup";
	private const string LocGroupDefaultName = "S.Group.DefaultName";
	private const string LocGroupSelectedCount = "S.Group.SelectedCount";
	private const string LocGroupMembersCount = "S.Group.MembersCount";
	private const string LocGroupCollapsedTooltip = "S.Group.CollapsedTooltip";
	private const string LocGroupExpandedTooltip = "S.Group.ExpandedTooltip";
	private const string LocGroupToastCreated = "S.Group.Toast.Created";
	private const string LocGroupToastConsolidated = "S.Group.Toast.Consolidated";
	private const string BrushText = "Brush.Text";

	private const string LocInfoTitle = "S.Info.Title";
	private const string LocInfoMain = "S.Info.Main";
	private const string LocErrorArchiveExtractFailed = "S.Error.ArchiveExtractFailed";

	private const double UiZoomStep = 0.1;
	private const string ThemeIconDark = "🌙";
	private const string ThemeIconLight = "☀";

	private sealed record BoardEntry(Preset? Preset, PresetGroup? Group, int BoardIndex);

	private List<Preset> _presets = new();
	private List<PresetGroup> _groups = new();
	private Dictionary<string, PresetGroup> _presetToGroup = new();
	private List<string> _boardOrderIds = new();
	private List<string> _visibleBoardIds = new();
	private readonly List<BoardEntry> _boardOrder = new();
	private readonly HashSet<string> _selectedPresetIds = new();
	private readonly List<Border> _groupColorSwatches = new();
	private string? _pendingGroupColorKey;
	private string? _activePresetId;
	private TextBlock? _activeCellSizeText;
	private double _cellScale = AppState.GalleryCellScaleDefault;
	private double _uiScale = AppState.UiScaleDefault;
	private bool _cellScaleReady;
	private int _baselineSizePx;
	private Point? _presetDragStartPoint;
	private bool _justDraggedPreset;
	private int? _pendingInsertIndex;
	private string? _pendingGroupAttachId;
	private string? _draggedPresetId;
	private string? _draggedGroupId;
	private bool _justDraggedGroup;

	public MainWindow()
	{
		InitializeComponent();

		Width = AppState.GetMainWindowWidth();
		Height = AppState.GetMainWindowHeight();

		_activePresetId = AppState.GetActivePresetId();

		_baselineSizePx = RegistryCursorService.GetBaseSize();
		SetSliderSilently(_baselineSizePx);

		_uiScale = AppState.GetUiScale();
		ApplyUiScale(_uiScale);

		_cellScale = AppState.GetGalleryCellScale();
		SetCellScaleSliderSilently(_cellScale);

		UpdateThemeToggleIcon();
		UpdateLanguageButtonText();

		UpdateOpenFolderToggleIcon();

		BuildGroupColorSwatches();
		ReloadGallery();
		UpdateUndoButton();

		var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? AppInfo.DefaultVersion;
		FooterRun.Text = string.Format(FooterFormat, AppInfo.Author, version, AppInfo.LicenseName);

		_ = CheckForUpdatesAsync(version);
	}

	private UpdateInfo? _updateInfo;

	private async Task CheckForUpdatesAsync(string currentVersion)
	{
		((Storyboard)Resources[UpdateSpinnerStoryboardKey]).Begin(this, true);

		_updateInfo = await UpdateChecker.GetLatestReleaseInfoAsync();

		((Storyboard)Resources[UpdateSpinnerStoryboardKey]).Stop(this);
		UpdateSpinner.Visibility = Visibility.Collapsed;
		UpdateCheckingLabel.Visibility = Visibility.Collapsed;

		if (_updateInfo is null)
			return;

		if (!Version.TryParse(_updateInfo.Version, out var latestVersion))
			return;

		if (!Version.TryParse(currentVersion, out var currentVer))
			return;

		if (latestVersion > currentVer)
		{
			UpdateIndicator.Visibility = Visibility.Visible;
			ToastService.Show(RootGrid, Loc.Get(LocToastUpdateAvailable));
		}
		else
			UpToDateLabel.Visibility = Visibility.Visible;
	}

	private void OnUpdateIndicatorClick(object sender, RoutedEventArgs e)
	{
		if (_updateInfo is null)
			return;

		new UpdateWindow(_updateInfo, RootGrid) { Owner = this }.ShowDialog();
	}

	private void OnUpToDateLabelClick(object sender, MouseButtonEventArgs e)
	{
		var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? AppInfo.DefaultVersion;

		UpToDateLabel.Visibility = Visibility.Collapsed;
		UpdateCheckingLabel.Visibility = Visibility.Visible;

		_ = CheckForUpdatesAsync(version);
	}

	private void OnFooterClick(object sender, RoutedEventArgs e)
	{
		new AboutWindow { Owner = this }.ShowDialog();
	}

	private void OnGitHubIconClick(object sender, MouseButtonEventArgs e)
	{
		System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
		{
			FileName = AppInfo.GitHubUrl,
			UseShellExecute = true,
		});
	}

	private void OnInfoButtonClick(object sender, RoutedEventArgs e)
	{
		new InfoHelpWindow(Loc.Get(LocInfoTitle), Loc.Get(LocInfoMain)) { Owner = this }.ShowDialog();
	}

	private void OnOpenFolderToggleClick(object sender, RoutedEventArgs e)
	{
		AppState.SetOpenFolderAfterDownload(!AppState.GetOpenFolderAfterDownload());
		UpdateOpenFolderToggleIcon();
	}

	private void UpdateOpenFolderToggleIcon()
	{
		var brushKey = AppState.GetOpenFolderAfterDownload() ? "Brush.Accent" : "Brush.TextDim";
		OpenFolderIcon.Fill = (Brush)Application.Current.Resources[brushKey];
	}

	private void OnExportButtonClick(object sender, RoutedEventArgs e)
	{
		new ExportWindow(_presets, _groups) { Owner = this }.ShowDialog();
	}

	private void OnImportButtonClick(object sender, RoutedEventArgs e)
	{
		var dialog = new OpenFileDialog
		{
			Filter = Loc.Get(LocImportFileFilter),
			CheckFileExists = true,
			InitialDirectory = AppPaths.DownloadsDir,
		};

		if (dialog.ShowDialog(this) != true)
			return;

		DetectedPackage? detected;
		try
		{
			detected = PresetPackageService.TryDetectPackage(dialog.FileName);
		}
		catch (PackageVersionUnsupportedException exception)
		{
			MessageBox.Show(Loc.Format(LocErrorImportVersionUnsupported, exception.FoundVersion, exception.MaxSupportedVersion),
				Loc.Get(LocErrorTitle), MessageBoxButton.OK, MessageBoxImage.Warning);
			return;
		}

		if (detected == null)
		{
			MessageBox.Show(Loc.Get(LocErrorImportUnrecognized),
				Loc.Get(LocErrorTitle), MessageBoxButton.OK, MessageBoxImage.Warning);
			return;
		}

		ImportPackage(detected);
	}

	private void ImportPackage(DetectedPackage detected)
	{
		var picker = new ImportPickerWindow(detected.Entries, detected.Groups) { Owner = this };

		if (picker.ShowDialog() == true)
		{
			var imported = PresetPackageService.ImportSelected(detected, picker.SelectedEntries,
				picker.SelectedGroups, picker.IgnoreIndividualSizes, picker.UniformSize);
			ReloadGallery();

			if (imported > 0)
				ToastService.Show(RootGrid, Loc.Format(LocToastImported, imported));
		}

		PresetPackageService.CleanupPackage(detected);
	}

	private void SetSliderSilently(int sizeInPixels)
	{
		SizeSlider.Value = (sizeInPixels - RegistryCursorService.SizeStep) / (double)RegistryCursorService.SizeStep;
		SizeValueText.Text = $"{sizeInPixels} {PixelSuffix}";
	}

	private void ApplyUiScale(double scale)
	{
		UiScaleTransform.ScaleX = scale;
		UiScaleTransform.ScaleY = scale;
		UiZoomText.Text = $"{(int)Math.Round(scale * 100)}%";
	}

	private void OnUiZoomOutClick(object sender, RoutedEventArgs e) => AdjustUiZoom(-UiZoomStep);
	private void OnUiZoomInClick(object sender, RoutedEventArgs e) => AdjustUiZoom(UiZoomStep);

	private void AdjustUiZoom(double delta)
	{
		_uiScale = Math.Clamp(Math.Round(_uiScale + delta, 2), AppState.UiScaleMin, AppState.UiScaleMax);
		ApplyUiScale(_uiScale);
		AppState.SetUiScale(_uiScale);
	}

	private void SetCellScaleSliderSilently(double scale)
	{
		_cellScaleReady = false;
		CellScaleSlider.Value = scale;
		CellScaleValueText.Text = $"{(int)Math.Round(scale * 100)}%";
		_cellScaleReady = true;
	}

	private void OnCellScaleSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (CellScaleValueText == null)
			return;

		CellScaleValueText.Text = $"{(int)Math.Round(e.NewValue * 100)}%";

		if (!_cellScaleReady)
			return;

		_cellScale = e.NewValue;
		AppState.SetGalleryCellScale(_cellScale);
		ReloadGallery();
	}

	private void UpdateThemeToggleIcon() =>
		ThemeToggleIcon.Text = ThemeManager.Current == ThemeManager.Dark ? ThemeIconDark : ThemeIconLight;

	private void OnThemeToggleClick(object sender, RoutedEventArgs e)
	{
		var next = ThemeManager.Current == ThemeManager.Dark ? ThemeManager.Light : ThemeManager.Dark;
		ThemeManager.SetTheme(next);
		ReplaceWindowToApplyNewTheme();
	}

	private void ReplaceWindowToApplyNewTheme()
	{
		var wasMaximized = WindowState == WindowState.Maximized;
		var bounds = RestoreBounds;

		var replacement = new MainWindow
		{
			WindowStartupLocation = WindowStartupLocation.Manual,
			Left = bounds.Left,
			Top = bounds.Top,
			Width = bounds.Width,
			Height = bounds.Height,
		};

		Application.Current.MainWindow = replacement;
		replacement.Show();

		if (wasMaximized)
			replacement.WindowState = WindowState.Maximized;

		Close();
	}

	private void UpdateLanguageButtonText() =>
		LanguageButtonText.Text = LocalizationManager.Current.ToUpperInvariant();

	private void OnLanguageButtonClick(object sender, RoutedEventArgs e)
	{
		var menu = new ContextMenu { PlacementTarget = LanguageButton, IsOpen = true };

		foreach (var language in LocalizationManager.Available)
		{
			var item = new MenuItem
			{
				Header = language.DisplayName,
				IsCheckable = true,
				IsChecked = language.Code == LocalizationManager.Current,
			};
			item.Click += (_, _) => SwitchLanguage(language.Code);
			menu.Items.Add(item);
		}
	}

	private void SwitchLanguage(string code)
	{
		if (code == LocalizationManager.Current)
			return;

		LocalizationManager.SetLanguage(code);
		UpdateLanguageButtonText();
		ReloadGallery();
	}

	private void ReloadGallery()
	{
		_presets = PresetStore.LoadAll();
		_groups = GroupStore.LoadAll();
		_presetToGroup = _groups
			.SelectMany(group => group.MemberPresetIds.Select(presetId => (presetId, group)))
			.GroupBy(entry => entry.presetId)
			.ToDictionary(entry => entry.Key, entry => entry.First().group);

		_boardOrderIds = ReconcileBoardOrder(BoardOrderStore.Load(), _presets, _groups, _presetToGroup);
		BoardOrderStore.Save(_boardOrderIds);
		_visibleBoardIds = _boardOrderIds.Where(IsBoardIdVisible).ToList();

		ClearGroupSelection();

		Gallery.Items.Clear();
		_activeCellSizeText = null;

		if (_activePresetId != null && _presets.All(preset => preset.Id != _activePresetId))
		{
			_activePresetId = null;
			AppState.SetActivePresetId(null);
		}

		Gallery.Items.Add(CreateDefaultCell());

		_boardOrder.Clear();
		var presetsById = _presets.ToDictionary(preset => preset.Id);
		var groupsById = _groups.ToDictionary(group => group.Id);

		for (var boardIndex = 0; boardIndex < _boardOrderIds.Count; boardIndex++)
		{
			var id = _boardOrderIds[boardIndex];

			if (groupsById.TryGetValue(id, out var group))
			{
				Gallery.Items.Add(CreateGroupCell(group));
				_boardOrder.Add(new BoardEntry(null, group, boardIndex));
				continue;
			}

			if (!presetsById.TryGetValue(id, out var preset))
				continue;

			if (_presetToGroup.TryGetValue(preset.Id, out var owningGroup) && owningGroup.Collapsed)
				continue;

			Gallery.Items.Add(CreatePresetCell(preset, _presetToGroup.GetValueOrDefault(preset.Id)));
			_boardOrder.Add(new BoardEntry(preset, null, boardIndex));
		}

		Gallery.Items.Add(CreateAddCell());
		EmptyHint.Visibility = _presets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
	}

	private bool IsBoardIdVisible(string id)
	{
		if (_presetToGroup.TryGetValue(id, out var group))
			return !group.Collapsed;

		return true;
	}

	private static List<string> ReconcileBoardOrder(List<string> persisted, List<Preset> presets,
		List<PresetGroup> groups, Dictionary<string, PresetGroup> presetToGroup)
	{
		var validIds = new HashSet<string>(presets.Select(preset => preset.Id));
		validIds.UnionWith(groups.Select(group => group.Id));

		var result = persisted.Where(validIds.Contains).ToList();
		var known = new HashSet<string>(result);
		var placedGroups = new HashSet<string>();

		foreach (var preset in presets)
		{
			if (presetToGroup.TryGetValue(preset.Id, out var group) && placedGroups.Add(group.Id) && known.Add(group.Id))
				result.Add(group.Id);

			if (known.Add(preset.Id))
				result.Add(preset.Id);
		}

		foreach (var group in groups)
		{
			if (known.Add(group.Id))
				result.Add(group.Id);
		}

		return result;
	}

	private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

	private double CellFontScale => Math.Sqrt(_cellScale);

	private FrameworkElement CreateDefaultCell()
	{
		var isActive = _activePresetId == null;

		var defaults = RegistryCursorService.GetWindowsDefaultValues();
		var previewPath = defaults.TryGetValue(CursorRoles.ArrowRoleName, out var arrow) ? arrow : null;

		var image = new Image
		{
			Width = CellPreviewSize * _cellScale,
			Height = CellPreviewSize * _cellScale,
			SnapsToDevicePixels = true,
		};
		RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
		CursorPreviewService.ApplyPreview(image, previewPath);

		var nameText = new TextBlock
		{
			Text = Loc.Get(LocWindowsDefault),
			FontSize = CellNameFontSize * CellFontScale,
			TextTrimming = TextTrimming.CharacterEllipsis,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(4, 8, 4, 0),
		};

		var sizeText = new TextBlock
		{
			Text = $"{AppState.GetDefaultBaseSize()} {PixelSuffix}",
			Foreground = Brush(BrushTextDim),
			FontSize = CellSizeFontSize * CellFontScale,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(0, 2, 0, 0),
		};

		if (isActive)
			_activeCellSizeText = sizeText;

		var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
		panel.Children.Add(image);
		panel.Children.Add(nameText);
		panel.Children.Add(sizeText);

		var cell = new Border
		{
			Width = CellSize * _cellScale,
			Height = CellSize * _cellScale,
			Margin = new Thickness(CellMargin),
			CornerRadius = new CornerRadius(CellCornerRadius),
			Background = Brush(BrushBg),
			BorderThickness = new Thickness(CellBorderThickness),
			BorderBrush = isActive ? Brush(BrushAccent) : Brush(BrushBorder),
			Child = panel,
			Cursor = Cursors.Hand,
		};

		cell.MouseEnter += (_, _) =>
		{
			if (_activePresetId != null) cell.Background = Brush(BrushSurfaceHover);
		};
		cell.MouseLeave += (_, _) => cell.Background = Brush(BrushBg);
		cell.MouseLeftButtonUp += (_, _) => ApplyDefault();
		cell.MouseLeftButtonDown += (_, _) => { };

		cell.ToolTip = new ToolTip { Content = Loc.Get(LocWindowsDefault) };

		return cell;
	}

	private FrameworkElement CreatePresetCell(Preset preset, PresetGroup? group)
	{
		var isActive = preset.Id == _activePresetId;
		var isSelected = _selectedPresetIds.Contains(preset.Id);

		var previewPath = PresetStore.GetRoleFilePath(preset, CursorRoles.ArrowRoleName)
							?? preset.Roles.Keys.Concat(preset.RoleRefs.Keys)
								.Select(role => PresetStore.GetRoleFilePath(preset, role))
								.FirstOrDefault(path => path != null);

		var image = new Image
		{
			Width = CellPreviewSize * _cellScale,
			Height = CellPreviewSize * _cellScale,
			SnapsToDevicePixels = true,
		};
		RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
		CursorPreviewService.ApplyPreview(image, previewPath);

		var nameText = new TextBlock
		{
			Text = preset.Name,
			FontSize = CellNameFontSize * CellFontScale,
			TextTrimming = TextTrimming.CharacterEllipsis,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(4, 8, 4, 0),
		};

		var countText = new TextBlock
		{
			Text = $"{preset.Roles.Count + preset.RoleRefs.Count}/{CursorRoles.All.Length}",
			Foreground = Brush(BrushTextDim),
			FontSize = CellCountFontSize * CellFontScale,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(0, 2, 0, 0),
		};

		var sizeText = new TextBlock
		{
			Text = $"{preset.BaseSize} {PixelSuffix}",
			Foreground = Brush(BrushTextDim),
			FontSize = CellSizeFontSize * CellFontScale,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(0, 2, 0, 0),
		};

		if (isActive)
			_activeCellSizeText = sizeText;

		var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
		panel.Children.Add(image);
		panel.Children.Add(nameText);
		panel.Children.Add(countText);
		panel.Children.Add(sizeText);

		var cellContent = new Grid();
		cellContent.Children.Add(panel);

		var isMixed = preset.RoleRefs.Count > 0;
		if (isMixed)
		{
			cellContent.Children.Add(new TextBlock
			{
				Text = MixedBadgeText,
				FontSize = MixedBadgeFontSize * CellFontScale,
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Top,
				Margin = new Thickness(0, 4, 6, 0),
				IsHitTestVisible = false,
			});
		}

		var selectionBadge = new Border
		{
			Width = SelectionBadgeSize * CellFontScale,
			Height = SelectionBadgeSize * CellFontScale,
			CornerRadius = new CornerRadius(SelectionBadgeSize),
			Background = Brush(BrushAccent),
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(6, 4, 0, 0),
			IsHitTestVisible = false,
			Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed,
			Child = new TextBlock
			{
				Text = SelectionBadgeText,
				FontSize = SelectionBadgeFontSize * CellFontScale,
				Foreground = System.Windows.Media.Brushes.White,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
			},
		};
		cellContent.Children.Add(selectionBadge);

		var cell = new Border
		{
			Width = CellSize * _cellScale,
			Height = CellSize * _cellScale,
			Margin = new Thickness(CellMargin),
			CornerRadius = new CornerRadius(CellCornerRadius),
			Background = Brush(BrushSurface),
			BorderThickness = new Thickness(isSelected ? SelectionBorderThickness : CellBorderThickness),
			BorderBrush = isSelected ? Brush(BrushAccent) : (isActive ? Brush(BrushAccent) : Brush(BrushBorder)),
			Child = cellContent,
			Cursor = Cursors.Hand,
			Tag = preset,
		};

		FrameworkElement result = cell;
		if (group != null)
		{
			var groupBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(GroupColors.ResolveHex(group.ColorKey))!);
			var ringSize = CellSize * _cellScale + GroupOutlinePadding * 2;

			var outlineRect = new System.Windows.Shapes.Rectangle
			{
				Width = ringSize,
				Height = ringSize,
				RadiusX = CellCornerRadius + GroupOutlinePadding,
				RadiusY = CellCornerRadius + GroupOutlinePadding,
				Stroke = groupBrush,
				StrokeThickness = GroupOutlineThickness,
				StrokeDashArray = new DoubleCollection { 4, 3 },
				Opacity = GroupOutlineOpacity,
				IsHitTestVisible = false,
			};

			cell.Margin = new Thickness(GroupOutlinePadding);

			var wrapper = new Grid { Margin = new Thickness(CellMargin) };
			wrapper.Children.Add(cell);
			wrapper.Children.Add(outlineRect);
			result = wrapper;
		}

		cell.MouseEnter += (_, _) =>
		{
			if (preset.Id != _activePresetId) cell.Background = Brush(BrushSurfaceHover);
		};
		cell.MouseLeave += (_, _) => cell.Background = Brush(BrushSurface);
		cell.MouseLeftButtonDown += (_, e) =>
		{
			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
				return;

			_presetDragStartPoint = e.GetPosition(cell);
		};
		cell.MouseMove += (_, e) =>
		{
			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
				return;

			if (_presetDragStartPoint is not { } start || e.LeftButton != MouseButtonState.Pressed)
				return;

			var position = e.GetPosition(cell);
			if (Math.Abs(position.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
				Math.Abs(position.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance)
				return;

			_presetDragStartPoint = null;
			_justDraggedPreset = true;
			BeginDragGhost(preset, previewPath);
			DragDrop.DoDragDrop(cell, new DataObject(PresetDragFormatName, preset.Id), DragDropEffects.Move);
			EndDragGhost();
		};
		cell.MouseLeftButtonUp += (_, _) =>
		{
			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
			{
				_presetDragStartPoint = null;
				ToggleSelection(preset, cell, selectionBadge);
				return;
			}

			_presetDragStartPoint = null;

			if (_justDraggedPreset)
			{
				_justDraggedPreset = false;
				return;
			}

			ApplyPreset(preset);
		};
		cell.MouseRightButtonUp += (_, e) =>
		{
			cell.ContextMenu!.IsOpen = true;
			e.Handled = true;
		};

		var visibleIndex = _visibleBoardIds.IndexOf(preset.Id);
		var isFirst = visibleIndex <= 0;
		var isLast = visibleIndex < 0 || visibleIndex >= _visibleBoardIds.Count - 1;

		var menu = new ContextMenu();
		var editItem = new MenuItem { Header = Loc.Get(LocMenuEdit) };
		editItem.Click += (_, _) => EditPreset(preset);
		var renameItem = new MenuItem { Header = Loc.Get(LocMenuRename) };
		renameItem.Click += (_, _) => StartInlineRename(preset, nameText, panel);
		var moveLeftItem = new MenuItem
		{
			Header = Loc.Get(LocMenuMoveLeft),
			Visibility = isFirst ? Visibility.Collapsed : Visibility.Visible,
		};
		moveLeftItem.Click += (_, _) => MovePreset(preset, -1);
		var moveRightItem = new MenuItem
		{
			Header = Loc.Get(LocMenuMoveRight),
			Visibility = isLast ? Visibility.Collapsed : Visibility.Visible,
		};
		moveRightItem.Click += (_, _) => MovePreset(preset, 1);
		var downloadItem = new MenuItem { Header = Loc.Get(LocMenuDownload) };
		downloadItem.Click += (_, _) => DownloadPreset(preset);
		var deleteItem = new MenuItem { Header = Loc.Get(LocMenuDelete) };
		deleteItem.Click += (_, _) => DeletePreset(preset);
		menu.Items.Add(editItem);
		menu.Items.Add(renameItem);
		menu.Items.Add(moveLeftItem);
		menu.Items.Add(moveRightItem);
		menu.Items.Add(downloadItem);

		var assignableGroups = _groups.Where(candidate => group == null || candidate.Id != group.Id).ToList();
		if (assignableGroups.Count > 0)
		{
			var assignToGroupItem = new MenuItem { Header = Loc.Get(LocMenuAssignToGroup) };

			foreach (var targetGroup in assignableGroups)
			{
				var targetGroupItem = new MenuItem
				{
					Header = targetGroup.Name,
					Icon = new Border
					{
						Width = 10,
						Height = 10,
						CornerRadius = new CornerRadius(10),
						Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(GroupColors.ResolveHex(targetGroup.ColorKey))!),
					},
				};
				targetGroupItem.Click += (_, _) =>
				{
					if (_presetToGroup.TryGetValue(preset.Id, out var currentGroup))
						GroupStore.RemoveMember(currentGroup.Id, preset.Id);

					GroupStore.AddMember(targetGroup.Id, preset.Id);
					ReloadGallery();
				};
				assignToGroupItem.Items.Add(targetGroupItem);
			}

			menu.Items.Add(assignToGroupItem);
		}

		if (group != null)
		{
			var removeFromGroupItem = new MenuItem { Header = Loc.Get(LocMenuRemoveFromGroup) };
			removeFromGroupItem.Click += (_, _) =>
			{
				GroupStore.RemoveMember(group.Id, preset.Id);
				ReloadGallery();
			};
			menu.Items.Add(removeFromGroupItem);
		}

		menu.Items.Add(new Separator());
		menu.Items.Add(deleteItem);
		cell.ContextMenu = menu;

		var hintPanel = new StackPanel();
		hintPanel.Children.Add(new TextBlock { Text = preset.Name, FontWeight = FontWeights.SemiBold });
		if (isMixed)
		{
			hintPanel.Children.Add(new TextBlock
			{
				Text = Loc.Get(LocMixedBadgeTooltip),
				FontSize = 11,
				Foreground = Brush(BrushTextDim),
				Margin = new Thickness(0, 2, 0, 0),
			});
		}
		hintPanel.Children.Add(new TextBlock
		{
			Text = Loc.Get(LocPresetContextHint),
			FontSize = 11,
			Foreground = Brush(BrushTextDim),
			Margin = new Thickness(0, 2, 0, 0),
		});
		cell.ToolTip = new ToolTip { Content = hintPanel };

		cell.InputBindings.Add(new MouseBinding(
			new RelayUiCommand(() => EditPreset(preset)),
			new MouseGesture(MouseAction.LeftDoubleClick)));

		return result;
	}

	private FrameworkElement CreateGroupCell(PresetGroup group)
	{
		var colorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(GroupColors.ResolveHex(group.ColorKey))!);

		var nameText = new TextBlock
		{
			Text = group.Name,
			FontSize = CellNameFontSize * CellFontScale,
			FontWeight = FontWeights.SemiBold,
			Foreground = System.Windows.Media.Brushes.White,
			TextTrimming = TextTrimming.CharacterEllipsis,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(4, 8, 4, 0),
		};

		var countText = new TextBlock
		{
			Text = Loc.Format(LocGroupMembersCount, group.MemberPresetIds.Count),
			Foreground = System.Windows.Media.Brushes.White,
			Opacity = 0.85,
			FontSize = CellCountFontSize * CellFontScale,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(0, 2, 0, 0),
		};

		var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
		panel.Children.Add(nameText);
		panel.Children.Add(countText);

		var tile = new Border
		{
			Width = CellSize * _cellScale,
			Height = CellSize * _cellScale,
			CornerRadius = new CornerRadius(CellCornerRadius),
			Background = colorBrush,
			BorderThickness = new Thickness(0),
			SnapsToDevicePixels = true,
			Child = panel,
			Cursor = Cursors.Hand,
		};
		Panel.SetZIndex(tile, GroupDeckMaxPeek + 1);

		tile.MouseLeftButtonDown += (_, e) =>
		{
			if (!group.Collapsed || Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
				return;

			_presetDragStartPoint = e.GetPosition(tile);
		};
		tile.MouseMove += (_, e) =>
		{
			if (!group.Collapsed || Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
				return;

			if (_presetDragStartPoint is not { } start || e.LeftButton != MouseButtonState.Pressed)
				return;

			var position = e.GetPosition(tile);
			if (Math.Abs(position.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
				Math.Abs(position.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance)
				return;

			_presetDragStartPoint = null;
			_justDraggedGroup = true;
			BeginGroupDragGhost(group);
			DragDrop.DoDragDrop(tile, new DataObject(GroupDragFormatName, group.Id), DragDropEffects.Move);
			EndDragGhost();
		};
		tile.MouseLeftButtonUp += (_, _) =>
		{
			_presetDragStartPoint = null;

			if (_justDraggedGroup)
			{
				_justDraggedGroup = false;
				return;
			}

			GroupStore.SetCollapsed(group.Id, !group.Collapsed);
			ReloadGallery();
		};

		var menu = new ContextMenu();
		var renameItem = new MenuItem { Header = Loc.Get(LocMenuRenameGroup) };
		renameItem.Click += (_, _) => StartInlineGroupRename(group, nameText, panel);
		var consolidateItem = new MenuItem { Header = Loc.Get(LocMenuConsolidateGroup) };
		consolidateItem.Click += (_, _) => ConsolidateGroup(group.Id);
		var ungroupItem = new MenuItem { Header = Loc.Get(LocMenuUngroup) };
		ungroupItem.Click += (_, _) =>
		{
			GroupStore.Delete(group.Id);
			ReloadGallery();
		};
		menu.Items.Add(renameItem);
		menu.Items.Add(consolidateItem);
		menu.Items.Add(ungroupItem);
		tile.ContextMenu = menu;

		tile.MouseRightButtonUp += (_, e) =>
		{
			tile.ContextMenu!.IsOpen = true;
			e.Handled = true;
		};

		tile.ToolTip = new ToolTip
		{
			Content = Loc.Get(group.Collapsed ? LocGroupExpandedTooltip : LocGroupCollapsedTooltip),
		};

		if (!group.Collapsed)
		{
			var wrapper = new Border { Margin = new Thickness(CellMargin), Child = tile };
			tile.Margin = new Thickness(0);
			return wrapper;
		}

		var deckGrid = new Grid
		{
			Margin = new Thickness(CellMargin),
			Width = CellSize * _cellScale + GroupDeckMaxPeek * GroupDeckPeekOffsetX * _cellScale,
			Height = CellSize * _cellScale + GroupDeckMaxPeek * GroupDeckPeekOffsetY * _cellScale,
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Top,
		};

		var peekCount = Math.Min(GroupDeckMaxPeek, group.MemberPresetIds.Count);
		for (var i = peekCount; i >= 1; i--)
		{
			var ghost = new Border
			{
				Width = CellSize * _cellScale,
				Height = CellSize * _cellScale,
				CornerRadius = new CornerRadius(CellCornerRadius),
				Background = Brush(BrushSurface),
				BorderThickness = new Thickness(CellBorderThickness),
				BorderBrush = Brush(BrushBorder),
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Top,
				Margin = new Thickness(i * GroupDeckPeekOffsetX * _cellScale, i * GroupDeckPeekOffsetY * _cellScale, 0, 0),
			};
			Panel.SetZIndex(ghost, GroupDeckMaxPeek + 1 - i);
			deckGrid.Children.Add(ghost);
		}

		tile.Margin = new Thickness(0);
		tile.HorizontalAlignment = HorizontalAlignment.Left;
		tile.VerticalAlignment = VerticalAlignment.Top;
		deckGrid.Children.Add(tile);

		return deckGrid;
	}

	private void StartInlineGroupRename(PresetGroup group, TextBlock nameText, StackPanel panel)
	{
		var index = panel.Children.IndexOf(nameText);
		if (index < 0)
			return;

		var done = false;

		var textBox = new TextBox
		{
			Text = group.Name,
			FontSize = nameText.FontSize,
			FontWeight = FontWeights.SemiBold,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(nameText.Margin.Left, nameText.Margin.Top - 2, nameText.Margin.Right, nameText.Margin.Bottom),
			Style = (Style)Application.Current.Resources[StyleTextBox],
			Background = Brush(BrushBg),
			BorderBrush = System.Windows.Media.Brushes.White,
			BorderThickness = new Thickness(1.5),
			Padding = new Thickness(6, 4, 6, 4),
		};

		void Restore()
		{
			var currentIndex = panel.Children.IndexOf(textBox);
			if (currentIndex < 0)
				return;

			panel.Children.RemoveAt(currentIndex);
			panel.Children.Insert(currentIndex, nameText);
		}

		void Commit()
		{
			if (done)
				return;
			done = true;

			var newName = textBox.Text.Trim();
			Restore();

			if (!string.IsNullOrWhiteSpace(newName) && newName != group.Name)
			{
				GroupStore.Rename(group.Id, newName);
				ReloadGallery();
			}
		}

		void Cancel()
		{
			if (done)
				return;
			done = true;
			Restore();
		}

		textBox.PreviewMouseLeftButtonDown += (_, e) => e.Handled = true;
		textBox.PreviewMouseLeftButtonUp += (_, e) => e.Handled = true;
		textBox.KeyDown += (_, e) =>
		{
			if (e.Key == Key.Enter)
			{
				Commit();
				e.Handled = true;
			}
			else if (e.Key == Key.Escape)
			{
				Cancel();
				e.Handled = true;
			}
		};
		textBox.LostFocus += (_, _) => Commit();

		panel.Children.RemoveAt(index);
		panel.Children.Insert(index, textBox);
		textBox.Focus();
		textBox.SelectAll();
	}

	private void ClearGroupSelection()
	{
		_selectedPresetIds.Clear();
		_pendingGroupColorKey = null;

		if (GroupNameBox != null)
			GroupNameBox.Text = Loc.Get(LocGroupDefaultName);

		foreach (var swatch in _groupColorSwatches)
			swatch.BorderThickness = new Thickness(0);

		if (GroupToolbar != null)
			GroupToolbar.Visibility = Visibility.Collapsed;
	}

	private void ToggleSelection(Preset preset, Border cell, Border selectionBadge)
	{
		var nowSelected = !_selectedPresetIds.Contains(preset.Id);

		if (nowSelected)
			_selectedPresetIds.Add(preset.Id);
		else
			_selectedPresetIds.Remove(preset.Id);

		selectionBadge.Visibility = nowSelected ? Visibility.Visible : Visibility.Collapsed;
		cell.BorderBrush = nowSelected || preset.Id == _activePresetId ? Brush(BrushAccent) : Brush(BrushBorder);

		UpdateGroupToolbar();
	}

	private void UpdateGroupToolbar()
	{
		if (_selectedPresetIds.Count == 0)
		{
			GroupToolbar.Visibility = Visibility.Collapsed;
			return;
		}

		GroupToolbar.Visibility = Visibility.Visible;
		GroupSelectionCountText.Text = Loc.Format(LocGroupSelectedCount, _selectedPresetIds.Count);
	}

	private void BuildGroupColorSwatches()
	{
		GroupColorSwatches.Children.Clear();
		_groupColorSwatches.Clear();

		foreach (var (key, hex) in GroupColors.Palette)
		{
			var colorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);

			var swatch = new Border
			{
				Width = GroupSwatchSize,
				Height = GroupSwatchSize,
				CornerRadius = new CornerRadius(GroupSwatchSize),
				Background = colorBrush,
				BorderBrush = Brush(BrushText),
				BorderThickness = new Thickness(0),
				Margin = new Thickness(4, 0, 4, 0),
				Cursor = Cursors.Hand,
			};

			swatch.MouseLeftButtonUp += (_, _) =>
			{
				_pendingGroupColorKey = key;

				foreach (var other in _groupColorSwatches)
					other.BorderThickness = new Thickness(0);

				swatch.BorderThickness = new Thickness(GroupSwatchRingThickness);
			};

			GroupColorSwatches.Children.Add(swatch);
			_groupColorSwatches.Add(swatch);
		}
	}

	private void OnGroupCreateClick(object sender, RoutedEventArgs e)
	{
		if (_selectedPresetIds.Count == 0 || _pendingGroupColorKey == null)
			return;

		var name = GroupNameBox.Text.Trim();
		if (name.Length == 0)
			name = Loc.Get(LocGroupDefaultName);

		foreach (var presetId in _selectedPresetIds)
		{
			if (_presetToGroup.TryGetValue(presetId, out var oldGroup))
				GroupStore.RemoveMember(oldGroup.Id, presetId);
		}

		var group = new PresetGroup
		{
			Id = Guid.NewGuid().ToString("N"),
			Name = name,
			ColorKey = _pendingGroupColorKey,
			Collapsed = false,
			MemberPresetIds = _selectedPresetIds.ToList(),
		};

		GroupStore.Save(group);
		ReloadGallery();
		ToastService.Show(RootGrid, Loc.Format(LocGroupToastCreated, group.Name));
	}

	private void OnGroupCancelClick(object sender, RoutedEventArgs e) => ReloadGallery();

	private FrameworkElement CreateAddCell()
	{
		var plus = new TextBlock
		{
			Text = AddCellPlusText,
			FontSize = AddCellPlusFontSize * CellFontScale,
			Foreground = Brush(BrushTextDim),
			TextAlignment = TextAlignment.Center,
		};
		var label = new TextBlock
		{
			Text = Loc.Get(LocAddPreset),
			FontSize = CellNameFontSize * CellFontScale,
			Foreground = Brush(BrushTextDim),
			TextAlignment = TextAlignment.Center,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(8, 4, 8, 0),
		};
		var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
		panel.Children.Add(plus);
		panel.Children.Add(label);

		var cell = new Border
		{
			Width = CellSize * _cellScale,
			Height = CellSize * _cellScale,
			Margin = new Thickness(CellMargin),
			CornerRadius = new CornerRadius(CellCornerRadius),
			Background = System.Windows.Media.Brushes.Transparent,
			BorderThickness = new Thickness(CellBorderThickness),
			BorderBrush = Brush(BrushBorder),
			Child = panel,
			Cursor = Cursors.Hand,
			ToolTip = new ToolTip { Content = Loc.Get(LocAddPresetHint) },
			AllowDrop = true,
		};

		cell.MouseEnter += (_, _) => cell.BorderBrush = Brush(BrushAccent);
		cell.MouseLeave += (_, _) => cell.BorderBrush = Brush(BrushBorder);
		cell.MouseLeftButtonUp += (_, _) => OpenEditor(null, Array.Empty<string>());
		cell.Drop += OnWindowDrop;

		return cell;
	}

	private void ShowLoadingOverlay()
	{
		LoadingOverlay.Visibility = Visibility.Visible;
		((Storyboard)Resources[SpinnerStoryboardKey]).Begin(this, true);
	}

	private void HideLoadingOverlay()
	{
		((Storyboard)Resources[SpinnerStoryboardKey]).Stop(this);
		LoadingOverlay.Visibility = Visibility.Collapsed;
	}

	private async void ApplyPreset(Preset preset, bool force = false)
	{
		if (!force && preset.Id == _activePresetId)
			return;

		try
		{
			ShowLoadingOverlay();
			await Dispatcher.Yield(DispatcherPriority.Render);

			var values = new Dictionary<string, string>();
			foreach (var role in CursorRoles.All)
			{
				var path = PresetStore.GetRoleFilePath(preset, role.RegistryName);
				values[role.RegistryName] = path != null && File.Exists(path) ? path : EmptyValue;
			}

			await Task.Run(() =>
			{
				RegistryCursorService.SaveSnapshotToDisk(RegistryCursorService.TakeSnapshot());
				RegistryCursorService.ApplyValues(values);
				RegistryCursorService.SetBaseSize(preset.BaseSize);
			});

			_baselineSizePx = preset.BaseSize;
			SetSliderSilently(preset.BaseSize);
			_activePresetId = preset.Id;
			AppState.SetActivePresetId(preset.Id);

			ReloadGallery();
			UpdateUndoButton();
		}
		catch (Exception exception)
		{
			MessageBox.Show(Loc.Format(LocErrorApplyFailed, exception.Message),
				Loc.Get(LocErrorTitle), MessageBoxButton.OK, MessageBoxImage.Error);
		}
		finally
		{
			HideLoadingOverlay();
		}
	}

	private async void ApplyDefault()
	{
		if (_activePresetId == null)
			return;

		try
		{
			ShowLoadingOverlay();
			await Dispatcher.Yield(DispatcherPriority.Render);

			var defaultSize = AppState.GetDefaultBaseSize();

			await Task.Run(() =>
			{
				RegistryCursorService.SaveSnapshotToDisk(RegistryCursorService.TakeSnapshot());
				RegistryCursorService.ApplyValues(RegistryCursorService.GetWindowsDefaultValues());
				RegistryCursorService.SetBaseSize(defaultSize);
			});

			_activePresetId = null;
			AppState.SetActivePresetId(null);

			_baselineSizePx = defaultSize;
			SetSliderSilently(defaultSize);

			ReloadGallery();
			UpdateUndoButton();
		}
		catch (Exception exception)
		{
			MessageBox.Show(Loc.Format(LocErrorApplyFailed, exception.Message),
				Loc.Get(LocErrorTitle), MessageBoxButton.OK, MessageBoxImage.Error);
		}
		finally
		{
			HideLoadingOverlay();
		}
	}

	private async void OnUndoButtonClick(object sender, RoutedEventArgs e)
	{
		var snapshot = RegistryCursorService.LoadSnapshotFromDisk();

		if (snapshot == null)
			return;

		try
		{
			ShowLoadingOverlay();
			await Dispatcher.Yield(DispatcherPriority.Render);

			await Task.Run(() =>
			{
				RegistryCursorService.SaveSnapshotToDisk(RegistryCursorService.TakeSnapshot());
				RegistryCursorService.RestoreSnapshot(snapshot);
			});

			_activePresetId = FindPresetIdByValues(snapshot.Values);
			AppState.SetActivePresetId(_activePresetId);

			_baselineSizePx = snapshot.BaseSize;
			SetSliderSilently(snapshot.BaseSize);

			ReloadGallery();
			UpdateUndoButton();
		}
		catch (Exception exception)
		{
			MessageBox.Show(Loc.Format(LocErrorApplyFailed, exception.Message),
				Loc.Get(LocErrorTitle), MessageBoxButton.OK, MessageBoxImage.Error);
		}
		finally
		{
			HideLoadingOverlay();
		}
	}

	private string? FindPresetIdByValues(IReadOnlyDictionary<string, string> values)
	{
		if (!values.TryGetValue(CursorRoles.ArrowRoleName, out var arrow) || string.IsNullOrEmpty(arrow))
			return null;

		return _presets.FirstOrDefault(preset =>
			string.Equals(PresetStore.GetRoleFilePath(preset, CursorRoles.ArrowRoleName), arrow,
				StringComparison.OrdinalIgnoreCase))?.Id;
	}

	private void UpdateUndoButton() =>
		UndoButton.IsEnabled = RegistryCursorService.LoadSnapshotFromDisk() != null;

	private void DeletePreset(Preset preset)
	{
		var answer = MessageBox.Show(
			Loc.Format(LocConfirmDeleteText, preset.Name),
			Loc.Get(LocConfirmDeleteTitle),
			MessageBoxButton.YesNo, MessageBoxImage.Question);

		if (answer != MessageBoxResult.Yes)
			return;

		if (_presetToGroup.TryGetValue(preset.Id, out var owningGroup))
			GroupStore.RemoveMember(owningGroup.Id, preset.Id);

		PresetStore.Delete(preset.Id);

		if (_activePresetId == preset.Id)
		{
			_activePresetId = null;
			AppState.SetActivePresetId(null);
		}

		ReloadGallery();
	}

	private void MovePreset(Preset preset, int direction)
	{
		var visibleIndex = _visibleBoardIds.IndexOf(preset.Id);
		if (visibleIndex < 0)
			return;

		var targetVisibleIndex = visibleIndex + direction;
		if (targetVisibleIndex < 0 || targetVisibleIndex >= _visibleBoardIds.Count)
			return;

		var ownIndex = _boardOrderIds.IndexOf(preset.Id);
		var targetIndex = _boardOrderIds.IndexOf(_visibleBoardIds[targetVisibleIndex]);
		if (ownIndex < 0 || targetIndex < 0)
			return;

		(_boardOrderIds[ownIndex], _boardOrderIds[targetIndex]) = (_boardOrderIds[targetIndex], _boardOrderIds[ownIndex]);
		PersistBoardOrder();
	}

	private void DownloadPreset(Preset preset)
	{
		var invalid = Path.GetInvalidPathChars();
		var presetName = string.Join(EmptyValue, preset.Name.Where(character => !invalid.Contains(character))).Trim();
		if (string.IsNullOrWhiteSpace(presetName))
			presetName = Loc.Get(LocDefaultPresetName);

		var destDir = Path.Combine(AppPaths.DownloadsDir, presetName);

		var attempt = 1;
		while (Directory.Exists(destDir))
			destDir = Path.Combine(AppPaths.DownloadsDir, $"{presetName} ({attempt++})");

		Directory.CreateDirectory(destDir);

		var count = 0;
		foreach (var role in CursorRoles.All)
		{
			var resolvedPath = PresetStore.GetRoleFilePath(preset, role.RegistryName);
			if (resolvedPath == null || !File.Exists(resolvedPath))
				continue;

			var extension = Path.GetExtension(resolvedPath);
			var destPath = Path.Combine(destDir, $"{role.RegistryName}{extension}");
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

		ToastService.Show(RootGrid, Loc.Format(LocToastPresetDownloaded, presetName, count));
	}

	private void StartInlineRename(Preset preset, TextBlock nameText, StackPanel panel)
	{
		var index = panel.Children.IndexOf(nameText);
		if (index < 0)
			return;

		var done = false;

		var textBox = new TextBox
		{
			Text = preset.Name,
			FontSize = nameText.FontSize,
			FontWeight = FontWeights.SemiBold,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(nameText.Margin.Left, nameText.Margin.Top - 2, nameText.Margin.Right, nameText.Margin.Bottom),
			Style = (Style)Application.Current.Resources[StyleTextBox],
			Background = Brush(BrushBg),
			BorderBrush = Brush(BrushAccent),
			BorderThickness = new Thickness(1.5),
			Padding = new Thickness(6, 4, 6, 4),
		};

		void Restore()
		{
			var currentIndex = panel.Children.IndexOf(textBox);
			if (currentIndex < 0)
				return;

			panel.Children.RemoveAt(currentIndex);
			panel.Children.Insert(currentIndex, nameText);
		}

		void Commit()
		{
			if (done)
				return;
			done = true;

			var newName = textBox.Text.Trim();
			Restore();

			if (!string.IsNullOrWhiteSpace(newName) && newName != preset.Name)
			{
				PresetStore.Rename(preset.Id, newName);
				ReloadGallery();
			}
		}

		void Cancel()
		{
			if (done)
				return;
			done = true;
			Restore();
		}

		textBox.PreviewMouseLeftButtonDown += (_, e) => e.Handled = true;
		textBox.PreviewMouseLeftButtonUp += (_, e) => e.Handled = true;
		textBox.KeyDown += (_, e) =>
		{
			if (e.Key == Key.Enter)
			{
				Commit();
				e.Handled = true;
			}
			else if (e.Key == Key.Escape)
			{
				Cancel();
				e.Handled = true;
			}
		};
		textBox.LostFocus += (_, _) => Commit();

		panel.Children.RemoveAt(index);
		panel.Children.Insert(index, textBox);
		textBox.Focus();
		textBox.SelectAll();
	}

	private void ReorderBoardItem(string draggedId, int insertBeforeIndex)
	{
		var draggedIndex = _boardOrderIds.IndexOf(draggedId);
		if (draggedIndex < 0)
			return;

		_boardOrderIds.RemoveAt(draggedIndex);

		if (draggedIndex < insertBeforeIndex)
			insertBeforeIndex--;

		insertBeforeIndex = Math.Clamp(insertBeforeIndex, 0, _boardOrderIds.Count);
		_boardOrderIds.Insert(insertBeforeIndex, draggedId);

		PersistBoardOrder();
	}

	private void ConsolidateGroup(string groupId)
	{
		var group = _groups.FirstOrDefault(candidate => candidate.Id == groupId);
		if (group == null)
			return;

		var groupIndex = _boardOrderIds.IndexOf(groupId);
		if (groupIndex < 0)
			return;

		var memberIds = _boardOrderIds.Where(id => group.MemberPresetIds.Contains(id)).ToList();
		if (memberIds.Count == 0)
			return;

		_boardOrderIds.RemoveAll(id => group.MemberPresetIds.Contains(id));

		groupIndex = _boardOrderIds.IndexOf(groupId);
		_boardOrderIds.InsertRange(groupIndex + 1, memberIds);

		PersistBoardOrder();
		ToastService.Show(RootGrid, Loc.Format(LocGroupToastConsolidated, group.Name));
	}

	private void PersistBoardOrder()
	{
		BoardOrderStore.Save(_boardOrderIds);
		ReloadGallery();
	}

	private void BeginDragGhost(Preset preset, string? previewPath)
	{
		var size = CellSize * _cellScale;
		DragGhost.Width = size;
		DragGhost.Height = size;

		DragGhostImage.Width = CellPreviewSize * _cellScale;
		DragGhostImage.Height = CellPreviewSize * _cellScale;
		RenderOptions.SetBitmapScalingMode(DragGhostImage, BitmapScalingMode.NearestNeighbor);
		CursorPreviewService.ApplyPreview(DragGhostImage, previewPath);

		DragGhostText.Text = preset.Name;
		DragGhostText.FontSize = CellNameFontSize * CellFontScale;

		DragGhost.Visibility = Visibility.Visible;
		_draggedPresetId = preset.Id;
	}

	private void BeginGroupDragGhost(PresetGroup group)
	{
		var size = CellSize * _cellScale;
		DragGhost.Width = size;
		DragGhost.Height = size;

		DragGhostImage.Visibility = Visibility.Collapsed;
		DragGhost.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(GroupColors.ResolveHex(group.ColorKey))!);

		DragGhostText.Text = group.Name;
		DragGhostText.FontSize = CellNameFontSize * CellFontScale;
		DragGhostText.Foreground = System.Windows.Media.Brushes.White;

		DragGhost.Visibility = Visibility.Visible;
		_draggedGroupId = group.Id;
	}

	private void EndDragGhost()
	{
		DragGhost.Visibility = Visibility.Collapsed;
		DragGhost.Background = Brush(BrushSurface);
		DragGhostImage.Visibility = Visibility.Visible;
		DragGhostText.ClearValue(TextBlock.ForegroundProperty);
		ReorderInsertionLine.Visibility = Visibility.Collapsed;
		GroupAttachIndicator.Visibility = Visibility.Collapsed;
		_pendingInsertIndex = null;
		_pendingGroupAttachId = null;
		_draggedPresetId = null;
		_draggedGroupId = null;
	}

	private void UpdateDragGhostPosition(Point positionInRoot)
	{
		DragGhostTransform.X = positionInRoot.X - DragGhost.Width / 2;
		DragGhostTransform.Y = positionInRoot.Y - DragGhost.Height / 2;
	}

	private Rect GetClippedItemBoundsInGallery(FrameworkElement container)
	{
		// Everything here stays in Gallery's own coordinate space (never RootGrid's) so that
		// hit-testing/row-matching can never be thrown off by however Gallery's offset within
		// RootGrid gets computed. The only place that offset matters is the final on-screen
		// placement of the indicators, applied once, right before setting their transform.
		var topLeftInGallery = container.TransformToAncestor(Gallery).Transform(new Point(0, 0));

		return new Rect(topLeftInGallery.X, topLeftInGallery.Y,
			container.RenderSize.Width, container.RenderSize.Height);
	}

	private void UpdateReorderIndicator(Point positionInRoot, Point positionInGallery) =>
		UpdateBoardReorderIndicator(positionInRoot, positionInGallery, _draggedPresetId, allowGroupAttach: true);

	private void UpdateGroupReorderIndicator(Point positionInRoot, Point positionInGallery) =>
		UpdateBoardReorderIndicator(positionInRoot, positionInGallery, _draggedGroupId, allowGroupAttach: false);

	private void UpdateBoardReorderIndicator(Point positionInRoot, Point positionInGallery, string? draggedId, bool allowGroupAttach)
	{
		var galleryOffsetInRoot = positionInRoot - positionInGallery;
		var cells = new List<(BoardEntry entry, Rect bounds)>();

		for (var itemIndex = 0; itemIndex < _boardOrder.Count; itemIndex++)
		{
			if (Gallery.ItemContainerGenerator.ContainerFromIndex(itemIndex + 1) is not FrameworkElement container)
				continue;

			cells.Add((_boardOrder[itemIndex], GetClippedItemBoundsInGallery(container)));
		}

		if (allowGroupAttach)
		{
			var groupHover = cells.FirstOrDefault(cell => cell.entry.Group != null && IsInCenterZone(cell.bounds, positionInGallery));
			if (groupHover.entry?.Group != null)
			{
				SetGroupAttachIndicator(groupHover.entry.Group.Id, groupHover.entry.Group.Collapsed, groupHover.bounds, galleryOffsetInRoot);
				return;
			}
		}

		ClearGroupAttachIndicator();

		if (cells.Count == 0)
		{
			_pendingInsertIndex = 0;
			ReorderInsertionLine.Visibility = Visibility.Collapsed;
			return;
		}

		var rows = cells
			.GroupBy(cell => Math.Round(cell.bounds.Top / ReorderRowGroupingTolerance) * ReorderRowGroupingTolerance)
			.Select(group =>
			{
				var ordered = group.OrderBy(cell => cell.bounds.Left).ToList();
				var top = ordered.Min(c => c.bounds.Top);
				var bottom = ordered.Max(c => c.bounds.Bottom);
				return (top, bottom, cells: ordered);
			})
			.OrderBy(r => r.top)
			.ToList();

		if (rows.Count == 0)
		{
			_pendingInsertIndex = 0;
			ReorderInsertionLine.Visibility = Visibility.Collapsed;
			return;
		}

		var selectedRow = rows.FirstOrDefault(row => positionInGallery.Y >= row.top && positionInGallery.Y <= row.bottom);
		if (selectedRow.cells.Count == 0)
		{
			if (positionInGallery.Y < rows[0].top)
				selectedRow = rows[0];
			else
				selectedRow = rows[^1];
		}

		var rowTop = selectedRow.top;
		var rowHeight = selectedRow.bottom - selectedRow.top;
		var rowCells = selectedRow.cells;

		foreach (var (entry, bounds) in rowCells)
		{
			if (positionInGallery.X >= bounds.Left + bounds.Width / 2)
				continue;

			SetInsertionIndicator(entry.BoardIndex, bounds.Left - CellMargin, rowTop, rowHeight);
			return;
		}

		var lastCell = rowCells[^1];
		SetInsertionIndicator(lastCell.entry.BoardIndex + 1, lastCell.bounds.Right + CellMargin, rowTop, rowHeight);
		return;

		void SetInsertionIndicator(int insertBeforeIndex, double centerXInGallery, double topInGallery, double height)
		{
			var draggedIndex = draggedId != null ? _boardOrderIds.IndexOf(draggedId) : -1;

			if (draggedIndex >= 0 && (insertBeforeIndex == draggedIndex || insertBeforeIndex == draggedIndex + 1))
			{
				_pendingInsertIndex = null;
				ReorderInsertionLine.Visibility = Visibility.Collapsed;
				return;
			}

			_pendingInsertIndex = insertBeforeIndex;
			ReorderInsertionLineTransform.X = centerXInGallery + galleryOffsetInRoot.X - ReorderIndicatorWidth / 2;
			ReorderInsertionLineTransform.Y = topInGallery + galleryOffsetInRoot.Y;
			ReorderInsertionLine.Height = height;
			ReorderInsertionLine.Visibility = Visibility.Visible;
		}
	}

	private static bool IsInCenterZone(Rect bounds, Point point)
	{
		var marginX = bounds.Width * GroupAttachZoneMargin;
		var marginY = bounds.Height * GroupAttachZoneMargin;
		var zone = new Rect(bounds.Left + marginX, bounds.Top + marginY,
			Math.Max(0, bounds.Width - 2 * marginX), Math.Max(0, bounds.Height - 2 * marginY));

		return zone.Contains(point);
	}

	private void SetGroupAttachIndicator(string groupId, bool collapsed, Rect boundsInGallery, Vector galleryOffsetInRoot)
	{
		_pendingGroupAttachId = groupId;
		_pendingInsertIndex = null;
		ReorderInsertionLine.Visibility = Visibility.Collapsed;

		var colorKey = _groups.FirstOrDefault(candidate => candidate.Id == groupId)?.ColorKey;
		GroupAttachIndicator.Stroke = colorKey != null
			? new SolidColorBrush((Color)ColorConverter.ConvertFromString(GroupColors.ResolveHex(colorKey))!)
			: Brush(BrushAccent);

		var cellSize = CellSize * _cellScale;
		var tileTop = collapsed
			? boundsInGallery.Top
			: boundsInGallery.Top + (boundsInGallery.Height - cellSize) / 2;

		GroupAttachIndicatorTransform.X = boundsInGallery.Left + galleryOffsetInRoot.X - GroupOutlinePadding;
		GroupAttachIndicatorTransform.Y = tileTop + galleryOffsetInRoot.Y - GroupOutlinePadding;
		GroupAttachIndicator.Width = cellSize + GroupOutlinePadding * 2;
		GroupAttachIndicator.Height = cellSize + GroupOutlinePadding * 2;
		GroupAttachIndicator.Visibility = Visibility.Visible;
	}

	private void ClearGroupAttachIndicator()
	{
		_pendingGroupAttachId = null;
		GroupAttachIndicator.Visibility = Visibility.Collapsed;
	}

	private void AttachPresetToGroup(string draggedPresetId, string groupId)
	{
		if (!_groups.Any(candidate => candidate.Id == groupId))
			return;

		if (_presetToGroup.TryGetValue(draggedPresetId, out var oldGroup) && oldGroup.Id != groupId)
			GroupStore.RemoveMember(oldGroup.Id, draggedPresetId);

		GroupStore.AddMember(groupId, draggedPresetId);

		var groupIndex = _boardOrderIds.IndexOf(groupId);
		if (groupIndex < 0)
		{
			ReloadGallery();
			return;
		}

		ReorderBoardItem(draggedPresetId, groupIndex + 1);
	}

	private void EditPreset(Preset preset) => OpenEditor(preset, Array.Empty<string>());

	private void OpenEditor(Preset? preset, IReadOnlyList<string> droppedFiles, string? suggestedName = null)
	{
		var editor = new PresetEditorWindow(preset, droppedFiles, suggestedName) { Owner = this };

		if (editor.ShowDialog() == true && editor.Result != null)
		{
			Preset saved;
			try
			{
				saved = PresetStore.Save(editor.Result);
			}
			catch (Exception ex)
			{
				MessageBox.Show(Loc.Format(LocErrorSaveFailed, ex.Message),
					Loc.Get(LocErrorTitle), MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			foreach (var fileName in saved.Roles.Values)
				CursorPreviewService.Invalidate(
					System.IO.Path.Combine(PresetStore.GetFilesDir(saved.Id), fileName));

			if (saved.Id == _activePresetId)
				ApplyPreset(saved, force: true);
			else
				ReloadGallery();

			ToastService.Show(RootGrid, Loc.Get(LocToastSaved));
		}
	}

	private void OnSizeSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (SizeValueText == null)
			return;

		var sizeInPixels = RegistryCursorService.SizeStep + (int)e.NewValue * RegistryCursorService.SizeStep;
		SizeValueText.Text = $"{sizeInPixels} {PixelSuffix}";
		UpdateApplySizeButtonHighlight(sizeInPixels);
	}

	private void UpdateApplySizeButtonHighlight(int sizeInPixels) =>
		ApplySizeButton.Style = (Style)Application.Current.Resources[
			sizeInPixels != _baselineSizePx ? StyleAccentButton : StyleButton];

	private void OnApplySizeButtonClick(object sender, RoutedEventArgs e)
	{
		var sizeInPixels = RegistryCursorService.SizeStep + (int)SizeSlider.Value * RegistryCursorService.SizeStep;
		ApplyAndPersistSize(sizeInPixels);
	}

	public async void ApplyAndPersistSize(int sizeInPixels)
	{
		try
		{
			ShowLoadingOverlay();
			await Dispatcher.Yield(DispatcherPriority.Render);

			await Task.Run(() => RegistryCursorService.SetBaseSize(sizeInPixels));

			if (_activePresetId != null)
			{
				PresetStore.UpdateBaseSize(_activePresetId, sizeInPixels);

				var preset = _presets.FirstOrDefault(preset => preset.Id == _activePresetId);
				if (preset != null)
					preset.BaseSize = sizeInPixels;
			}
			else
			{
				AppState.SetDefaultBaseSize(sizeInPixels);
			}

			if (_activeCellSizeText != null)
				_activeCellSizeText.Text = $"{sizeInPixels} {PixelSuffix}";

			_baselineSizePx = sizeInPixels;
			UpdateApplySizeButtonHighlight(sizeInPixels);

			ToastService.Show(RootGrid, Loc.Get(LocToastSizeApplied));
		}
		catch (Exception exception)
		{
			MessageBox.Show(Loc.Format(LocErrorApplyFailed, exception.Message),
				Loc.Get(LocErrorTitle), MessageBoxButton.OK, MessageBoxImage.Error);
		}
		finally
		{
			HideLoadingOverlay();
		}
	}

	public void SyncSizeSlider(int sizeInPixels) => SetSliderSilently(sizeInPixels);

	private void OnWindowDragOver(object sender, DragEventArgs e)
	{
		if (e.Data.GetDataPresent(PresetDragFormatName))
		{
			var positionInRoot = e.GetPosition(RootGrid);
			var positionInGallery = e.GetPosition(Gallery);
			UpdateDragGhostPosition(positionInRoot);
			UpdateReorderIndicator(positionInRoot, positionInGallery);

			e.Effects = DragDropEffects.Move;
			e.Handled = true;
			return;
		}

		if (e.Data.GetDataPresent(GroupDragFormatName))
		{
			var positionInRoot = e.GetPosition(RootGrid);
			var positionInGallery = e.GetPosition(Gallery);
			UpdateDragGhostPosition(positionInRoot);
			UpdateGroupReorderIndicator(positionInRoot, positionInGallery);

			e.Effects = DragDropEffects.Move;
			e.Handled = true;
			return;
		}

		e.Effects = HasDroppableCursorSource(e) ? DragDropEffects.Copy : DragDropEffects.None;
		e.Handled = true;
	}

	private void OnWindowDragEnter(object sender, DragEventArgs e)
	{
		if (e.Data.GetDataPresent(PresetDragFormatName) || e.Data.GetDataPresent(GroupDragFormatName))
		{
			DragGhost.Visibility = Visibility.Visible;
			return;
		}

		if (HasDroppableCursorSource(e))
			WindowDropIndicator.Visibility = Visibility.Visible;
	}

	private void OnWindowDragLeave(object sender, DragEventArgs e)
	{
		WindowDropIndicator.Visibility = Visibility.Collapsed;

		if (e.Data.GetDataPresent(PresetDragFormatName) || e.Data.GetDataPresent(GroupDragFormatName))
		{
			DragGhost.Visibility = Visibility.Collapsed;
			ReorderInsertionLine.Visibility = Visibility.Collapsed;
			GroupAttachIndicator.Visibility = Visibility.Collapsed;
		}
	}

	private void OnWindowDrop(object sender, DragEventArgs e)
	{
		WindowDropIndicator.Visibility = Visibility.Collapsed;

		if (e.Data.GetData(PresetDragFormatName) is string draggedPresetId)
		{
			e.Handled = true;

			if (_pendingGroupAttachId is { } groupId)
				AttachPresetToGroup(draggedPresetId, groupId);
			else if (_pendingInsertIndex is { } insertIndex)
				ReorderBoardItem(draggedPresetId, insertIndex);

			return;
		}

		if (e.Data.GetData(GroupDragFormatName) is string draggedGroupId)
		{
			e.Handled = true;

			if (_pendingInsertIndex is { } groupInsertIndex)
				ReorderBoardItem(draggedGroupId, groupInsertIndex);

			return;
		}

		if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
			return;

		e.Handled = true;

		// Deferred: opening a modal window (OpenEditor -> ShowDialog) synchronously
		// inside a Drop handler confuses the OS OLE drag-drop loop and leaves the
		// cursor stuck in a "still dragging" state until Alt-Tab/click elsewhere.
		Dispatcher.BeginInvoke(new Action(() => HandleDroppedPaths(paths)), DispatcherPriority.Input);
	}

	private void HandleDroppedPaths(string[] paths)
	{
		var packagePath = paths.FirstOrDefault(path => File.Exists(path) && PresetPackageService.IsSupportedPackageFile(path));
		if (packagePath != null)
		{
			DetectedPackage? detected;
			try
			{
				detected = PresetPackageService.TryDetectPackage(packagePath);
			}
			catch (PackageVersionUnsupportedException exception)
			{
				MessageBox.Show(Loc.Format(LocErrorImportVersionUnsupported, exception.FoundVersion, exception.MaxSupportedVersion),
					Loc.Get(LocErrorTitle), MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			if (detected != null)
			{
				ImportPackage(detected);
				return;
			}
		}

		List<string> files;
		try
		{
			files = ResolveCursorFiles(paths);
		}
		catch (Exception exception)
		{
			MessageBox.Show(Loc.Format(LocErrorArchiveExtractFailed, exception.Message),
				Loc.Get(LocErrorTitle), MessageBoxButton.OK, MessageBoxImage.Error);
			return;
		}

		if (files.Count == 0)
			return;

		OpenEditor(null, files, GetSuggestedPresetName(paths));
	}

	private static string? GetSuggestedPresetName(IEnumerable<string> paths)
	{
		var folder = paths.FirstOrDefault(Directory.Exists);

		if (folder != null)
			return System.IO.Path.GetFileName(folder);

		var archive = paths.FirstOrDefault(ArchiveImportService.IsArchiveFile);

		return archive != null ? System.IO.Path.GetFileNameWithoutExtension(archive) : null;
	}

	private static bool HasDroppableCursorSource(DragEventArgs e)
	{
		if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
			return false;

		return paths.Any(path =>
			Directory.Exists(path) || ArchiveImportService.IsArchiveFile(path) ||
			PresetPackageService.IsSupportedPackageFile(path) || IsCursorFile(path));
	}

	private static List<string> ResolveCursorFiles(IEnumerable<string> paths)
	{
		var result = new List<string>();

		foreach (var path in paths)
		{
			if (Directory.Exists(path))
				result.AddRange(Directory.EnumerateFiles(path, FileSearchPattern, SearchOption.TopDirectoryOnly)
					.Where(IsCursorFile));
			else if (ArchiveImportService.IsArchiveFile(path))
				result.AddRange(Directory.EnumerateFiles(
						ArchiveImportService.ExtractToTempFolder(path), FileSearchPattern, SearchOption.AllDirectories)
					.Where(IsCursorFile));
			else if (IsCursorFile(path))
				result.Add(path);
		}

		return result;
	}

	private static bool IsCursorFile(string path)
	{
		var extension = System.IO.Path.GetExtension(path).ToLowerInvariant();

		return extension is CurExtension or AniExtension;
	}

	protected override void OnClosed(EventArgs e)
	{
		AppState.SetMainWindowSize(Width, Height);
		base.OnClosed(e);
	}
}

public sealed class RelayUiCommand(Action execute) : ICommand
{
	public event EventHandler? CanExecuteChanged { add { } remove { } }
	public bool CanExecute(object? parameter) => true;
	public void Execute(object? parameter) => execute();
}
