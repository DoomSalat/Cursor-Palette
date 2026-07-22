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
	private const double ReorderIndicatorWidth = 4;
	private const double ReorderRowGroupingTolerance = 1;

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

	private const string LocInfoTitle = "S.Info.Title";
	private const string LocInfoMain = "S.Info.Main";
	private const string LocErrorArchiveExtractFailed = "S.Error.ArchiveExtractFailed";

	private const double UiZoomStep = 0.1;
	private const string ThemeIconDark = "🌙";
	private const string ThemeIconLight = "☀";

	private List<Preset> _presets = new();
	private string? _activePresetId;
	private TextBlock? _activeCellSizeText;
	private double _cellScale = AppState.GalleryCellScaleDefault;
	private double _uiScale = AppState.UiScaleDefault;
	private bool _cellScaleReady;
	private int _baselineSizePx;
	private Point? _presetDragStartPoint;
	private bool _justDraggedPreset;
	private int? _pendingInsertIndex;
	private string? _draggedPresetId;

	public MainWindow()
	{
		InitializeComponent();

		_activePresetId = AppState.GetActivePresetId();

		_baselineSizePx = RegistryCursorService.GetBaseSize();
		SetSliderSilently(_baselineSizePx);

		_uiScale = AppState.GetUiScale();
		ApplyUiScale(_uiScale);

		_cellScale = AppState.GetGalleryCellScale();
		SetCellScaleSliderSilently(_cellScale);

		UpdateThemeToggleIcon();
		UpdateLanguageButtonText();

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

	private void OnExportButtonClick(object sender, RoutedEventArgs e)
	{
		new ExportWindow(_presets) { Owner = this }.ShowDialog();
	}

	private void OnImportButtonClick(object sender, RoutedEventArgs e)
	{
		var dialog = new OpenFileDialog
		{
			Filter = Loc.Get(LocImportFileFilter),
			CheckFileExists = true,
		};

		if (dialog.ShowDialog(this) != true)
			return;

		var detected = PresetPackageService.TryDetectPackage(dialog.FileName);
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
		var picker = new ImportPickerWindow(detected.Entries) { Owner = this };

		if (picker.ShowDialog() == true)
		{
			var imported = PresetPackageService.ImportSelected(detected, picker.SelectedEntries);
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
		Gallery.Items.Clear();
		_activeCellSizeText = null;

		if (_activePresetId != null && _presets.All(preset => preset.Id != _activePresetId))
		{
			_activePresetId = null;
			AppState.SetActivePresetId(null);
		}

		Gallery.Items.Add(CreateDefaultCell());

		foreach (var preset in _presets)
			Gallery.Items.Add(CreatePresetCell(preset));

		Gallery.Items.Add(CreateAddCell());
		EmptyHint.Visibility = _presets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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

	private FrameworkElement CreatePresetCell(Preset preset)
	{
		var isActive = preset.Id == _activePresetId;

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

		var cell = new Border
		{
			Width = CellSize * _cellScale,
			Height = CellSize * _cellScale,
			Margin = new Thickness(CellMargin),
			CornerRadius = new CornerRadius(CellCornerRadius),
			Background = Brush(BrushSurface),
			BorderThickness = new Thickness(CellBorderThickness),
			BorderBrush = isActive ? Brush(BrushAccent) : Brush(BrushBorder),
			Child = cellContent,
			Cursor = Cursors.Hand,
			Tag = preset,
		};

		cell.MouseEnter += (_, _) =>
		{
			if (preset.Id != _activePresetId) cell.Background = Brush(BrushSurfaceHover);
		};
		cell.MouseLeave += (_, _) => cell.Background = Brush(BrushSurface);
		cell.MouseLeftButtonDown += (_, e) => _presetDragStartPoint = e.GetPosition(cell);
		cell.MouseMove += (_, e) =>
		{
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

		var presetIndex = _presets.FindIndex(candidate => candidate.Id == preset.Id);
		var isFirst = presetIndex <= 0;
		var isLast = presetIndex < 0 || presetIndex >= _presets.Count - 1;

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

		return cell;
	}

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
		var index = _presets.FindIndex(candidate => candidate.Id == preset.Id);
		if (index < 0)
			return;

		var targetIndex = index + direction;
		if (targetIndex < 0 || targetIndex >= _presets.Count)
			return;

		(_presets[index], _presets[targetIndex]) = (_presets[targetIndex], _presets[index]);
		PersistPresetOrder();
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

	private void ReorderPreset(string draggedPresetId, int insertBeforeIndex)
	{
		var draggedIndex = _presets.FindIndex(preset => preset.Id == draggedPresetId);
		if (draggedIndex < 0)
			return;

		var dragged = _presets[draggedIndex];
		_presets.RemoveAt(draggedIndex);

		if (draggedIndex < insertBeforeIndex)
			insertBeforeIndex--;

		insertBeforeIndex = Math.Clamp(insertBeforeIndex, 0, _presets.Count);
		_presets.Insert(insertBeforeIndex, dragged);

		PersistPresetOrder();
	}

	private void PersistPresetOrder()
	{
		PresetStore.Reorder(_presets.Select(preset => preset.Id).ToList());
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

	private void EndDragGhost()
	{
		DragGhost.Visibility = Visibility.Collapsed;
		ReorderInsertionLine.Visibility = Visibility.Collapsed;
		_pendingInsertIndex = null;
		_draggedPresetId = null;
	}

	private void UpdateDragGhostPosition(Point positionInRoot)
	{
		DragGhostTransform.X = positionInRoot.X - DragGhost.Width / 2;
		DragGhostTransform.Y = positionInRoot.Y - DragGhost.Height / 2;
	}

	private void UpdateReorderIndicator(Point positionInRoot)
	{
		var cells = new List<(int presetIndex, Rect bounds)>();

		for (var presetIndex = 0; presetIndex < _presets.Count; presetIndex++)
		{
			if (Gallery.ItemContainerGenerator.ContainerFromIndex(presetIndex + 1) is not FrameworkElement container)
				continue;

			var topLeft = container.TransformToAncestor(RootGrid).Transform(new Point(0, 0));
			cells.Add((presetIndex, new Rect(topLeft, container.RenderSize)));
		}

		if (cells.Count == 0)
		{
			_pendingInsertIndex = 0;
			ReorderInsertionLine.Visibility = Visibility.Collapsed;
			return;
		}

		var rowTop = cells
			.Select(cell => cell.bounds)
			.OrderBy(bounds => Math.Abs(bounds.Top + bounds.Height / 2 - positionInRoot.Y))
			.First()
			.Top;

		var rowCells = cells
			.Where(cell => Math.Abs(cell.bounds.Top - rowTop) < ReorderRowGroupingTolerance)
			.OrderBy(cell => cell.bounds.Left)
			.ToList();

		foreach (var (presetIndex, bounds) in rowCells)
		{
			if (positionInRoot.X >= bounds.Left + bounds.Width / 2)
				continue;

			SetInsertionIndicator(presetIndex, bounds.Left - CellMargin, bounds.Top, bounds.Height);
			return;
		}

		var lastCellBounds = rowCells[^1].bounds;
		SetInsertionIndicator(rowCells[^1].presetIndex + 1, lastCellBounds.Right + CellMargin, lastCellBounds.Top, lastCellBounds.Height);
		return;

		void SetInsertionIndicator(int insertBeforeIndex, double centerX, double top, double height)
		{
			var draggedIndex = _draggedPresetId != null
				? _presets.FindIndex(preset => preset.Id == _draggedPresetId)
				: -1;

			if (draggedIndex >= 0 && (insertBeforeIndex == draggedIndex || insertBeforeIndex == draggedIndex + 1))
			{
				_pendingInsertIndex = null;
				ReorderInsertionLine.Visibility = Visibility.Collapsed;
				return;
			}

			_pendingInsertIndex = insertBeforeIndex;
			ReorderInsertionLineTransform.X = centerX - ReorderIndicatorWidth / 2;
			ReorderInsertionLineTransform.Y = top;
			ReorderInsertionLine.Height = height;
			ReorderInsertionLine.Visibility = Visibility.Visible;
		}
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
			UpdateDragGhostPosition(positionInRoot);
			UpdateReorderIndicator(positionInRoot);

			e.Effects = DragDropEffects.Move;
			e.Handled = true;
			return;
		}

		e.Effects = HasDroppableCursorSource(e) ? DragDropEffects.Copy : DragDropEffects.None;
		e.Handled = true;
	}

	private void OnWindowDragEnter(object sender, DragEventArgs e)
	{
		if (e.Data.GetDataPresent(PresetDragFormatName))
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

		if (e.Data.GetDataPresent(PresetDragFormatName))
		{
			DragGhost.Visibility = Visibility.Collapsed;
			ReorderInsertionLine.Visibility = Visibility.Collapsed;
		}
	}

	private void OnWindowDrop(object sender, DragEventArgs e)
	{
		WindowDropIndicator.Visibility = Visibility.Collapsed;

		if (e.Data.GetData(PresetDragFormatName) is string draggedPresetId)
		{
			e.Handled = true;

			if (_pendingInsertIndex is { } insertIndex)
				ReorderPreset(draggedPresetId, insertIndex);

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
			var detected = PresetPackageService.TryDetectPackage(packagePath);
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
}

public sealed class RelayUiCommand(Action execute) : ICommand
{
	public event EventHandler? CanExecuteChanged { add { } remove { } }
	public bool CanExecute(object? parameter) => true;
	public void Execute(object? parameter) => execute();
}
