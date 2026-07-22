using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CursorPalette.Models;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class ImportPickerWindow : Window
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

	private const string LocImportSelectionCount = "S.Import.SelectionCount";
	private const string LocInfoTitle = "S.Info.Title";
	private const string LocInfoImport = "S.Info.Import";

	private readonly List<(PackageEntry Entry, Border Cell)> _tiles = new();

	public IReadOnlyList<PackageEntry> SelectedEntries { get; private set; } = Array.Empty<PackageEntry>();

	public ImportPickerWindow(IReadOnlyList<PackageEntry> entries)
	{
		InitializeComponent();

		var uiScale = AppState.GetUiScale();
		UiScaleTransform.ScaleX = uiScale;
		UiScaleTransform.ScaleY = uiScale;

		foreach (var entry in entries)
			_tiles.Add((entry, CreateTile(entry)));

		EmptyHint.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

		UpdateSelectionCount();
	}

	private void OnInfoButtonClick(object sender, RoutedEventArgs e)
	{
		new InfoHelpWindow(Loc.Get(LocInfoTitle), Loc.Get(LocInfoImport)) { Owner = this }.ShowDialog();
	}

	private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

	private static bool IsSelected(Border cell) => cell.Tag is true;

	private static void SetSelected(Border cell, bool selected)
	{
		cell.Tag = selected;
		cell.BorderBrush = selected ? Brush(BrushAccent) : Brush(BrushBorder);
	}

	private Border CreateTile(PackageEntry entry)
	{
		var image = new Image { Width = CellPreviewSize, Height = CellPreviewSize, SnapsToDevicePixels = true };
		RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
		CursorPreviewService.ApplyPreview(image, entry.PreviewPath);

		var nameText = new TextBlock
		{
			Text = entry.DisplayName,
			FontSize = CellNameFontSize,
			TextTrimming = TextTrimming.CharacterEllipsis,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(4, 6, 4, 0),
		};

		var countText = new TextBlock
		{
			Text = $"{entry.RoleCount}/{CursorRoles.All.Length}",
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

	private void UpdateSelectionCount()
	{
		var selected = _tiles.Count(tile => IsSelected(tile.Cell));
		SelectionCountText.Text = Loc.Format(LocImportSelectionCount, selected, _tiles.Count);
		ImportButton.IsEnabled = selected > 0;
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

	private void OnImportButtonClick(object sender, RoutedEventArgs e)
	{
		SelectedEntries = _tiles.Where(tile => IsSelected(tile.Cell)).Select(tile => tile.Entry).ToList();

		if (SelectedEntries.Count == 0)
			return;

		DialogResult = true;
	}
}
