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
	private bool _sizeSliderReady;

	public MainWindow()
	{
		InitializeComponent();

		_activePresetId = AppState.GetActivePresetId();

		var size = RegistryCursorService.GetBaseSize();
		SizeSlider.Value = (size - Constants.Cursor.SizeStep) / (double)Constants.Cursor.SizeStep;
		SizeValueText.Text = $"{size} {Constants.UI.PixelSuffix}";
		_sizeSliderReady = true;

		ReloadGallery();
		UpdateUndoButton();
	}

	private void ReloadGallery()
	{
		_presets = PresetStore.LoadAll();
		Gallery.Items.Clear();

		foreach (var preset in _presets)
			Gallery.Items.Add(CreatePresetCell(preset));

		Gallery.Items.Add(CreateAddCell());
		EmptyHint.Visibility = _presets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
	}

	private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

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

		var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
		panel.Children.Add(image);
		panel.Children.Add(nameText);
		panel.Children.Add(countText);

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
		cell.Drop += Window_Drop;

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

	private void ResetButton_Click(object sender, RoutedEventArgs e)
	{
		RegistryCursorService.SaveSnapshotToDisk(RegistryCursorService.TakeSnapshot());
		RegistryCursorService.ResetToWindowsDefault();

		_activePresetId = null;
		AppState.SetActivePresetId(null);

		_sizeSliderReady = false;
		SizeSlider.Value = 1;
		SizeValueText.Text = $"{RegistryCursorService.DefaultBaseSize} {Constants.UI.PixelSuffix}";
		_sizeSliderReady = true;

		ReloadGallery();
		UpdateUndoButton();
	}

	private void UndoButton_Click(object sender, RoutedEventArgs e)
	{
		var snapshot = RegistryCursorService.LoadSnapshotFromDisk();
		if (snapshot == null) return;

		RegistryCursorService.SaveSnapshotToDisk(RegistryCursorService.TakeSnapshot());
		RegistryCursorService.RestoreSnapshot(snapshot);

		_activePresetId = FindPresetIdByValues(snapshot.Values);
		AppState.SetActivePresetId(_activePresetId);

		_sizeSliderReady = false;
		SizeSlider.Value = (snapshot.BaseSize - Constants.Cursor.SizeStep) / (double)Constants.Cursor.SizeStep;
		SizeValueText.Text = $"{snapshot.BaseSize} {Constants.UI.PixelSuffix}";
		_sizeSliderReady = true;

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
		if (answer != MessageBoxResult.Yes) return;

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

	private void SizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		var sizePx = Constants.Cursor.SizeStep + (int)e.NewValue * Constants.Cursor.SizeStep;
		SizeValueText.Text = $"{sizePx} {Constants.UI.PixelSuffix}";
		if (!_sizeSliderReady) return;
		RegistryCursorService.SetBaseSize(sizePx);
	}

	private void Window_DragOver(object sender, DragEventArgs e)
	{
		e.Effects = GetDroppedCursorFiles(e).Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;
		e.Handled = true;
	}

	private void Window_Drop(object sender, DragEventArgs e)
	{
		var files = GetDroppedCursorFiles(e);
		if (files.Count == 0) return;
		e.Handled = true;
		OpenEditor(null, files);
	}

	private static List<string> GetDroppedCursorFiles(DragEventArgs e)
	{
		var result = new List<string>();
		if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return result;

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
