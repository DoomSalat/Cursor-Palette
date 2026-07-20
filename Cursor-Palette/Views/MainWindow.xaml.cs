using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CursorPalette.Models;
using CursorPalette.Services;

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

	private const string BrushTextDim = "Brush.TextDim";
	private const string BrushBg = "Brush.Bg";
	private const string BrushAccent = "Brush.Accent";
	private const string BrushBorder = "Brush.Border";
	private const string BrushSurface = "Brush.Surface";
	private const string BrushSurfaceHover = "Brush.SurfaceHover";

	private const string LocWindowsDefault = "S.WindowsDefault";
	private const string LocMenuEdit = "S.Menu.Edit";
	private const string LocMenuDelete = "S.Menu.Delete";
	private const string LocAddPreset = "S.AddPreset";
	private const string LocAddPresetHint = "S.AddPreset.Hint";
	private const string LocErrorApplyFailed = "S.Error.ApplyFailed";
	private const string LocErrorTitle = "S.Error.Title";
	private const string LocErrorSaveFailed = "S.Error.SaveFailed";
	private const string LocConfirmDeleteText = "S.ConfirmDelete.Text";
	private const string LocConfirmDeleteTitle = "S.ConfirmDelete.Title";

	private const double CellSize = 148;
	private const double CellMargin = 6;
	private const double CellCornerRadius = 10;
	private const double CellBorderThickness = 2;
	private const double CellPreviewSize = 48;
	private const double CellNameFontSize = 13;
	private const double CellCountFontSize = 11;
	private const double CellSizeFontSize = 11;
	private const double AddCellPlusFontSize = 34;

	private const double UiZoomStep = 0.1;
	private const string ThemeIconDark = "🌙";
	private const string ThemeIconLight = "☀";

	private List<Preset> _presets = new();
	private string? _activePresetId;
	private TextBlock? _activeCellSizeText;
	private double _cellScale = AppState.GalleryCellScaleDefault;
	private double _uiScale = AppState.UiScaleDefault;
	private bool _cellScaleReady;

	public MainWindow()
	{
		InitializeComponent();

		_activePresetId = AppState.GetActivePresetId();

		SetSliderSilently(RegistryCursorService.GetBaseSize());

		_uiScale = AppState.GetUiScale();
		ApplyUiScale(_uiScale);

		_cellScale = AppState.GetGalleryCellScale();
		SetCellScaleSliderSilently(_cellScale);

		UpdateThemeToggleIcon();

		ReloadGallery();
		UpdateUndoButton();

		var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? AppInfo.DefaultVersion;
		FooterRun.Text = string.Format(FooterFormat, AppInfo.Author, version, AppInfo.LicenseName);
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

	private void SetSliderSilently(int sizePx)
	{
		SizeSlider.Value = (sizePx - RegistryCursorService.SizeStep) / (double)RegistryCursorService.SizeStep;
		SizeValueText.Text = $"{sizePx} {PixelSuffix}";
	}

	// ---- зум интерфейса (шапка/тулбар/футер/галерея целиком) ----

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

	// ---- размер ячеек галереи ----

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

	// ---- тема ----

	private void UpdateThemeToggleIcon() =>
		ThemeToggleIcon.Text = ThemeManager.Current == ThemeManager.Dark ? ThemeIconDark : ThemeIconLight;

	private void OnThemeToggleClick(object sender, RoutedEventArgs e)
	{
		var next = ThemeManager.Current == ThemeManager.Dark ? ThemeManager.Light : ThemeManager.Dark;
		ThemeManager.SetTheme(next);

		// RestoreBounds — позиция/размер окна вне свёрнутого/развёрнутого
		// состояния; читаем её независимо от текущего WindowState, чтобы
		// корректно перенести и обычное, и развёрнутое окно.
		var wasMaximized = WindowState == WindowState.Maximized;
		var bounds = RestoreBounds;

		// StaticResource в уже загруженной разметке не обновляется на лету —
		// пересоздаём окно, чтобы все стили резолвились против новой темы.
		// Позицию/размер переносим вручную — иначе новое окно всплывает
		// в дефолтном месте экрана.
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

	private void ReloadGallery()
	{
		_presets = PresetStore.LoadAll();
		Gallery.Items.Clear();
		_activeCellSizeText = null;

		if (_activePresetId != null && _presets.All(p => p.Id != _activePresetId))
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

	// Диапазон масштаба сетки широкий (0.5–3.5x), но текст растущий линейно
	// с ним на больших значениях выглядит непропорционально огромным рядом
	// с превью — поэтому шрифты растут медленнее самой ячейки.
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
			Source = CursorPreviewService.GetPreview(previewPath),
			SnapsToDevicePixels = true,
		};
		RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);

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
							?? preset.Roles.Keys.Select(r => PresetStore.GetRoleFilePath(preset, r))
								.FirstOrDefault(p => p != null);

		var image = new Image
		{
			Width = CellPreviewSize * _cellScale,
			Height = CellPreviewSize * _cellScale,
			Source = CursorPreviewService.GetPreview(previewPath),
			SnapsToDevicePixels = true,
		};
		RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);

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
			Text = $"{preset.Roles.Count}/{CursorRoles.All.Length}",
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

		var cell = new Border
		{
			Width = CellSize * _cellScale,
			Height = CellSize * _cellScale,
			Margin = new Thickness(CellMargin),
			CornerRadius = new CornerRadius(CellCornerRadius),
			Background = Brush(BrushSurface),
			BorderThickness = new Thickness(CellBorderThickness),
			BorderBrush = isActive ? Brush(BrushAccent) : Brush(BrushBorder),
			Child = panel,
			Cursor = Cursors.Hand,
			Tag = preset,
		};

		cell.MouseEnter += (_, _) =>
		{
			if (preset.Id != _activePresetId) cell.Background = Brush(BrushSurfaceHover);
		};
		cell.MouseLeave += (_, _) => cell.Background = Brush(BrushSurface);
		cell.MouseLeftButtonUp += (_, _) => ApplyPreset(preset);
		cell.MouseRightButtonUp += (_, e) =>
		{
			cell.ContextMenu!.IsOpen = true;
			e.Handled = true;
		};

		var menu = new ContextMenu();
		var editItem = new MenuItem { Header = Loc.Get(LocMenuEdit) };
		editItem.Click += (_, _) => EditPreset(preset);
		var deleteItem = new MenuItem { Header = Loc.Get(LocMenuDelete) };
		deleteItem.Click += (_, _) => DeletePreset(preset);
		menu.Items.Add(editItem);
		menu.Items.Add(deleteItem);
		cell.ContextMenu = menu;
		cell.MouseLeftButtonDown += (_, _) => { };

		var editHint = new ToolTip { Content = preset.Name };
		cell.ToolTip = editHint;

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

	private void ApplyPreset(Preset preset)
	{
		try
		{
			RegistryCursorService.SaveSnapshotToDisk(RegistryCursorService.TakeSnapshot());

			var values = new Dictionary<string, string>();
			foreach (var role in CursorRoles.All)
			{
				var path = PresetStore.GetRoleFilePath(preset, role.RegistryName);
				values[role.RegistryName] = path != null && File.Exists(path) ? path : EmptyValue;
			}

			RegistryCursorService.ApplyValues(values);
			RegistryCursorService.SetBaseSize(preset.BaseSize);
			SetSliderSilently(preset.BaseSize);
			_activePresetId = preset.Id;
			AppState.SetActivePresetId(preset.Id);

			ReloadGallery();
			UpdateUndoButton();
		}
		catch (Exception ex)
		{
			MessageBox.Show(Loc.Format(LocErrorApplyFailed, ex.Message),
				Loc.Get(LocErrorTitle), MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	private void ApplyDefault()
	{
		try
		{
			RegistryCursorService.SaveSnapshotToDisk(RegistryCursorService.TakeSnapshot());

			var defaultSize = AppState.GetDefaultBaseSize();
			RegistryCursorService.ApplyValues(RegistryCursorService.GetWindowsDefaultValues());
			RegistryCursorService.SetBaseSize(defaultSize);

			_activePresetId = null;
			AppState.SetActivePresetId(null);

			SetSliderSilently(defaultSize);

			ReloadGallery();
			UpdateUndoButton();
		}
		catch (Exception ex)
		{
			MessageBox.Show(Loc.Format(LocErrorApplyFailed, ex.Message),
				Loc.Get(LocErrorTitle), MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	private void OnUndoButtonClick(object sender, RoutedEventArgs e)
	{
		var snapshot = RegistryCursorService.LoadSnapshotFromDisk();

		if (snapshot == null)
			return;

		try
		{
			RegistryCursorService.SaveSnapshotToDisk(RegistryCursorService.TakeSnapshot());
			RegistryCursorService.RestoreSnapshot(snapshot);

			_activePresetId = FindPresetIdByValues(snapshot.Values);
			AppState.SetActivePresetId(_activePresetId);

			SetSliderSilently(snapshot.BaseSize);

			ReloadGallery();
			UpdateUndoButton();
		}
		catch (Exception ex)
		{
			MessageBox.Show(Loc.Format(LocErrorApplyFailed, ex.Message),
				Loc.Get(LocErrorTitle), MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	private string? FindPresetIdByValues(IReadOnlyDictionary<string, string> values)
	{
		if (!values.TryGetValue(CursorRoles.ArrowRoleName, out var arrow) || string.IsNullOrEmpty(arrow))
			return null;

		return _presets.FirstOrDefault(p =>
			string.Equals(PresetStore.GetRoleFilePath(p, CursorRoles.ArrowRoleName), arrow,
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

	private void EditPreset(Preset preset) => OpenEditor(preset, Array.Empty<string>());

	private void OpenEditor(Preset? preset, IReadOnlyList<string> droppedFiles)
	{
		var editor = new PresetEditorWindow(preset, droppedFiles) { Owner = this };

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
				ApplyPreset(saved);
			else
				ReloadGallery();
		}
	}

	private void OnSizeSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (SizeValueText == null)
			return;

		var sizePx = RegistryCursorService.SizeStep + (int)e.NewValue * RegistryCursorService.SizeStep;
		SizeValueText.Text = $"{sizePx} {PixelSuffix}";
	}

	private void OnApplySizeButtonClick(object sender, RoutedEventArgs e)
	{
		var sizePx = RegistryCursorService.SizeStep + (int)SizeSlider.Value * RegistryCursorService.SizeStep;
		ApplyAndPersistSize(sizePx);
	}

	public void ApplyAndPersistSize(int sizePx)
	{
		try
		{
			RegistryCursorService.SetBaseSize(sizePx);

			if (_activePresetId != null)
			{
				PresetStore.UpdateBaseSize(_activePresetId, sizePx);

				var preset = _presets.FirstOrDefault(p => p.Id == _activePresetId);
				if (preset != null)
					preset.BaseSize = sizePx;
			}
			else
			{
				AppState.SetDefaultBaseSize(sizePx);
			}

			if (_activeCellSizeText != null)
				_activeCellSizeText.Text = $"{sizePx} {PixelSuffix}";
		}
		catch (Exception ex)
		{
			MessageBox.Show(Loc.Format(LocErrorApplyFailed, ex.Message),
				Loc.Get(LocErrorTitle), MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	public void SyncSizeSlider(int sizePx) => SetSliderSilently(sizePx);

	private void OnWindowDragOver(object sender, DragEventArgs e)
	{
		e.Effects = GetDroppedCursorFiles(e).Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;
		e.Handled = true;
	}

	private void OnWindowDrop(object sender, DragEventArgs e)
	{
		var files = GetDroppedCursorFiles(e);

		if (files.Count == 0)
			return;

		e.Handled = true;
		OpenEditor(null, files);
	}

	private static List<string> GetDroppedCursorFiles(DragEventArgs e)
	{
		var result = new List<string>();

		if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
			return result;

		foreach (var path in paths)
		{
			if (Directory.Exists(path))
				result.AddRange(Directory.EnumerateFiles(path, FileSearchPattern, SearchOption.TopDirectoryOnly)
					.Where(IsCursorFile));
			else if (IsCursorFile(path))
				result.Add(path);
		}

		return result;
	}

	private static bool IsCursorFile(string path)
	{
		var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();

		return ext is CurExtension or AniExtension;
	}
}

public sealed class RelayUiCommand(Action execute) : ICommand
{
	public event EventHandler? CanExecuteChanged { add { } remove { } }
	public bool CanExecute(object? parameter) => true;
	public void Execute(object? parameter) => execute();
}
