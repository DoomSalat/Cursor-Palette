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
	private List<Preset> _presets = new();
	private string? _activePresetId;
	private TextBlock? _activeCellSizeText;

	public MainWindow()
	{
		InitializeComponent();

		_activePresetId = AppState.GetActivePresetId();

		SetSliderSilently(RegistryCursorService.GetBaseSize());

		ReloadGallery();
		UpdateUndoButton();

		var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
		FooterText.Text = $"Capitan Salat  ·  v{version}";
	}

	/// <summary>Ставит ползунок/текст без применения к системе (программное обновление).</summary>
	private void SetSliderSilently(int sizePx)
	{
		SizeSlider.Value = (sizePx - Constants.Cursor.SizeStep) / (double)Constants.Cursor.SizeStep;
		SizeValueText.Text = $"{sizePx} {Constants.UI.PixelSuffix}";
	}

	private void ReloadGallery()
	{
		_presets = PresetStore.LoadAll();
		Gallery.Items.Clear();
		_activeCellSizeText = null;

		Gallery.Items.Add(CreateDefaultCell());

		foreach (var preset in _presets)
			Gallery.Items.Add(CreatePresetCell(preset));

		Gallery.Items.Add(CreateAddCell());
		EmptyHint.Visibility = _presets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
	}

	private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

	private FrameworkElement CreateDefaultCell()
	{
		var isActive = _activePresetId == null;

		var defaults = RegistryCursorService.GetWindowsDefaultValues();
		var previewPath = defaults.TryGetValue(Constants.Cursor.ArrowRoleName, out var arrow) ? arrow : null;

		var image = new Image
		{
			Width = Constants.UI.Cell.PreviewSize,
			Height = Constants.UI.Cell.PreviewSize,
			Source = CursorPreviewService.GetPreview(previewPath),
			SnapsToDevicePixels = true,
		};
		RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);

		var nameText = new TextBlock
		{
			Text = Loc.Get(Constants.Strings.WindowsDefault),
			TextTrimming = TextTrimming.CharacterEllipsis,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(4, 8, 4, 0),
		};

		var sizeText = new TextBlock
		{
			Text = $"{AppState.GetDefaultBaseSize()} {Constants.UI.PixelSuffix}",
			Foreground = Brush(Constants.Resources.BrushTextDim),
			FontSize = Constants.UI.Cell.SizeFontSize,
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
			Width = Constants.UI.Cell.Size,
			Height = Constants.UI.Cell.Size,
			Margin = new Thickness(Constants.UI.Cell.Margin),
			CornerRadius = new CornerRadius(Constants.UI.Cell.CornerRadius),
			Background = Brush(Constants.Resources.BrushBg),
			BorderThickness = new Thickness(Constants.UI.Cell.BorderThickness),
			BorderBrush = isActive ? Brush(Constants.Resources.BrushAccent) : Brush(Constants.Resources.BrushBorder),
			Child = panel,
			Cursor = Cursors.Hand,
		};

		cell.MouseEnter += (_, _) =>
		{
			if (_activePresetId != null) cell.Background = Brush(Constants.Resources.BrushSurfaceHover);
		};
		cell.MouseLeave += (_, _) => cell.Background = Brush(Constants.Resources.BrushBg);
		cell.MouseLeftButtonUp += (_, _) => ApplyDefault();
		cell.MouseLeftButtonDown += (_, _) => { };

		cell.ToolTip = new ToolTip { Content = Loc.Get(Constants.Strings.WindowsDefault) };

		return cell;
	}

	private FrameworkElement CreatePresetCell(Preset preset)
	{
		var isActive = preset.Id == _activePresetId;

		var previewPath = PresetStore.GetRoleFilePath(preset, Constants.Cursor.ArrowRoleName)
							?? preset.Roles.Keys.Select(r => PresetStore.GetRoleFilePath(preset, r))
								.FirstOrDefault(p => p != null);

		var image = new Image
		{
			Width = Constants.UI.Cell.PreviewSize,
			Height = Constants.UI.Cell.PreviewSize,
			Source = CursorPreviewService.GetPreview(previewPath),
			SnapsToDevicePixels = true,
		};
		RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);

		var nameText = new TextBlock
		{
			Text = preset.Name,
			TextTrimming = TextTrimming.CharacterEllipsis,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(4, 8, 4, 0),
		};

		var countText = new TextBlock
		{
			Text = $"{preset.Roles.Count}/{Constants.Cursor.TotalRoles}",
			Foreground = Brush(Constants.Resources.BrushTextDim),
			FontSize = Constants.UI.Cell.CountFontSize,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(0, 2, 0, 0),
		};

		var sizeText = new TextBlock
		{
			Text = $"{preset.BaseSize} {Constants.UI.PixelSuffix}",
			Foreground = Brush(Constants.Resources.BrushTextDim),
			FontSize = Constants.UI.Cell.SizeFontSize,
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
			Width = Constants.UI.Cell.Size,
			Height = Constants.UI.Cell.Size,
			Margin = new Thickness(Constants.UI.Cell.Margin),
			CornerRadius = new CornerRadius(Constants.UI.Cell.CornerRadius),
			Background = Brush(Constants.Resources.BrushSurface),
			BorderThickness = new Thickness(Constants.UI.Cell.BorderThickness),
			BorderBrush = isActive ? Brush(Constants.Resources.BrushAccent) : Brush(Constants.Resources.BrushBorder),
			Child = panel,
			Cursor = Cursors.Hand,
			Tag = preset,
		};

		cell.MouseEnter += (_, _) =>
		{
			if (preset.Id != _activePresetId) cell.Background = Brush(Constants.Resources.BrushSurfaceHover);
		};
		cell.MouseLeave += (_, _) => cell.Background = Brush(Constants.Resources.BrushSurface);
		cell.MouseLeftButtonUp += (_, _) => ApplyPreset(preset);
		cell.MouseRightButtonUp += (_, e) =>
		{
			cell.ContextMenu!.IsOpen = true;
			e.Handled = true;
		};

		var menu = new ContextMenu();
		var editItem = new MenuItem { Header = Loc.Get(Constants.Strings.MenuEdit) };
		editItem.Click += (_, _) => EditPreset(preset);
		var deleteItem = new MenuItem { Header = Loc.Get(Constants.Strings.MenuDelete) };
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
			Text = "+",
			FontSize = Constants.UI.AddCell.PlusFontSize,
			Foreground = Brush(Constants.Resources.BrushTextDim),
			TextAlignment = TextAlignment.Center,
		};
		var label = new TextBlock
		{
			Text = Loc.Get(Constants.Strings.AddPreset),
			Foreground = Brush(Constants.Resources.BrushTextDim),
			TextAlignment = TextAlignment.Center,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(8, 4, 8, 0),
		};
		var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
		panel.Children.Add(plus);
		panel.Children.Add(label);

		var cell = new Border
		{
			Width = Constants.UI.Cell.Size,
			Height = Constants.UI.Cell.Size,
			Margin = new Thickness(Constants.UI.Cell.Margin),
			CornerRadius = new CornerRadius(Constants.UI.Cell.CornerRadius),
			Background = System.Windows.Media.Brushes.Transparent,
			BorderThickness = new Thickness(Constants.UI.Cell.BorderThickness),
			BorderBrush = Brush(Constants.Resources.BrushBorder),
			Child = panel,
			Cursor = Cursors.Hand,
			ToolTip = new ToolTip { Content = Loc.Get(Constants.Strings.AddPresetHint) },
			AllowDrop = true,
		};

		cell.MouseEnter += (_, _) => cell.BorderBrush = Brush(Constants.Resources.BrushAccent);
		cell.MouseLeave += (_, _) => cell.BorderBrush = Brush(Constants.Resources.BrushBorder);
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
				values[role.RegistryName] = path != null && File.Exists(path) ? path : "";
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
			MessageBox.Show(Loc.Format(Constants.Strings.ErrorApplyFailed, ex.Message),
				Loc.Get(Constants.Strings.ErrorTitle), MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	private void ApplyDefault()
	{
		RegistryCursorService.SaveSnapshotToDisk(RegistryCursorService.TakeSnapshot());

		// У дефолтной ячейки свой сохранённый размер (settings.json) —
		// сброс курсоров не сбрасывает выбранный для неё размер.
		var defaultSize = AppState.GetDefaultBaseSize();
		RegistryCursorService.ApplyValues(RegistryCursorService.GetWindowsDefaultValues());
		RegistryCursorService.SetBaseSize(defaultSize);

		_activePresetId = null;
		AppState.SetActivePresetId(null);

		SetSliderSilently(defaultSize);

		ReloadGallery();
		UpdateUndoButton();
	}

	private void OnUndoButtonClick(object sender, RoutedEventArgs e)
	{
		var snapshot = RegistryCursorService.LoadSnapshotFromDisk();

		if (snapshot == null)
			return;

		RegistryCursorService.SaveSnapshotToDisk(RegistryCursorService.TakeSnapshot());
		RegistryCursorService.RestoreSnapshot(snapshot);

		_activePresetId = FindPresetIdByValues(snapshot.Values);
		AppState.SetActivePresetId(_activePresetId);

		SetSliderSilently(snapshot.BaseSize);

		ReloadGallery();
		UpdateUndoButton();
	}

	private string? FindPresetIdByValues(IReadOnlyDictionary<string, string> values)
	{
		if (!values.TryGetValue(Constants.Cursor.ArrowRoleName, out var arrow) || string.IsNullOrEmpty(arrow))
			return null;

		return _presets.FirstOrDefault(p =>
			string.Equals(PresetStore.GetRoleFilePath(p, Constants.Cursor.ArrowRoleName), arrow,
				StringComparison.OrdinalIgnoreCase))?.Id;
	}

	private void UpdateUndoButton() =>
		UndoButton.IsEnabled = RegistryCursorService.LoadSnapshotFromDisk() != null;

	private void DeletePreset(Preset preset)
	{
		var answer = MessageBox.Show(
			Loc.Format(Constants.Strings.ConfirmDeleteText, preset.Name),
			Loc.Get(Constants.Strings.ConfirmDeleteTitle),
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
			var saved = PresetStore.Save(editor.Result);

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

		// Ползунок только выбирает значение — применение и сохранение по кнопке.
		var sizePx = Constants.Cursor.SizeStep + (int)e.NewValue * Constants.Cursor.SizeStep;
		SizeValueText.Text = $"{sizePx} {Constants.UI.PixelSuffix}";
	}

	private void OnApplySizeButtonClick(object sender, RoutedEventArgs e)
	{
		var sizePx = Constants.Cursor.SizeStep + (int)SizeSlider.Value * Constants.Cursor.SizeStep;
		ApplyAndPersistSize(sizePx);
	}

	/// <summary>
	/// Применяет размер к системе и сохраняет его в активный пресет
	/// (или в настройки дефолтной ячейки), обновляя текст на её ячейке.
	/// </summary>
	public void ApplyAndPersistSize(int sizePx)
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
			_activeCellSizeText.Text = $"{sizePx} {Constants.UI.PixelSuffix}";
	}

	/// <summary>Синхронизация верхнего ползунка из редактора пресета.</summary>
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
				result.AddRange(Directory.EnumerateFiles(path, "*.*", SearchOption.TopDirectoryOnly)
					.Where(IsCursorFile));
			else if (IsCursorFile(path))
				result.Add(path);
		}

		return result;
	}

	private static bool IsCursorFile(string path)
	{
		var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();

		return ext is Constants.Files.CurExtension or Constants.Files.AniExtension;
	}
}

public sealed class RelayUiCommand(Action execute) : ICommand
{
	public event EventHandler? CanExecuteChanged { add { } remove { } }
	public bool CanExecute(object? parameter) => true;
	public void Execute(object? parameter) => execute();
}
