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

	private const string PixelSuffix = "px";
	private const string LocGroupMembersCount = "S.Group.MembersCount";

	private readonly List<(PackageEntry Entry, Border Cell, TextBlock SizeText)> _tiles = new();
	private readonly List<(PackageGroupEntry Group, Border Cell)> _groupTiles = new();

	public IReadOnlyList<PackageEntry> SelectedEntries { get; private set; } = Array.Empty<PackageEntry>();

	public IReadOnlyList<PackageGroupEntry> SelectedGroups { get; private set; } = Array.Empty<PackageGroupEntry>();

	public bool IgnoreIndividualSizes { get; private set; }

	public int UniformSize { get; private set; }

	public ImportPickerWindow(IReadOnlyList<PackageEntry> entries, IReadOnlyList<PackageGroupEntry>? groups = null)
	{
		InitializeComponent();

		var uiScale = AppState.GetUiScale();
		UiScaleTransform.ScaleX = uiScale;
		UiScaleTransform.ScaleY = uiScale;

		foreach (var group in groups ?? Array.Empty<PackageGroupEntry>())
			_groupTiles.Add((group, CreateGroupTile(group)));

		foreach (var entry in entries)
		{
			var tile = CreateTile(entry);
			_tiles.Add((entry, tile.Cell, tile.SizeText));
		}

		EmptyHint.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

		var defaultSize = AppState.GetDefaultBaseSize();
		UniformSizeSlider.Value = (defaultSize - RegistryCursorService.SizeStep) / (double)RegistryCursorService.SizeStep;
		UniformSizeValueText.Text = $"{defaultSize} {PixelSuffix}";

		UpdateSelectionCount();
	}

	private Border CreateGroupTile(PackageGroupEntry group)
	{
		var colorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(GroupColors.ResolveHex(group.ColorKey))!);

		var nameText = new TextBlock
		{
			Text = group.Name,
			FontSize = CellNameFontSize,
			FontWeight = FontWeights.SemiBold,
			Foreground = System.Windows.Media.Brushes.White,
			TextTrimming = TextTrimming.CharacterEllipsis,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(4, 6, 4, 0),
		};

		var countText = new TextBlock
		{
			Text = Loc.Format(LocGroupMembersCount, group.MemberKeys.Count),
			Foreground = System.Windows.Media.Brushes.White,
			Opacity = 0.85,
			FontSize = CellCountFontSize,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(0, 2, 0, 0),
		};

		var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
		panel.Children.Add(nameText);
		panel.Children.Add(countText);

		var cell = new Border
		{
			Width = CellSize,
			Height = CellSize,
			Margin = new Thickness(CellMargin),
			CornerRadius = new CornerRadius(CellCornerRadius),
			Background = colorBrush,
			BorderThickness = new Thickness(CellBorderThickness),
			BorderBrush = colorBrush,
			Child = panel,
			Cursor = Cursors.Hand,
		};

		SetSelected(cell, true);

		cell.MouseLeftButtonUp += (_, _) =>
		{
			var selecting = !IsSelected(cell);
			SetSelected(cell, selecting);

			foreach (var memberKey in group.MemberKeys)
			{
				var memberTile = _tiles.FirstOrDefault(t => t.Entry.Key == memberKey);
				if (memberTile.Cell != null)
					SetSelected(memberTile.Cell, selecting);
			}

			UpdateSelectionCount();
		};

		Gallery.Items.Add(cell);

		return cell;
	}

	private void SyncGroupTileSelections()
	{
		foreach (var (group, cell) in _groupTiles)
		{
			var allMembersSelected = group.MemberKeys.Count > 0 && group.MemberKeys.All(memberKey =>
				_tiles.FirstOrDefault(t => t.Entry.Key == memberKey) is { Cell: not null } match &&
				IsSelected(match.Cell));

			SetSelected(cell, allMembersSelected);
		}
	}

	private void OnInfoButtonClick(object sender, RoutedEventArgs e)
	{
		new InfoHelpWindow(Loc.Get(LocInfoTitle), Services.HelpTextService.Get("Import")) { Owner = this }.ShowDialog();
	}

	private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

	private static bool IsSelected(Border cell) => cell.Tag is true;

	private static void SetSelected(Border cell, bool selected)
	{
		cell.Tag = selected;
		cell.BorderBrush = selected ? Brush(BrushAccent) : Brush(BrushBorder);
	}

	private (Border Cell, TextBlock SizeText) CreateTile(PackageEntry entry)
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

		var sizeText = new TextBlock
		{
			Text = $"{entry.BaseSize} {PixelSuffix}",
			Foreground = Brush(BrushTextDim),
			FontSize = CellCountFontSize,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(0, 1, 0, 0),
		};

		var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
		panel.Children.Add(image);
		panel.Children.Add(nameText);
		panel.Children.Add(countText);
		panel.Children.Add(sizeText);

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

		return (cell, sizeText);
	}

	private void UpdateSelectionCount()
	{
		SyncGroupTileSelections();

		var selected = _tiles.Count(tile => IsSelected(tile.Cell));
		SelectionCountText.Text = Loc.Format(LocImportSelectionCount, selected, _tiles.Count);
		ImportButton.IsEnabled = selected > 0;
	}

	private void OnSelectAllClick(object sender, RoutedEventArgs e)
	{
		foreach (var (_, cell, _) in _tiles)
			SetSelected(cell, true);

		UpdateSelectionCount();
	}

	private void OnSelectNoneClick(object sender, RoutedEventArgs e)
	{
		foreach (var (_, cell, _) in _tiles)
			SetSelected(cell, false);

		UpdateSelectionCount();
	}

	private void OnImportButtonClick(object sender, RoutedEventArgs e)
	{
		SelectedEntries = _tiles.Where(tile => IsSelected(tile.Cell)).Select(tile => tile.Entry).ToList();
		SelectedGroups = _groupTiles.Where(tile => IsSelected(tile.Cell)).Select(tile => tile.Group).ToList();

		if (SelectedEntries.Count == 0)
			return;

		IgnoreIndividualSizes = IgnoreSizesCheck.IsChecked == true;
		UniformSize = RegistryCursorService.SizeStep + (int)UniformSizeSlider.Value * RegistryCursorService.SizeStep;

		DialogResult = true;
	}

	private void OnIgnoreSizesChanged(object sender, RoutedEventArgs e)
	{
		var ignore = IgnoreSizesCheck.IsChecked == true;
		UniformSizeRow.Visibility = ignore ? Visibility.Visible : Visibility.Collapsed;
		UpdateTileSizes();
	}

	private void OnUniformSizeSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (UniformSizeValueText == null)
			return;

		var sizePx = RegistryCursorService.SizeStep + (int)e.NewValue * RegistryCursorService.SizeStep;
		UniformSizeValueText.Text = $"{sizePx} {PixelSuffix}";
		UpdateTileSizes();
	}

	private void UpdateTileSizes()
	{
		var ignore = IgnoreSizesCheck?.IsChecked == true;
		var uniformPx = RegistryCursorService.SizeStep + (int)UniformSizeSlider.Value * RegistryCursorService.SizeStep;

		foreach (var (entry, _, sizeText) in _tiles)
			sizeText.Text = ignore ? $"{uniformPx} {PixelSuffix}" : $"{entry.BaseSize} {PixelSuffix}";
	}
}
