using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CursorPalette.Models;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class ExportWindow : Window
{
	private const double CellSize = 120;
	private const double CellMargin = 6;
	private const double CellPreviewSize = 40;
	private const double CellCornerRadius = 10;
	private const double CellBorderThickness = 2;
	private const double CellNameFontSize = 12;
	private const double CellCountFontSize = 10;

	private const string BrushAccent = "Brush.Accent";
	private const string BrushBorder = "Brush.Border";
	private const string BrushSurface = "Brush.Surface";
	private const string BrushTextDim = "Brush.TextDim";

	private const string LocSelectionCount = "S.Import.SelectionCount";
	private const string LocToastExportedBundle = "S.Toast.ExportedBundle";
	private const string LocToastExportedArchive = "S.Toast.ExportedArchive";
	private const string LocErrorTitle = "S.Error.Title";
	private const string LocErrorExportFailed = "S.Error.ExportFailed";
	private const string LocInfoTitle = "S.Info.Title";
	private const string LocInfoExport = "S.Info.Export";

	private readonly List<(Preset Preset, Border Cell)> _tiles = new();

	public ExportWindow(IReadOnlyList<Preset> presets)
	{
		InitializeComponent();

		var uiScale = AppState.GetUiScale();
		UiScaleTransform.ScaleX = uiScale;
		UiScaleTransform.ScaleY = uiScale;

		foreach (var preset in presets)
			_tiles.Add((preset, CreateTile(preset)));

		EmptyHint.Visibility = presets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

		UpdateSelectionCount();
	}

	private void OnInfoButtonClick(object sender, RoutedEventArgs e)
	{
		new InfoHelpWindow(Loc.Get(LocInfoTitle), Loc.Get(LocInfoExport)) { Owner = this }.ShowDialog();
	}

	private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

	private static bool IsSelected(Border cell) => cell.Tag is true;

	private static void SetSelected(Border cell, bool selected)
	{
		cell.Tag = selected;
		cell.BorderBrush = selected ? Brush(BrushAccent) : Brush(BrushBorder);
	}

	private Border CreateTile(Preset preset)
	{
		var previewPath = PresetStore.GetRoleFilePath(preset, CursorRoles.ArrowRoleName)
							?? preset.Roles.Keys.Concat(preset.RoleRefs.Keys)
								.Select(role => PresetStore.GetRoleFilePath(preset, role))
								.FirstOrDefault(path => path != null);

		var image = new Image { Width = CellPreviewSize, Height = CellPreviewSize, SnapsToDevicePixels = true };
		RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
		CursorPreviewService.ApplyPreview(image, previewPath);

		var nameText = new TextBlock
		{
			Text = preset.Name,
			FontSize = CellNameFontSize,
			TextTrimming = TextTrimming.CharacterEllipsis,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(4, 6, 4, 0),
		};

		var countText = new TextBlock
		{
			Text = $"{preset.Roles.Count + preset.RoleRefs.Count}/{CursorRoles.All.Length}",
			Foreground = Brush(BrushTextDim),
			FontSize = CellCountFontSize,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(0, 2, 0, 0),
		};

		var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
		panel.Children.Add(image);
		panel.Children.Add(nameText);
		panel.Children.Add(countText);

		var cell = new Border
		{
			Width = CellSize,
			Height = CellSize,
			Margin = new Thickness(CellMargin),
			CornerRadius = new CornerRadius(CellCornerRadius),
			Background = Brush(BrushSurface),
			BorderThickness = new Thickness(CellBorderThickness),
			Child = panel,
			Cursor = Cursors.Hand,
		};

		SetSelected(cell, true);

		cell.MouseLeftButtonUp += (_, _) =>
		{
			SetSelected(cell, !IsSelected(cell));
			UpdateSelectionCount();
		};

		Gallery.Items.Add(cell);
		return cell;
	}

	private List<Preset> GetSelectedPresets() =>
		_tiles.Where(tile => IsSelected(tile.Cell)).Select(tile => tile.Preset).ToList();

	private void UpdateSelectionCount()
	{
		var selected = _tiles.Count(tile => IsSelected(tile.Cell));
		SelectionCountText.Text = Loc.Format(LocSelectionCount, selected, _tiles.Count);

		ExportBundleButton.IsEnabled = selected > 0;
		ExportArchiveButton.IsEnabled = selected > 0;
	}

	private void OnSelectAllClick(object sender, RoutedEventArgs e)
	{
		foreach (var (_, cell) in _tiles)
			SetSelected(cell, true);

		UpdateSelectionCount();
	}

	private void OnSelectNoneClick(object sender, RoutedEventArgs e)
	{
		foreach (var (_, cell) in _tiles)
			SetSelected(cell, false);

		UpdateSelectionCount();
	}

	private void OnExportBundleClick(object sender, RoutedEventArgs e)
	{
		var selected = GetSelectedPresets();

		if (selected.Count == 0)
			return;

		try
		{
			var (path, count) = PresetPackageService.ExportBundle(selected);
			ToastService.Show(RootGrid, Loc.Format(LocToastExportedBundle, count, System.IO.Path.GetFileName(path)));
			RevealInExplorer(path);
		}
		catch (Exception exception)
		{
			MessageBox.Show(Loc.Format(LocErrorExportFailed, exception.Message),
				Loc.Get(LocErrorTitle), MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	private void OnExportArchiveClick(object sender, RoutedEventArgs e)
	{
		var selected = GetSelectedPresets();
		if (selected.Count == 0)
			return;

		try
		{
			var (path, count) = PresetPackageService.ExportArchive(selected);
			ToastService.Show(RootGrid, Loc.Format(LocToastExportedArchive, count, System.IO.Path.GetFileName(path)));
			RevealInExplorer(path);
		}
		catch (Exception exception)
		{
			MessageBox.Show(Loc.Format(LocErrorExportFailed, exception.Message),
				Loc.Get(LocErrorTitle), MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	private static void RevealInExplorer(string path)
	{
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = "explorer.exe",
				ArgumentList = { $"/select,\"{path}\"" },
				UseShellExecute = true,
			});
		}
		catch
		{
		}
	}
}
