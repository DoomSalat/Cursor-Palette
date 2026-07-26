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
	private const string GroupDragFormat = "application/x-cursor-palette-group";

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
	private const string LocMenuRandomPreset = "S.Menu.RandomPreset";
	private const string LocMenuDownloadSystem = "S.Menu.DownloadSystemCursors";
	private const string LocMenuEditGroup = "S.Menu.EditGroup";
	private const string LocMenuConsolidateGroup = "S.Menu.ConsolidateGroup";
	private const string LocMenuUngroup = "S.Menu.Ungroup";
	private const string LocMenuAssignToGroup = "S.Menu.AssignToGroup";
	private const string LocMenuRemoveFromGroup = "S.Menu.RemoveFromGroup";
	private const string LocGroupToastConsolidated = "S.Group.Toast.Consolidated";
	private const string LocGroupToastUngrouped = "S.Group.Toast.Ungrouped";
	private const string LocGroupToastCreated = "S.Group.Toast.Created";
	private const string LocGroupSelectedCount = "S.Group.SelectedCount";
	private const string LocGroupDefaultName = "S.Group.DefaultName";
	private const string LocGroupCreate = "S.Group.Create";
	private const string LocGroupCancel = "S.Group.Cancel";
	private const string LocMenuCreateGroup = "S.Menu.CreateGroup";
	private const string LocAppLogoRandomTooltip = "S.AppLogo.RandomTooltip";
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
	private const double AboutDialogWidth = 520;
	private const double AboutDialogHeight = 460;
	private const double AboutMinWidth = 400;
	private const double AboutMinHeight = 320;
	private const double AboutPadding = 20;
	private const double AboutCloseButtonMinWidth = 90;

	private const string AboutLicenseText = "Copyright (c) 2026 Capitan Salat\n\nPermission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the \"Software\"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:\n\nThe above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.\n\nTHE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.";

	private static readonly string[] SupportedLanguages = { "en", "ru", "de", "es", "ja", "zh" };
	private static readonly string[] CursorFilePatterns = { "*.cur", "*.ani", "*.png", "*.jpg", "*.bmp", "*.gif" };

	private readonly MainWindowViewModel _viewModel = new();
	private Slider? _sizeSlider;
	private TextBlock? _sizeValueText;
	private Button? _applySizeButton;
	private CheckBox? _scaleCursorsCheckBox;
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
	private string? _draggedGroupId;
	private BoardItem? _draggedBoardItem;
	private readonly HashSet<string> _selectedPresetIds = new();
	private string? _pendingGroupColorKey;
	private readonly List<Border> _groupColorSwatches = new();
	private const double GhostSize = 120;
	private const double GhostPreviewSize = 40;
	private const double CellMarginForIndicator = 6;

	public MainWindow()
	{
		InitializeComponent();
		DataContext = _viewModel;
		_viewModel.Initialize();

		Width = AppState.GetMainWindowWidth();
		Height = AppState.GetMainWindowHeight();

		_sizeSlider = this.FindControl<Slider>("SizeSlider");
		_sizeValueText = this.FindControl<TextBlock>("SizeValueText");
		_applySizeButton = this.FindControl<Button>("ApplySizeButton");
		_scaleCursorsCheckBox = this.FindControl<CheckBox>("ScaleCursorsCheckBox");
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

		if (_scaleCursorsCheckBox != null)
			_scaleCursorsCheckBox.IsChecked = AppState.GetScaleCursorsEnabled();

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

		UpdateOpenFolderToggleIcon();

		BuildGroupColorSwatches();

		_ = CheckForUpdatesAsync();
	}

	protected override void OnClosed(EventArgs e)
	{
		AppState.SetMainWindowSize(Width, Height);
		base.OnClosed(e);
	}

	private UpdateInfo? _updateInfo;

	private async Task CheckForUpdatesAsync()
	{
		var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? AppInfo.DefaultVersion;
		_updateInfo = await UpdateChecker.GetLatestReleaseInfoAsync();

		if (_updateInfo is null)
			return;

		var isAvailable = await UpdateChecker.IsUpdateAvailableAsync(version);
		if (isAvailable)
		{
			ShowToast(Loc.Format(LocToastUpdateAvailable, version));
			var indicator = this.FindControl<Button>("UpdateIndicator");
			if (indicator != null)
				indicator.IsVisible = true;
		}
	}

	private void OnUpdateIndicatorClick(object? sender, RoutedEventArgs e)
	{
		if (_updateInfo == null)
			return;

		try
		{
			System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
			{
				FileName = _updateInfo.DownloadUrl,
				UseShellExecute = true,
			});
		}
		catch
		{
		}
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

		var logoText = this.FindControl<TextBlock>("AppLogoText");
		if (logoText != null)
			ToolTip.SetTip(logoText, Loc.Get(LocAppLogoRandomTooltip));

		var groupCreateBtn = this.FindControl<Button>("GroupCreateButton");
		if (groupCreateBtn != null)
			groupCreateBtn.Content = Loc.Get(LocGroupCreate);

		var groupCancelBtn = this.FindControl<Button>("GroupCancelButton");
		if (groupCancelBtn != null)
			groupCancelBtn.Content = Loc.Get(LocGroupCancel);

		var groupNameBox = this.FindControl<TextBox>("GroupNameBox");
		if (groupNameBox != null && string.IsNullOrEmpty(groupNameBox.Text))
			groupNameBox.Text = Loc.Get(LocGroupDefaultName);

		var menuCreateGroup = this.FindControl<MenuItem>("MenuCreateGroupItem");
		if (menuCreateGroup != null)
			menuCreateGroup.Header = Loc.Get(LocMenuCreateGroup);
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
			Loc.Get(LocMenuDownloadSystem),
			Loc.Get(LocMenuToggleCollapse),
			Loc.Get(LocMenuRandomPreset),
			Loc.Get(LocMenuEditGroup),
			Loc.Get(LocMenuConsolidateGroup),
			Loc.Get(LocMenuUngroup),
			Loc.Get(LocMenuAssignToGroup),
			Loc.Get(LocMenuRemoveFromGroup),
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
		await ApplySizeInternal(size);
	}

	public async void ApplyPresetSize(int sizePx)
	{
		await ApplySizeInternal(sizePx);
	}

	private async Task ApplySizeInternal(int sizePx)
	{
		ShowLoadingOverlay();
		try
		{
			var useScaling = _scaleCursorsCheckBox?.IsChecked == true;

			await _viewModel.ApplySizeAsync(sizePx, useScaling);

			ShowToast(Loc.Get(LocToastSizeApplied));
		}
		finally
		{
			HideLoadingOverlay();
		}
	}

	private void OnScaleCursorsClick(object? sender, RoutedEventArgs e)
	{
		var enabled = _scaleCursorsCheckBox?.IsChecked == true;

		AppState.SetScaleCursorsEnabled(enabled);
	}

	public void SetScaleCursorsCheckbox(bool value)
	{
		if (_scaleCursorsCheckBox != null)
			_scaleCursorsCheckBox.IsChecked = value;

		AppState.SetScaleCursorsEnabled(value);
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
			if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
			{
				_viewModel.ToggleGroupCollapse(item.Group.Id);
				return;
			}

			_presetDragStartPoint = e.GetPosition(control);
			_draggedGroupId = item.Group.Id;
			_draggedBoardItem = item;
			return;
		}

		if (item.IsPreset && item.Preset != null)
		{
			if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
			{
				ToggleSelection(item.Preset.Id);
				_viewModel.ReloadGallery();
				return;
			}

			if (e.ClickCount >= 2)
			{
				var editor = new PresetEditorWindow(item.Preset, Array.Empty<string>());
				await editor.ShowDialog(this);

				if (editor.Result != null)
				{
					_viewModel.ReloadGallery();
					ShowToast(Loc.Get(LocToastSaved));
				}
				return;
			}

			_presetDragStartPoint = e.GetPosition(control);
			_draggedPresetId = item.Preset.Id;
			_draggedBoardItem = item;
		}
	}

	public async void OnPresetPointerMoved(object? sender, PointerEventArgs e)
	{
		if (_presetDragStartPoint is not { } start)
			return;

		if (sender is not Control control)
			return;

		var current = e.GetPosition(control);
		if (Math.Abs(current.X - start.X) < 4 && Math.Abs(current.Y - start.Y) < 4)
			return;

		if (_draggedGroupId != null)
		{
			var groupId = _draggedGroupId;
			var draggedItem = _draggedBoardItem;
			_presetDragStartPoint = null;
			_draggedGroupId = null;

			var data = new DataObject();
			data.Set(GroupDragFormat, groupId);

			BeginDragGhost(draggedItem);
			await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
			EndDragGhost();
			return;
		}

		if (_draggedPresetId == null)
			return;

		var presetId = _draggedPresetId;
		var draggedItem2 = _draggedBoardItem;
		_presetDragStartPoint = null;
		_draggedPresetId = null;

		var data2 = new DataObject();
		data2.Set(PresetDragFormat, presetId);

		BeginDragGhost(draggedItem2);
		await DragDrop.DoDragDrop(e, data2, DragDropEffects.Move);
		EndDragGhost();
	}

	public void OnPresetPointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		if (_presetDragStartPoint == null)
			return;

		_presetDragStartPoint = null;
		_draggedPresetId = null;

		if (sender is not Control control || control.DataContext is not BoardItem item)
			return;

		if (item.IsGroup && item.Group != null)
		{
			_draggedGroupId = null;
			_viewModel.ToggleGroupCollapse(item.Group.Id);
			return;
		}

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
		if (e.Data.Contains(PresetDragFormat) || e.Data.Contains(GroupDragFormat))
		{
			e.DragEffects = DragDropEffects.Move;
			UpdateDragGhostPosition(e.GetPosition(this));
			UpdateReorderIndicator(e.GetPosition(this));
		}
		else
			e.DragEffects = DragDropEffects.None;
	}

	public void OnPresetDrop(object? sender, DragEventArgs e)
	{
		if (sender is not Control control || control.DataContext is not BoardItem item)
			return;

		if (e.Data.Contains(GroupDragFormat))
		{
			var draggedGroupId = (string?)e.Data.Get(GroupDragFormat);
			if (draggedGroupId == null)
				return;

			var targetId = item.IsPreset ? item.Preset?.Id : item.IsGroup ? item.Group?.Id : null;
			if (targetId == null || targetId == draggedGroupId)
				return;

			_viewModel.ReorderPresetTo(draggedGroupId, targetId);
			return;
		}

		if (!e.Data.Contains(PresetDragFormat))
			return;

		if (!item.IsPreset || item.Preset == null)
			return;

		var draggedId = (string?)e.Data.Get(PresetDragFormat);
		if (draggedId == null)
			return;

		_viewModel.ReorderPresetTo(draggedId, item.Preset.Id);
	}

	private void BeginDragGhost(BoardItem? item)
	{
		if (item == null)
			return;

		var ghost = this.FindControl<Border>("DragGhost");
		var ghostText = this.FindControl<TextBlock>("DragGhostText");
		var ghostImage = this.FindControl<Image>("DragGhostImage");

		if (ghost == null || ghostText == null)
			return;

		ghostText.Text = item.DisplayName;

		if (ghostImage != null)
			ghostImage.Source = item.Preview;

		ghost.IsVisible = true;
	}

	private void EndDragGhost()
	{
		var ghost = this.FindControl<Border>("DragGhost");
		var insertionLine = this.FindControl<Border>("ReorderInsertionLine");

		if (ghost != null)
			ghost.IsVisible = false;

		if (insertionLine != null)
			insertionLine.IsVisible = false;

		_draggedBoardItem = null;
		_draggedGroupId = null;
	}

	private void UpdateDragGhostPosition(Point positionInWindow)
	{
		var ghost = this.FindControl<Border>("DragGhost");
		if (ghost == null || !ghost.IsVisible)
			return;

		var transform = ghost.RenderTransform as TranslateTransform;
		if (transform == null)
			return;

		transform.X = positionInWindow.X - GhostSize / 2;
		transform.Y = positionInWindow.Y - GhostSize / 2;
	}

	private void UpdateReorderIndicator(Point positionInWindow)
	{
		var insertionLine = this.FindControl<Border>("ReorderInsertionLine");
		var gallery = this.FindControl<ItemsControl>("Gallery");

		if (insertionLine == null || gallery == null)
			return;

		var positionInGallery = positionInWindow - gallery.TranslatePoint(new Point(0, 0), this)!.Value;

		var items = _viewModel.Board;
		if (items.Count == 0)
		{
			insertionLine.IsVisible = false;
			return;
		}

		var draggedId = _draggedBoardItem?.Preset?.Id ?? _draggedBoardItem?.Group?.Id;
		var bestIndex = -1;
		var bestX = 0.0;
		var bestTop = 0.0;
		var bestHeight = 0.0;

		for (var i = 0; i < items.Count; i++)
		{
			var container = gallery.ContainerFromIndex(i);
			if (container is not Control control)
				continue;

			var bounds = control.Bounds;
			if (bounds.Width == 0 || bounds.Height == 0)
				continue;

			var posInGallery = control.TranslatePoint(new Point(0, 0), gallery)!.Value;

			if (positionInGallery.Y >= posInGallery.Y && positionInGallery.Y <= posInGallery.Y + bounds.Height)
			{
				if (positionInGallery.X < posInGallery.X + bounds.Width / 2)
				{
					bestIndex = i;
					bestX = posInGallery.X - CellMarginForIndicator;
					bestTop = posInGallery.Y;
					bestHeight = bounds.Height;
				}
				else
				{
					bestIndex = i + 1;
					bestX = posInGallery.X + bounds.Width + CellMarginForIndicator;
					bestTop = posInGallery.Y;
					bestHeight = bounds.Height;
				}
				break;
			}
		}

		if (bestIndex < 0)
		{
			insertionLine.IsVisible = false;
			return;
		}

		var draggedIndex = draggedId != null
			? items.ToList().FindIndex(b => b.Preset?.Id == draggedId || b.Group?.Id == draggedId)
			: -1;

		if (draggedIndex >= 0 && (bestIndex == draggedIndex || bestIndex == draggedIndex + 1))
		{
			insertionLine.IsVisible = false;
			return;
		}

		var galleryOffset = gallery.TranslatePoint(new Point(0, 0), this)!.Value;
		var transform = insertionLine.RenderTransform as TranslateTransform;
		if (transform != null)
		{
			transform.X = bestX + galleryOffset.X - 2;
			transform.Y = bestTop + galleryOffset.Y;
		}
		insertionLine.Height = bestHeight;
		insertionLine.IsVisible = true;
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
			? files.Select(file => file.Path.LocalPath).ToArray()
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
			Width = AboutDialogWidth,
			Height = AboutDialogHeight,
			MinWidth = AboutMinWidth,
			MinHeight = AboutMinHeight,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			CanResize = true,
			ShowInTaskbar = false,
		};

		var contentPanel = new StackPanel
		{
			Spacing = 12,
		};

		var titlePanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Avalonia.Thickness(0, 0, 0, 4),
		};
		titlePanel.Children.Add(new TextBlock
		{
			Text = "Cursor ",
			FontSize = 18,
			FontWeight = FontWeight.SemiBold,
			VerticalAlignment = VerticalAlignment.Center,
		});
		titlePanel.Children.Add(new TextBlock
		{
			Text = "Palette",
			FontSize = 18,
			FontWeight = FontWeight.SemiBold,
			Foreground = (IBrush?)Application.Current?.FindResource("SystemAccentColor"),
			VerticalAlignment = VerticalAlignment.Center,
		});
		contentPanel.Children.Add(titlePanel);

		var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? AppInfo.DefaultVersion;
		contentPanel.Children.Add(new TextBlock
		{
			Text = $"v{version}",
			FontSize = 12,
			Foreground = Brushes.Gray,
		});

		contentPanel.Children.Add(new TextBlock
		{
			Text = AppInfo.LicenseName,
			FontSize = 13,
			FontWeight = FontWeight.SemiBold,
		});

		contentPanel.Children.Add(new TextBlock
		{
			Text = AboutLicenseText,
			FontSize = 12,
			TextWrapping = TextWrapping.Wrap,
		});

		var scrollViewer = new ScrollViewer
		{
			Content = contentPanel,
			Padding = new Avalonia.Thickness(AboutPadding),
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
		};

		var closeButton = new Button
		{
			Content = Loc.Get(LocAboutClose),
			HorizontalAlignment = HorizontalAlignment.Right,
			MinWidth = AboutCloseButtonMinWidth,
			Margin = new Avalonia.Thickness(DialogMargin, 10, DialogMargin, 10),
		};
		closeButton.Click += (_, _) => dialog.Close();

		var root = new Grid
		{
			RowDefinitions = new RowDefinitions("*,Auto"),
		};
		Grid.SetRow(scrollViewer, 0);
		Grid.SetRow(closeButton, 1);
		root.Children.Add(scrollViewer);
		root.Children.Add(closeButton);

		dialog.Content = root;
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

			var paths = files.Select(file => file.Path.LocalPath).ToArray();
			await _viewModel.HandleDroppedPathsAsync(paths);
		}
	}

	private BoardItem? GetContextMenuItem(object? sender)
	{
		if (sender is MenuItem menuItem && menuItem.DataContext is BoardItem item)
			return item;

		if (sender is Control control && control.DataContext is BoardItem controlItem)
			return controlItem;

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

		textBox.KeyDown += (_, keyEventArgs) =>
		{
			if (keyEventArgs.Key == Key.Enter)
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

	public async void OnMenuEditGroup(object? sender, RoutedEventArgs e)
	{
		if (GetContextMenuItem(sender) is not { IsGroup: true, Group: { } group })
			return;

		var dialog = new GroupEditWindow(group);
		var result = await dialog.ShowDialog<bool?>(this);

		if (result == true)
		{
			_viewModel.EditGroup(group.Id, dialog.GroupName, dialog.ColorKey);
			ShowToast(Loc.Get(LocToastSaved));
		}
	}

	public void OnMenuConsolidateGroup(object? sender, RoutedEventArgs e)
	{
		if (GetContextMenuItem(sender) is not { IsGroup: true, Group: { } group })
			return;

		_viewModel.ConsolidateGroup(group.Id);
		ShowToast(Loc.Format(LocGroupToastConsolidated, group.Name));
	}

	public void OnMenuUngroup(object? sender, RoutedEventArgs e)
	{
		if (GetContextMenuItem(sender) is not { IsGroup: true, Group: { } group })
			return;

		_viewModel.Ungroup(group.Id);
		ShowToast(Loc.Format(LocGroupToastUngrouped, group.Name));
	}

	public void OnMenuAssignToGroup(object? sender, RoutedEventArgs e)
	{
		if (GetContextMenuItem(sender) is not { IsPreset: true, Preset: { } preset })
			return;

		var groups = CursorPalette.Services.GroupStore.LoadAll();
		if (groups.Count == 0)
			return;

		var menu = new ContextMenu
		{
			PlacementTarget = sender as Control,
		};

		foreach (var targetGroup in groups)
		{
			var item = new MenuItem
			{
				Header = targetGroup.Name,
			};
			item.Click += (_, _) =>
			{
				_viewModel.AssignToGroup(preset.Id, targetGroup.Id);
				ShowToast(Loc.Get(LocToastSaved));
			};
			menu.Items.Add(item);
		}

		menu.Open(this);
	}

	public void OnMenuRemoveFromGroup(object? sender, RoutedEventArgs e)
	{
		if (GetContextMenuItem(sender) is not { IsPreset: true, Preset: { } preset, GroupId: { } groupId })
			return;

		_viewModel.RemoveFromGroup(preset.Id, groupId);
		ShowToast(Loc.Get(LocToastSaved));
	}

	private void BuildGroupColorSwatches()
	{
		if (this.FindControl<WrapPanel>("GroupColorSwatches") is not { } swatchesPanel)
			return;

		swatchesPanel.Children.Clear();
		_groupColorSwatches.Clear();

		foreach (var (key, hex) in GroupColors.Palette)
		{
			var swatch = new Border
			{
				Width = 20,
				Height = 20,
				CornerRadius = new CornerRadius(10),
				Background = Brush.Parse(hex),
				BorderThickness = new Thickness(0),
				Margin = new Thickness(4, 0, 4, 0),
				Cursor = new Cursor(StandardCursorType.Hand),
			};

			var capturedKey = key;
			swatch.PointerPressed += (_, _) =>
			{
				_pendingGroupColorKey = capturedKey;

				foreach (var other in _groupColorSwatches)
					other.BorderThickness = new Thickness(0);

				swatch.BorderThickness = new Thickness(2);
			};

			swatchesPanel.Children.Add(swatch);
			_groupColorSwatches.Add(swatch);
		}
	}

	private void ClearGroupSelection()
	{
		_selectedPresetIds.Clear();
		_pendingGroupColorKey = null;

		if (this.FindControl<TextBox>("GroupNameBox") is { } nameBox)
			nameBox.Text = Loc.Get(LocGroupDefaultName);

		foreach (var swatch in _groupColorSwatches)
			swatch.BorderThickness = new Thickness(0);

		if (this.FindControl<Border>("GroupToolbar") is { } toolbar)
			toolbar.IsVisible = false;
	}

	private void ToggleSelection(string presetId)
	{
		if (_selectedPresetIds.Contains(presetId))
			_selectedPresetIds.Remove(presetId);
		else
			_selectedPresetIds.Add(presetId);

		UpdateGroupToolbar();
		_viewModel.SetSelectedPresetIds(_selectedPresetIds);
	}

	private void UpdateGroupToolbar()
	{
		if (this.FindControl<Border>("GroupToolbar") is not { } toolbar)
			return;
		if (this.FindControl<TextBlock>("GroupSelectionCountText") is not { } countText)
			return;

		if (_selectedPresetIds.Count == 0)
		{
			toolbar.IsVisible = false;
			return;
		}

		toolbar.IsVisible = true;
		countText.Text = Loc.Format(LocGroupSelectedCount, _selectedPresetIds.Count);
	}

	public void OnGroupCreateClick(object? sender, RoutedEventArgs e)
	{
		if (_selectedPresetIds.Count == 0 || _pendingGroupColorKey == null)
			return;

		var nameBox = this.FindControl<TextBox>("GroupNameBox");
		var name = nameBox?.Text?.Trim() ?? "";
		if (name.Length == 0)
			name = Loc.Get(LocGroupDefaultName);

		_viewModel.CreateGroupFromSelection(name, _pendingGroupColorKey, _selectedPresetIds.ToList());
		ClearGroupSelection();
		ShowToast(Loc.Format(LocGroupToastCreated, name));
	}

	public void OnGroupCancelClick(object? sender, RoutedEventArgs e)
	{
		ClearGroupSelection();
		_viewModel.SetSelectedPresetIds(null);
		_viewModel.ReloadGallery();
	}

	public async void OnMenuCreateGroup(object? sender, RoutedEventArgs e)
	{
		var dialog = new GroupEditWindow();
		var result = await dialog.ShowDialog<bool?>(this);

		if (result != true)
			return;

		var name = dialog.GroupName;
		if (string.IsNullOrWhiteSpace(name))
			name = Loc.Get(LocGroupDefaultName);

		_viewModel.CreateEmptyGroup(name, dialog.ColorKey);
		ShowToast(Loc.Format(LocGroupToastCreated, name));
	}

	public async void OnMenuRandomPreset(object? sender, RoutedEventArgs e)
	{
		if (GetContextMenuItem(sender) is not { IsGroup: true, Group: { } group })
			return;

		ShowLoadingOverlay();
		try
		{
			await _viewModel.ApplyRandomFromGroupAsync(group);
			ShowToast(Loc.Get(LocToastSaved));
		}
		finally
		{
			HideLoadingOverlay();
		}
	}

	public async void OnAppLogoClick(object? sender, PointerPressedEventArgs e)
	{
		ShowLoadingOverlay();
		try
		{
			await _viewModel.ApplyRandomFromBoardAsync();
			ShowToast(Loc.Get(LocToastSaved));
		}
		finally
		{
			HideLoadingOverlay();
		}
	}

	private const string LocToastSystemCursorsDownloaded = "S.Toast.SystemCursorsDownloaded";
	private const string LocWindowsDefault = "S.WindowsDefault";
	private const string DownloadsFolderName = "Downloads";

	public void OnMenuDownloadSystem(object? sender, RoutedEventArgs e)
	{
		var cursorService = CursorServiceProvider.Current as LinuxCursorService;
		if (cursorService == null)
			return;

		var currentValues = cursorService.ReadCurrentValues();
		if (currentValues.Count == 0)
			return;

		var downloadsFolder = GetDownloadsFolder();
		var folderName = Loc.Get(LocWindowsDefault);
		var destFolder = System.IO.Path.Combine(downloadsFolder, folderName);
		Directory.CreateDirectory(destFolder);

		var count = 0;
		foreach (var (roleName, sourcePath) in currentValues)
		{
			if (!File.Exists(sourcePath))
				continue;

			var destPath = System.IO.Path.Combine(destFolder, $"{roleName}.xcursor");
			File.Copy(sourcePath, destPath, overwrite: true);
			count++;
		}

		ShowToast(Loc.Format(LocToastSystemCursorsDownloaded, count));
	}

	private static string GetDownloadsFolder()
	{
		var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DownloadsFolderName);
		return Directory.Exists(path) ? path : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
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
		if (_languageButton == null)
			return;

		var menu = new ContextMenu
		{
			PlacementTarget = _languageButton,
		};

		foreach (var language in LocalizationManager.Available)
		{
			var code = language.Code;
			var item = new MenuItem
			{
				Header = language.DisplayName,
				IsChecked = language.Code == LocalizationManager.Current,
			};
			item.Click += (_, _) => SwitchLanguage(code);
			menu.Items.Add(item);
		}

		menu.Open(_languageButton);
	}

	private void SwitchLanguage(string code)
	{
		if (code == LocalizationManager.Current)
			return;

		LocalizationManager.SetLanguage(code);

		_languageIndex = Math.Max(0, Array.IndexOf(SupportedLanguages, code));
		if (_languageButton != null)
			_languageButton.Content = code.ToUpperInvariant();

		ApplyLocalization();
		_viewModel.ReloadGallery();
	}

	private void OnOpenFolderToggleClick(object? sender, RoutedEventArgs e)
	{
		AppState.SetOpenFolderAfterDownload(!AppState.GetOpenFolderAfterDownload());
		UpdateOpenFolderToggleIcon();
	}

	private void OnExportClick(object? sender, RoutedEventArgs e)
	{
		var presets = PresetStore.LoadAll();
		var groups = GroupStore.LoadAll();
		var toastHost = this.FindControl<Panel>("RootGrid");
		if (toastHost == null)
			return;

		var dialog = new ExportWindow(presets, groups, toastHost);
		dialog.ShowDialog(this);
	}

	private async void OnImportClick(object? sender, RoutedEventArgs e)
	{
		var storageProvider = StorageProvider;
		var files = await storageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
		{
			Title = "Import",
			AllowMultiple = false,
			FileTypeFilter = new[]
			{
				new Avalonia.Platform.Storage.FilePickerFileType("Cursor Palette Package")
				{
					Patterns = new[] { "*.cursorpalette", "*.zip", "*.tar.gz" },
				},
			},
		});

		if (files.Count == 0)
			return;

		var filePath = files[0].Path.LocalPath;
		var detected = PresetPackageService.TryDetectPackage(filePath);
		if (detected == null)
		{
			ShowToast(Loc.Get("S.Error.ImportUnrecognized"));
			return;
		}

		_viewModel.ImportAllFromPackage(detected);
		ShowToast(Loc.Get(LocToastSaved));
	}

	private void UpdateOpenFolderToggleIcon()
	{
		var toggle = this.FindControl<Button>("OpenFolderToggle");
		if (toggle != null)
			toggle.Opacity = AppState.GetOpenFolderAfterDownload() ? 1.0 : 0.4;
	}

	private void OnGitHubLinkClick(object? sender, RoutedEventArgs e)
	{
		try
		{
			System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
			{
				FileName = AppInfo.GitHubUrl,
				UseShellExecute = true,
			});
		}
		catch
		{
		}
	}

	private const string LocInfoTitle = "S.Info.Title";

	private void OnInfoClick(object? sender, RoutedEventArgs e)
	{
		var title = Loc.Get(LocInfoTitle);
		var body = HelpTextService.Get("Main");
		var dialog = new InfoHelpWindow(title, body);
		dialog.ShowDialog(this);
	}
}
