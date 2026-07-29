using System.IO;
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
	private const double GroupIndicatorSize = 10;

	private const string BrushAccent = "Brush.Accent";
	private const string BrushBorder = "Brush.Border";
	private const string BrushSurface = "Brush.Surface";
	private const string BrushTextDim = "Brush.TextDim";

	private const string LocImportSelectionCount = "S.Import.SelectionCount";
	private const string LocInfoTitle = "S.Info.Title";
	private const string LocInfoImport = "S.Info.Import";

	private const string PixelSuffix = "px";
	private const string LocGroupMembersCount = "S.Group.MembersCount";
	private const string ExpandIconUri = "pack://application:,,,/Resources/ExpandIcon32.png";
	private const string StairIconUri = "pack://application:,,,/Resources/StairIcon24.png";

	private readonly List<(PackageEntry Entry, Border Cell, TextBlock SizeText)> _tiles = new();
	private readonly List<(PackageGroupEntry Group, Border Cell)> _groupTiles = new();
	private readonly Dictionary<string, string> _entryToColorKey = new();
	private IReadOnlyList<Preset> _existingPresets = Array.Empty<Preset>();
	private DetectedPackage? _package;

	public IReadOnlyList<PackageEntry> SelectedEntries { get; private set; } = Array.Empty<PackageEntry>();

	public IReadOnlyList<PackageGroupEntry> SelectedGroups { get; private set; } = Array.Empty<PackageGroupEntry>();

	public bool IgnoreIndividualSizes { get; private set; }

	public int UniformSize { get; private set; }

	public ImportPickerWindow(DetectedPackage package, IReadOnlyList<Preset>? existingPresets = null)
	{
		_package = package;
		_existingPresets = existingPresets ?? Array.Empty<Preset>();
		InitializeComponent();

		var uiScale = AppState.GetUiScale();
		UiScaleTransform.ScaleX = uiScale;
		UiScaleTransform.ScaleY = uiScale;

		var entries = package.Entries;
		var groups = package.Groups;

		foreach (var group in groups)
		{
			_groupTiles.Add((group, CreateGroupTile(group)));

			foreach (var memberKey in group.MemberKeys)
				_entryToColorKey[memberKey] = group.ColorKey;
		}

		foreach (var entry in entries)
		{
			var tile = CreateTile(entry);
			_tiles.Add((entry, tile.Cell, tile.SizeText));
		}

		EmptyHint.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

		var defaultSize = AppState.GetDefaultBaseSize();
		UniformSizeSlider.Value = (defaultSize - RegistryCursorService.SizeStep) / (double)RegistryCursorService.SizeStep;
		UniformSizeValueText.Text = $"{defaultSize} {PixelSuffix}";

		ApplyHideExisting();
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
				if (memberTile.Cell != null && memberTile.Cell.Visibility == Visibility.Visible)
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
			var visibleMembers = group.MemberKeys
				.Select(key => _tiles.FirstOrDefault(t => t.Entry.Key == key))
				.Where(t => t.Cell != null && t.Cell.Visibility == Visibility.Visible)
				.ToList();

			var allMembersSelected = visibleMembers.Count > 0 && visibleMembers.All(match => IsSelected(match.Cell));

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

		var content = new Grid();
		content.Children.Add(panel);

		if (_entryToColorKey.TryGetValue(entry.Key, out var colorKey))
		{
			content.Children.Add(new Border
			{
				Width = GroupIndicatorSize,
				Height = GroupIndicatorSize,
				CornerRadius = new CornerRadius(GroupIndicatorSize),
				Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(GroupColors.ResolveHex(colorKey))!),
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Top,
				Margin = new Thickness(6),
			});
		}

		if (entry.UseScaling)
		{
			var scalingIcon = new Image
			{
				Width = 16,
				Height = 16,
				SnapsToDevicePixels = true,
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Bottom,
				Margin = new Thickness(0, 0, 6, 6),
				IsHitTestVisible = false,
				Source = new System.Windows.Media.Imaging.BitmapImage(
					new Uri((ScaleMode)entry.ScaleMode == ScaleMode.NearestNeighbor ? StairIconUri : ExpandIconUri)),
			};
			RenderOptions.SetBitmapScalingMode(scalingIcon, BitmapScalingMode.NearestNeighbor);
			content.Children.Add(scalingIcon);
		}

		var cell = new Border
		{
			Width = CellSize,
			Height = CellSize,
			Margin = new Thickness(CellMargin),
			CornerRadius = new CornerRadius(CellCornerRadius),
			Background = Brush(BrushSurface),
			BorderThickness = new Thickness(CellBorderThickness),
			Child = content,
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

		var visibleTiles = _tiles.Where(tile => tile.Cell.Visibility == Visibility.Visible).ToList();
		var selected = visibleTiles.Count(tile => IsSelected(tile.Cell));
		SelectionCountText.Text = Loc.Format(LocImportSelectionCount, selected, visibleTiles.Count);
		ImportButton.IsEnabled = selected > 0;
	}

	private void OnSelectAllClick(object sender, RoutedEventArgs e)
	{
		foreach (var (_, cell, _) in _tiles.Where(t => t.Cell.Visibility == Visibility.Visible))
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
		SelectedEntries = _tiles.Where(tile => tile.Cell.Visibility == Visibility.Visible && IsSelected(tile.Cell))
			.Select(tile => tile.Entry).ToList();
		SelectedGroups = _groupTiles.Where(tile => tile.Cell.Visibility == Visibility.Visible && IsSelected(tile.Cell))
			.Select(tile => tile.Group).ToList();

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

	private void OnHideExistingChanged(object sender, RoutedEventArgs e)
	{
		ApplyHideExisting();
	}

	private void ApplyHideExisting()
	{
		var hide = HideExistingCheck.IsChecked == true;

		foreach (var (entry, cell, _) in _tiles)
		{
			var isExisting = IsEntryExisting(entry);
			cell.Visibility = hide && isExisting ? Visibility.Collapsed : Visibility.Visible;

			if (hide && isExisting)
				SetSelected(cell, false);
		}

		foreach (var (group, cell) in _groupTiles)
		{
			var allMembersHidden = group.MemberKeys.Count > 0 && group.MemberKeys.All(memberKey =>
				_tiles.FirstOrDefault(t => t.Entry.Key == memberKey) is { Cell: not null } match &&
				match.Cell.Visibility == Visibility.Collapsed);

			cell.Visibility = hide && allMembersHidden ? Visibility.Collapsed : Visibility.Visible;

			if (hide && allMembersHidden)
				SetSelected(cell, false);
		}

		UpdateSelectionCount();
	}

	private bool IsEntryExisting(PackageEntry entry)
	{
		if (_package == null)
			return false;

		var draft = PresetPackageService.BuildDraft(_package, entry);
		if (draft == null || draft.RoleSources.Count == 0)
			return false;

		var entryRoles = draft.RoleSources
			.Where(kv => kv.Value.OwnFilePath != null)
			.ToDictionary(kv => kv.Key, kv => kv.Value.OwnFilePath!);

		if (entryRoles.Count == 0)
			return false;

		foreach (var preset in _existingPresets)
		{
			var presetRoleCount = preset.Roles.Count + preset.RoleRefs.Count;
			if (presetRoleCount != entryRoles.Count)
				continue;

			var allMatch = true;
			foreach (var (role, entryPath) in entryRoles)
			{
				var presetPath = PresetStore.GetRoleFilePath(preset, role);
				if (presetPath == null || !File.Exists(presetPath) || !File.Exists(entryPath))
				{
					allMatch = false;
					break;
				}

				if (!File.ReadAllBytes(entryPath).SequenceEqual(File.ReadAllBytes(presetPath)))
				{
					allMatch = false;
					break;
				}
			}

			if (allMatch)
				return true;
		}

		return false;
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
