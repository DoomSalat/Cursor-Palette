using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CursorPalette.Linux.Services;
using CursorPalette.Linux.ViewModels;
using CursorPalette.Models;
using CursorPalette.Services;

namespace CursorPalette.Linux.Views;

public partial class MainWindow : Window
{
	private const string PixelSuffix = "px";
	private const string FooterFormat = "{0}  ·  v{1}  ·  {2}";
	private const string EmptyValue = "";
	private const string CursorFileFilterName = "Cursors";
	private const string DeleteButtonText = "Delete";
	private const string CancelButtonText = "Cancel";
	private const string AddCellPlusText = "+";
	private const string ThemeIconDark = "🌙";
	private const string ThemeIconLight = "☀";
	private const string MixedBadgeText = "🧩";
	private const string PresetDragFormat = "application/x-cursor-palette-preset";

	private const string LocApplySize = "S.ApplySize";
	private const string LocUndo = "S.Undo";
	private const string LocResetDefault = "S.ResetDefault";
	private const string LocConfirmDeleteTitle = "S.ConfirmDelete.Title";
	private const string LocConfirmDeleteText = "S.ConfirmDelete.Text";
	private const string LocEditorCancel = "S.Editor.Cancel";
	private const string LocMenuEdit = "S.Menu.Edit";
	private const string LocMenuRename = "S.Menu.Rename";
	private const string LocMenuMoveLeft = "S.Menu.MoveLeft";
	private const string LocMenuMoveRight = "S.Menu.MoveRight";
	private const string LocMenuDownload = "S.Menu.Download";
	private const string LocMenuDelete = "S.Menu.Delete";
	private const string LocMenuToggleCollapse = "S.Menu.ToggleCollapse";
	private const string LocRenameTitle = "S.Menu.Rename";
	private const string LocEditorSave = "S.Editor.Save";
	private const string LocEmptyGallery = "S.EmptyGallery";
	private const string LocToastSaved = "S.Toast.Saved";
	private const string LocToastSizeApplied = "S.Toast.SizeApplied";
	private const string LocAboutTitle = "S.About.Title";
	private const string LocAboutClose = "S.About.Close";
	private const string LocAboutLicenseHint = "S.About.LicenseHint";
	private const string LocToastUpdateAvailable = "S.Toast.UpdateAvailable";

	private const double DialogMargin = 16;
	private const double DialogSpacing = 12;
	private const double ButtonSpacing = 8;
	private const double DeleteDialogWidth = 360;
	private const double DeleteDialogHeight = 160;
	private const double UiZoomStep = 0.1;

	private static readonly string[] SupportedLanguages = { "en", "ru", "de", "es", "ja", "zh" };
	private static readonly string[] CursorFilePatterns = { "*.cur", "*.ani", "*.png", "*.jpg", "*.bmp", "*.gif" };

	private readonly MainWindowViewModel _viewModel = new();
	private Slider? _sizeSlider;
	private TextBlock? _sizeValueText;
	private Button? _applySizeButton;
	private Button? _languageButton;
	private TextBlock? _zoomText;
	private double _uiScale = 1.0;
	private Slider? _cellScaleSlider;
	private TextBlock? _cellScaleValueText;
	private double _cellScale = 1.0;
	private Border? _loadingOverlay;
	private DispatcherTimer? _loadingSpinnerTimer;
	private Point? _presetDragStartPoint;
	private string? _draggedPresetId;

	public MainWindow()
	{
		InitializeComponent();
		DataContext = _viewModel;
		_viewModel.Initialize();

		_sizeSlider = this.FindControl<Slider>("SizeSlider");
		_sizeValueText = this.FindControl<TextBlock>("SizeValueText");
		_applySizeButton = this.FindControl<Button>("ApplySizeButton");
		_languageButton = this.FindControl<Button>("LanguageButton");
		_zoomText = this.FindControl<TextBlock>("ZoomText");
		_cellScaleSlider = this.FindControl<Slider>("CellScaleSlider");
		_cellScaleValueText = this.FindControl<TextBlock>("CellScaleValueText");

		if (_sizeSlider != null)
		{
			_sizeSlider.Value = _viewModel.BaselineSizePx;
			_sizeSlider.PropertyChanged += OnSizeSliderChanged;
		}

		if (_applySizeButton != null)
			_applySizeButton.Click += OnApplySizeClick;

		UpdateSizeText(_viewModel.BaselineSizePx);
		ApplyLocalization();
		UpdateThemeToggleIcon();

		var currentLang = LocalizationManager.Current;
		_languageIndex = Math.Max(0, Array.IndexOf(SupportedLanguages, currentLang));
		if (_languageButton != null)
			_languageButton.Content = SupportedLanguages[_languageIndex].ToUpperInvariant();

		AddHandler(DragDrop.DropEvent, OnDrop);
		AddHandler(DragDrop.DragOverEvent, OnDragOver);
		AddHandler(PointerMovedEvent, OnPresetPointerMoved, RoutingStrategies.Bubble);
		AddHandler(PointerReleasedEvent, OnPresetPointerReleased, RoutingStrategies.Bubble);
		AddHandler(DragDrop.DragOverEvent, OnPresetDragOver, RoutingStrategies.Bubble, handledEventsToo: true);
		AddHandler(DragDrop.DropEvent, OnPresetDrop, RoutingStrategies.Bubble, handledEventsToo: true);

		_uiScale = AppState.GetUiScale();
		ApplyUiScale(_uiScale);

		_cellScale = AppState.GetGalleryCellScale();
		if (_cellScaleSlider != null)
		{
			_cellScaleSlider.Value = _cellScale;
			_cellScaleSlider.PropertyChanged += OnCellScaleSliderChanged;
		}

		ApplyCellScale(_cellScale);

		_ = CheckForUpdatesAsync();
	}

	private async Task CheckForUpdatesAsync()
	{
		var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? AppInfo.DefaultVersion;
		var isAvailable = await UpdateChecker.IsUpdateAvailableAsync(version);
		if (isAvailable)
			ShowToast(Loc.Format(LocToastUpdateAvailable, version));
	}

	private void ApplyUiScale(double scale)
	{
		var rootGrid = this.FindControl<Grid>("RootGrid");
		if (rootGrid != null)
		{
			rootGrid.RenderTransform = new ScaleTransform(scale, scale);
			rootGrid.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
		}

		if (_zoomText != null)
			_zoomText.Text = $"{(int)Math.Round(scale * 100)}%";
	}

	public void OnZoomInClick(object? sender, RoutedEventArgs e) => AdjustUiZoom(UiZoomStep);
	public void OnZoomOutClick(object? sender, RoutedEventArgs e) => AdjustUiZoom(-UiZoomStep);

	private void AdjustUiZoom(double delta)
	{
		_uiScale = Math.Clamp(Math.Round(_uiScale + delta, 2), AppState.UiScaleMin, AppState.UiScaleMax);

		ApplyUiScale(_uiScale);
		AppState.SetUiScale(_uiScale);
	}

	private void ApplyCellScale(double scale)
	{
		var gallery = this.FindControl<ItemsControl>("Gallery");
		if (gallery != null)
		{
			gallery.RenderTransform = new ScaleTransform(scale, scale);
			gallery.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
		}

		if (_cellScaleValueText != null)
			_cellScaleValueText.Text = $"{(int)Math.Round(scale * 100)}%";
	}

	private void OnCellScaleSliderChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
	{
		if (_cellScaleSlider == null)
			return;

		if (e.Property != Slider.ValueProperty)
			return;

		_cellScale = _cellScaleSlider.Value;
		ApplyCellScale(_cellScale);
		AppState.SetGalleryCellScale(_cellScale);
	}

	private void ShowLoadingOverlay()
	{
		if (_loadingOverlay != null)
		{
			_loadingOverlay.IsVisible = true;
			return;
		}

		var spinner = new Ellipse
		{
			Width = 32,
			Height = 32,
			Stroke = Brushes.CornflowerBlue,
			StrokeThickness = 3,
			StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 28, 9 },
			RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
			RenderTransform = new RotateTransform(0),
		};

		_loadingSpinnerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
		var angle = 0d;
		_loadingSpinnerTimer.Tick += (_, _) =>
		{
			angle = (angle + 6) % 360;
			spinner.RenderTransform = new RotateTransform(angle);
		};

		_loadingOverlay = new Border
		{
			Background = new SolidColorBrush(0xB3000000),
			IsHitTestVisible = false,
			IsVisible = true,
			ZIndex = 3000,
			Child = spinner,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch,
		};

		var root = this.FindControl<Panel>("RootGrid");
		root?.Children.Add(_loadingOverlay);
		_loadingSpinnerTimer.Start();
	}

	private void HideLoadingOverlay()
	{
		if (_loadingSpinnerTimer != null)
		{
			_loadingSpinnerTimer.Stop();
			_loadingSpinnerTimer = null;
		}

		if (_loadingOverlay != null)
		{
			_loadingOverlay.IsVisible = false;
			var root = this.FindControl<Panel>("RootGrid");
			root?.Children.Remove(_loadingOverlay);
			_loadingOverlay = null;
		}
	}

	private void ApplyLocalization()
	{
		if (_applySizeButton != null)
			_applySizeButton.Content = Loc.Get(LocApplySize);

		var undoButton = this.FindControl<Button>("UndoButton");
		if (undoButton != null)
			undoButton.Content = Loc.Get(LocUndo);

		var defaultLabel = this.FindControl<TextBlock>("DefaultLabel");
		if (defaultLabel != null)
			defaultLabel.Text = Loc.Get(LocResetDefault);

		var emptyHint = this.FindControl<TextBlock>("EmptyGalleryHint");
		if (emptyHint != null)
			emptyHint.Text = Loc.Get(LocEmptyGallery);
	}

	private void OnContextMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
	{
		if (sender is not ContextMenu menu)
			return;

		var items = menu.Items;
		if (items == null)
			return;

		var labels = new[]
		{
			Loc.Get(LocMenuEdit),
			Loc.Get(LocMenuRename),
			Loc.Get(LocMenuMoveLeft),
			Loc.Get(LocMenuMoveRight),
			Loc.Get(LocMenuDownload),
			Loc.Get(LocMenuToggleCollapse),
			Loc.Get(LocMenuDelete),
		};

		var index = 0;
		foreach (var item in items)
		{
			if (item is MenuItem menuItem && index < labels.Length)
				menuItem.Header = labels[index];
			index++;
		}
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	private void OnSizeSliderChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
	{
		if (_sizeSlider == null)
			return;

		var size = (int)Math.Round(_sizeSlider.Value);
		UpdateSizeText(size);
	}

	private void UpdateSizeText(int size)
	{
		if (_sizeValueText != null)
			_sizeValueText.Text = $"{size} {PixelSuffix}";
	}

	private async void OnApplySizeClick(object? sender, RoutedEventArgs e)
	{
		if (_sizeSlider == null)
			return;

		var size = (int)Math.Round(_sizeSlider.Value);
		ShowLoadingOverlay();
		try
		{
			await _viewModel.ApplySizeAsync(size);
			ShowToast(Loc.Get(LocToastSizeApplied));
		}
		finally
		{
			HideLoadingOverlay();
		}
	}

	public async void OnPresetClick(object? sender, PointerPressedEventArgs e)
	{
		if (sender is not Control control || control.DataContext is not BoardItem item)
			return;

		if (item.IsAddCell)
		{
			await OpenPresetEditorForNew();
			return;
		}

		if (item.IsDefaultCell)
		{
			ApplyDefault();
			return;
		}

		if (item.IsGroup && item.Group != null)
		{
			_viewModel.ToggleGroupCollapse(item.Group.Id);
			return;
		}

		if (item.IsPreset && item.Preset != null)
		{
			_presetDragStartPoint = e.GetPosition(control);
			_draggedPresetId = item.Preset.Id;
		}
	}

	public async void OnPresetPointerMoved(object? sender, PointerEventArgs e)
	{
		if (_presetDragStartPoint is not { } start || _draggedPresetId == null)
			return;

		if (sender is not Control control)
			return;

		var current = e.GetPosition(control);
		if (Math.Abs(current.X - start.X) < 4 && Math.Abs(current.Y - start.Y) < 4)
			return;

		var presetId = _draggedPresetId;
		_presetDragStartPoint = null;
		_draggedPresetId = null;

		var data = new DataObject();
		data.Set(PresetDragFormat, presetId);

		await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
	}

	public void OnPresetPointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		if (_presetDragStartPoint == null)
			return;

		_presetDragStartPoint = null;
		_draggedPresetId = null;

		if (sender is not Control control || control.DataContext is not BoardItem item)
			return;

		if (item.IsPreset && item.Preset != null)
		{
			_ = ApplyPresetFromClick(item.Preset);
		}
	}

	private async Task ApplyPresetFromClick(Preset preset)
	{
		ShowLoadingOverlay();

		try
		{
			await _viewModel.ApplyPresetAsync(preset);
			ShowToast(Loc.Get(LocToastSaved));
		}
		finally
		{
			HideLoadingOverlay();
		}
	}

	public void OnPresetDragOver(object? sender, DragEventArgs e)
	{
		if (e.Data.Contains(PresetDragFormat))
			e.DragEffects = DragDropEffects.Move;
		else
			e.DragEffects = DragDropEffects.None;
	}

	public void OnPresetDrop(object? sender, DragEventArgs e)
	{
		if (!e.Data.Contains(PresetDragFormat))
			return;

		if (sender is not Control control || control.DataContext is not BoardItem item)
			return;

		if (!item.IsPreset || item.Preset == null)
			return;

		var draggedId = (string?)e.Data.Get(PresetDragFormat);
		if (draggedId == null)
			return;

		_viewModel.ReorderPresetTo(draggedId, item.Preset.Id);
	}

	private async void ApplyDefault()
	{
		await _viewModel.ApplyDefaultAsync();
	}

	private async Task OpenFilePickerForCursors()
	{
		var topLevel = TopLevel.GetTopLevel(this);
		if (topLevel == null)
			return;

		var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
		{
			Title = Loc.Get(LocResetDefault),
			AllowMultiple = true,
			FileTypeFilter = new[]
			{
				new FilePickerFileType(CursorFileFilterName)
				{
					Patterns = CursorFilePatterns
				}
			}
		});

		if (files.Count == 0)
			return;

		var paths = files.Select(f => f.Path.LocalPath).ToArray();
		await _viewModel.ImportCursorsAsync(paths);
	}

	private async Task OpenPresetEditorForNew()
	{
		var topLevel = TopLevel.GetTopLevel(this);
		if (topLevel == null)
			return;

		var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
		{
			Title = Loc.Get(LocResetDefault),
			AllowMultiple = true,
			FileTypeFilter = new[]
			{
				new FilePickerFileType(CursorFileFilterName)
				{
					Patterns = CursorFilePatterns
				}
			}
		});

		var paths = files.Count > 0
			? files.Select(f => f.Path.LocalPath).ToArray()
			: Array.Empty<string>();

		var editor = new PresetEditorWindow(null, paths);
		await editor.ShowDialog(this);

		if (editor.Result == null)
			return;

		try
		{
			PresetStore.Save(editor.Result);
			_viewModel.ReloadGallery();
		}
		catch
		{
		}
	}

	private void ShowToast(string message)
	{
		var root = this.FindControl<Panel>("RootGrid");
		if (root != null)
			ToastService.Show(root, message);
	}

	public async void OnFooterClick(object? sender, PointerPressedEventArgs e)
	{
		var dialog = new Window
		{
			Title = Loc.Get(LocAboutTitle),
			Width = 360,
			Height = 220,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			CanResize = false,
			ShowInTaskbar = false,
		};

		var panel = new StackPanel
		{
			Margin = new Avalonia.Thickness(24),
			Spacing = 12,
		};

		panel.Children.Add(new TextBlock
		{
			Text = "Cursor Palette",
			FontSize = 18,
			FontWeight = FontWeight.Bold,
		});

		panel.Children.Add(new TextBlock
		{
			Text = Loc.Get(LocAboutLicenseHint),
			FontSize = 12,
			Foreground = Brushes.Gray,
			TextWrapping = TextWrapping.Wrap,
		});

		var closeButton = new Button
		{
			Content = Loc.Get(LocAboutClose),
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Avalonia.Thickness(0, 8, 0, 0),
		};
		closeButton.Click += (_, _) => dialog.Close();
		panel.Children.Add(closeButton);

		dialog.Content = panel;
		await dialog.ShowDialog(this);
	}

	private void OnDragOver(object? sender, DragEventArgs e)
	{
		if (e.Data.Contains(DataFormats.Files))
			e.DragEffects = DragDropEffects.Copy;
		else
			e.DragEffects = DragDropEffects.None;
	}

	private async void OnDrop(object? sender, DragEventArgs e)
	{
		if (e.Data.Contains(DataFormats.Files))
		{
			var files = e.Data.GetFiles();
			if (files == null)
				return;

			var paths = files.Select(f => f.Path.LocalPath).ToArray();
			await _viewModel.HandleDroppedPathsAsync(paths);
		}
	}

	private BoardItem? GetContextMenuItem(object? sender)
	{
		if (sender is MenuItem menuItem && menuItem.DataContext is BoardItem item)
			return item;

		if (sender is Control control && control.DataContext is BoardItem ctrlItem)
			return ctrlItem;

		return null;
	}

	public async void OnMenuEdit(object? sender, RoutedEventArgs e)
	{
		if (GetContextMenuItem(sender) is not { IsPreset: true, Preset: { } preset })
			return;

		var editor = new PresetEditorWindow(preset, Array.Empty<string>());
		await editor.ShowDialog(this);

		if (editor.Result == null)
			return;

		try
		{
			PresetStore.Save(editor.Result);
			_viewModel.ReloadGallery();
		}
		catch
		{
		}
	}

	public async void OnMenuRename(object? sender, RoutedEventArgs e)
	{
		if (GetContextMenuItem(sender) is not { IsPreset: true, Preset: { } preset })
			return;

		var dialog = new Window
		{
			Title = Loc.Get(LocRenameTitle),
			Width = DeleteDialogWidth,
			Height = DeleteDialogHeight,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
		};

		var panel = new StackPanel
		{
			Margin = new Avalonia.Thickness(DialogMargin),
			Spacing = DialogSpacing,
			VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
		};

		var textBox = new TextBox
		{
			Text = preset.Name,
			SelectionStart = 0,
			SelectionEnd = preset.Name.Length,
		};

		panel.Children.Add(textBox);

		var buttonPanel = new StackPanel
		{
			Orientation = Avalonia.Layout.Orientation.Horizontal,
			HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
			Spacing = ButtonSpacing,
		};

		var saveButton = new Button { Content = Loc.Get(LocEditorSave) };
		var cancelButton = new Button { Content = Loc.Get(LocEditorCancel) };

		buttonPanel.Children.Add(cancelButton);
		buttonPanel.Children.Add(saveButton);
		panel.Children.Add(buttonPanel);
		dialog.Content = panel;

		cancelButton.Click += (_, _) => dialog.Close();
		saveButton.Click += (_, _) =>
		{
			_viewModel.RenamePreset(preset, textBox.Text ?? EmptyValue);
			dialog.Close();
		};

		textBox.KeyDown += (_, keyArgs) =>
		{
			if (keyArgs.Key == Key.Enter)
			{
				_viewModel.RenamePreset(preset, textBox.Text ?? EmptyValue);
				dialog.Close();
			}
		};

		await dialog.ShowDialog(this);
		textBox.Focus();
	}

	public void OnMenuMoveLeft(object? sender, RoutedEventArgs e)
	{
		if (GetContextMenuItem(sender) is not { IsPreset: true, Preset: { } preset })
			return;

		_viewModel.MovePreset(preset, -1);
	}

	public void OnMenuMoveRight(object? sender, RoutedEventArgs e)
	{
		if (GetContextMenuItem(sender) is not { IsPreset: true, Preset: { } preset })
			return;

		_viewModel.MovePreset(preset, 1);
	}

	public async void OnMenuDownload(object? sender, RoutedEventArgs e)
	{
		if (GetContextMenuItem(sender) is not { IsPreset: true, Preset: { } preset })
			return;

		var topLevel = TopLevel.GetTopLevel(this);
		if (topLevel == null)
			return;

		var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
		{
			Title = Loc.Get(LocMenuDownload),
			DefaultExtension = "cursorpalette",
			FileTypeChoices = new[]
			{
				new FilePickerFileType("Cursor Palette Bundle")
				{
					Patterns = new[] { "*.cursorpalette" }
				}
			}
		});

		if (file == null)
			return;

		try
		{
			var (path, count) = PresetPackageService.ExportBundle(new[] { preset }, preset.Name);
			File.Move(path, file.Path.LocalPath, overwrite: true);
			ShowToast(Loc.Get("S.Toast.PresetDownloaded"));
		}
		catch
		{
		}
	}

	public async void OnMenuDelete(object? sender, RoutedEventArgs e)
	{
		var item = GetContextMenuItem(sender);
		if (item == null)
			return;

		if (item.IsGroup && item.Group != null)
		{
			_viewModel.DeleteGroup(item.Group.Id);
			return;
		}

		if (item is not { IsPreset: true, Preset: { } preset })
			return;

		var dialog = new Window
		{
			Title = Loc.Get(LocConfirmDeleteTitle),
			Width = DeleteDialogWidth,
			Height = DeleteDialogHeight,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
		};

		var panel = new StackPanel
		{
			Margin = new Avalonia.Thickness(DialogMargin),
			Spacing = DialogSpacing,
			VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
		};

		panel.Children.Add(new TextBlock
		{
			Text = Loc.Format(LocConfirmDeleteText, preset.Name),
			TextWrapping = Avalonia.Media.TextWrapping.Wrap,
		});

		var buttonPanel = new StackPanel
		{
			Orientation = Avalonia.Layout.Orientation.Horizontal,
			HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
			Spacing = ButtonSpacing,
		};

		var yesButton = new Button { Content = DeleteButtonText };
		var noButton = new Button { Content = Loc.Get(LocEditorCancel) };

		buttonPanel.Children.Add(noButton);
		buttonPanel.Children.Add(yesButton);
		panel.Children.Add(buttonPanel);
		dialog.Content = panel;

		noButton.Click += (_, _) => dialog.Close();
		yesButton.Click += (_, _) =>
		{
			_viewModel.DeletePreset(preset);
			dialog.Close();
		};

		await dialog.ShowDialog(this);
	}

	public void OnMenuToggleCollapse(object? sender, RoutedEventArgs e)
	{
		if (GetContextMenuItem(sender) is not { IsGroup: true, Group: { } group })
			return;

		_viewModel.ToggleGroupCollapse(group.Id);
	}

	private async void OnUndoClick(object? sender, RoutedEventArgs e)
	{
		await _viewModel.UndoAsync();
	}

	private bool _isDarkTheme;

	private void OnThemeToggle(object? sender, RoutedEventArgs e)
	{
		_isDarkTheme = !_isDarkTheme;
		RequestedThemeVariant = _isDarkTheme ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;

		UpdateThemeToggleIcon();
	}

	private void UpdateThemeToggleIcon()
	{
		var themeButton = this.FindControl<Button>("ThemeToggleButton");
		if (themeButton != null)
			themeButton.Content = _isDarkTheme ? ThemeIconDark : ThemeIconLight;
	}

	private int _languageIndex;

	private void OnLanguageClick(object? sender, RoutedEventArgs e)
	{
		_languageIndex = (_languageIndex + 1) % SupportedLanguages.Length;
		var lang = SupportedLanguages[_languageIndex];

		if (_languageButton != null)
			_languageButton.Content = lang.ToUpperInvariant();

		LocalizationManager.SetLanguage(lang);
		ApplyLocalization();
		_viewModel.ReloadGallery();
	}
}
