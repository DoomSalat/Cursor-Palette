using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using CursorPalette.Linux.Services;
using CursorPalette.Models;
using CursorPalette.Services;

namespace CursorPalette.Linux.Views;

public class ImportPickerWindow : Window
{
	private const double CellSize = 120;
	private const double CellMargin = 6;
	private const double CellPreviewSize = 40;
	private const double CellCornerRadius = 10;
	private const double CellBorderThickness = 2;
	private const double CellNameFontSize = 12;
	private const double CellCountFontSize = 10;
	private const double GroupIndicatorSize = 10;
	private const double DialogWidth = 580;
	private const double DialogHeight = 560;
	private const double DialogMinWidth = 380;
	private const double DialogMinHeight = 320;
	private const double DialogPadding = 16;
	private const double CloseButtonMinWidth = 90;

	private const string LocSelectionCount = "S.Import.SelectionCount";
	private const string LocImportTitle = "S.Import.Title";
	private const string LocImportConfirm = "S.Import.Confirm";
	private const string LocImportEmpty = "S.Import.Empty";
	private const string LocImportIgnoreSizes = "S.Import.IgnoreSizes";
	private const string LocExportSelectAll = "S.SelectAll";
	private const string LocExportSelectNone = "S.SelectNone";
	private const string LocGroupMembersCount = "S.Group.MembersCount";
	private const string PixelSuffix = "px";

	private readonly List<(PackageEntry Entry, Border Cell, TextBlock SizeText)> _tiles = new();
	private readonly List<(PackageGroupEntry Group, Border Cell)> _groupTiles = new();
	private readonly Dictionary<string, string> _entryToColorKey = new();
	private readonly Panel _toastHost;

	private TextBlock? _selectionCountText;
	private Button? _importButton;
	private CheckBox? _ignoreSizesCheck;
	private Slider? _uniformSizeSlider;
	private TextBlock? _uniformSizeValueText;
	private Grid? _uniformSizeRow;

	public IReadOnlyList<PackageEntry> SelectedEntries { get; private set; } = Array.Empty<PackageEntry>();
	public IReadOnlyList<PackageGroupEntry> SelectedGroups { get; private set; } = Array.Empty<PackageGroupEntry>();
	public bool IgnoreIndividualSizes { get; private set; }
	public int UniformSize { get; private set; }

	public ImportPickerWindow(IReadOnlyList<PackageEntry> entries, IReadOnlyList<PackageGroupEntry>? groups, Panel toastHost)
	{
		_toastHost = toastHost;

		Title = Loc.Get(LocImportTitle);
		Width = DialogWidth;
		Height = DialogHeight;
		MinWidth = DialogMinWidth;
		MinHeight = DialogMinHeight;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;

		var root = new Grid
		{
			RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto,Auto"),
		};

		var headerBorder = new Border
		{
			Padding = new Thickness(DialogPadding, 10),
			BorderThickness = new Thickness(0, 0, 0, 1),
		};
		ThemeManager.BindResource(headerBorder, Border.BackgroundProperty, "SystemControlDefaultChromeMediumBrush");
		ThemeManager.BindResource(headerBorder, Border.BorderBrushProperty, "SystemControlBorderBrush");

		var headerGrid = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
		};

		_selectionCountText = new TextBlock
		{
			FontSize = 13,
			VerticalAlignment = VerticalAlignment.Center,
		};
		Grid.SetColumn(_selectionCountText, 0);
		headerGrid.Children.Add(_selectionCountText);

		var selectAllButton = new Button
		{
			Content = Loc.Get(LocExportSelectAll),
			Padding = new Thickness(8, 2),
			Margin = new Thickness(4, 0),
		};
		selectAllButton.Click += (_, _) => SelectAll();
		Grid.SetColumn(selectAllButton, 1);
		headerGrid.Children.Add(selectAllButton);

		var selectNoneButton = new Button
		{
			Content = Loc.Get(LocExportSelectNone),
			Padding = new Thickness(8, 2),
		};
		selectNoneButton.Click += (_, _) => SelectNone();
		Grid.SetColumn(selectNoneButton, 2);
		headerGrid.Children.Add(selectNoneButton);

		headerBorder.Child = headerGrid;
		Grid.SetRow(headerBorder, 0);
		root.Children.Add(headerBorder);

		var scrollViewer = new ScrollViewer
		{
			Margin = new Thickness(DialogPadding, 8, DialogPadding, 4),
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
		};

		var gallery = new WrapPanel();

		foreach (var group in groups ?? Array.Empty<PackageGroupEntry>())
		{
			_groupTiles.Add((group, CreateGroupTile(group, gallery)));

			foreach (var memberKey in group.MemberKeys)
				_entryToColorKey[memberKey] = group.ColorKey;
		}

		foreach (var entry in entries)
		{
			var (cell, sizeText) = CreateTile(entry, gallery);
			_tiles.Add((entry, cell, sizeText));
		}

		if (entries.Count == 0)
		{
			var emptyHint = new TextBlock
			{
				Text = Loc.Get(LocImportEmpty),
				FontSize = 14,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0, 40, 0, 0),
			};
			ThemeManager.BindResource(emptyHint, TextBlock.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");
			gallery.Children.Add(emptyHint);
		}

		scrollViewer.Content = gallery;
		Grid.SetRow(scrollViewer, 1);
		root.Children.Add(scrollViewer);

		_ignoreSizesCheck = new CheckBox
		{
			Content = Loc.Get(LocImportIgnoreSizes),
			Margin = new Thickness(DialogPadding, 4, DialogPadding, 0),
		};
		_ignoreSizesCheck.IsCheckedChanged += OnIgnoreSizesChanged;
		Grid.SetRow(_ignoreSizesCheck, 2);
		root.Children.Add(_ignoreSizesCheck);

		_uniformSizeRow = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
			IsVisible = false,
			Margin = new Thickness(DialogPadding, 10, DialogPadding, 0),
		};
		Grid.SetRow(_uniformSizeRow, 3);

		var uniformSizeLabel = new TextBlock
		{
			Text = Loc.Get("S.CursorSize"),
			FontSize = 13,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0, 0, 8, 0),
		};
		Grid.SetColumn(uniformSizeLabel, 0);
		_uniformSizeRow.Children.Add(uniformSizeLabel);

		_uniformSizeSlider = new Slider
		{
			Minimum = 0,
			Maximum = 10,
			Value = (AppState.GetDefaultBaseSize() - CursorConstants.SizeStep) / (double)CursorConstants.SizeStep,
			VerticalAlignment = VerticalAlignment.Center,
		};
		_uniformSizeSlider.PropertyChanged += OnUniformSizeSliderChanged;
		Grid.SetColumn(_uniformSizeSlider, 1);
		_uniformSizeRow.Children.Add(_uniformSizeSlider);

		_uniformSizeValueText = new TextBlock
		{
			FontSize = 13,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(8, 0, 0, 0),
		};
		Grid.SetColumn(_uniformSizeValueText, 2);
		_uniformSizeRow.Children.Add(_uniformSizeValueText);

		root.Children.Add(_uniformSizeRow);

		UpdateUniformSizeText();

		var footerBorder = new Border
		{
			Padding = new Thickness(DialogPadding, 10),
			BorderThickness = new Thickness(0, 1, 0, 0),
		};
		ThemeManager.BindResource(footerBorder, Border.BackgroundProperty, "SystemControlDefaultChromeMediumBrush");
		ThemeManager.BindResource(footerBorder, Border.BorderBrushProperty, "SystemControlBorderBrush");

		var bottomBar = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Spacing = 8,
		};

		var cancelButton = new Button
		{
			Content = Loc.Get("S.Editor.Cancel"),
			MinWidth = CloseButtonMinWidth,
		};
		cancelButton.Click += (_, _) => Close();
		bottomBar.Children.Add(cancelButton);

		_importButton = new Button
		{
			Content = Loc.Get(LocImportConfirm),
			MinWidth = CloseButtonMinWidth,
			Classes = { "accent" },
		};
		_importButton.Click += OnImportClick;
		bottomBar.Children.Add(_importButton);

		footerBorder.Child = bottomBar;
		Grid.SetRow(footerBorder, 4);
		root.Children.Add(footerBorder);

		Content = root;

		var uiScale = AppState.GetUiScale();
		if (uiScale != 1.0)
		{
			root.RenderTransform = new ScaleTransform(uiScale, uiScale);
			root.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
		}

		UpdateSelectionCount();
	}

	private Border CreateGroupTile(PackageGroupEntry group, WrapPanel gallery)
	{
		var colorBrush = new SolidColorBrush(Color.Parse(GroupColors.ResolveHex(group.ColorKey)));

		var panel = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Center,
			Spacing = 2,
		};

		panel.Children.Add(new TextBlock
		{
			Text = group.Name,
			FontSize = CellNameFontSize,
			FontWeight = FontWeight.SemiBold,
			Foreground = Brushes.White,
			TextTrimming = TextTrimming.CharacterEllipsis,
			TextAlignment = TextAlignment.Center,
			MaxWidth = CellSize - 12,
		});

		panel.Children.Add(new TextBlock
		{
			Text = Loc.Format(LocGroupMembersCount, group.MemberKeys.Count),
			Foreground = Brushes.White,
			Opacity = 0.85,
			FontSize = CellCountFontSize,
			TextAlignment = TextAlignment.Center,
		});

		var cell = new Border
		{
			Width = CellSize,
			Height = CellSize,
			Margin = new Thickness(CellMargin),
			CornerRadius = new CornerRadius(CellCornerRadius),
			BorderThickness = new Thickness(CellBorderThickness),
			BorderBrush = colorBrush,
			Background = colorBrush,
			Child = panel,
		};

		SetSelected(cell, true);

		cell.PointerPressed += (_, _) =>
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

		gallery.Children.Add(cell);
		return cell;
	}

	private (Border Cell, TextBlock SizeText) CreateTile(PackageEntry entry, WrapPanel gallery)
	{
		var preview = CursorPreviewService.GetPreview(entry.PreviewPath);

		var panel = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Center,
			Spacing = 2,
		};

		if (preview != null)
		{
			panel.Children.Add(new Image
			{
				Source = preview,
				Width = CellPreviewSize,
				Height = CellPreviewSize,
			});
		}

		panel.Children.Add(new TextBlock
		{
			Text = entry.DisplayName,
			FontSize = CellNameFontSize,
			TextTrimming = TextTrimming.CharacterEllipsis,
			TextAlignment = TextAlignment.Center,
			MaxWidth = CellSize - 12,
			Margin = new Thickness(4, 4, 4, 0),
		});

		var roleCountText = new TextBlock
		{
			Text = $"{entry.RoleCount}/{CursorRoles.All.Length}",
			FontSize = CellCountFontSize,
			TextAlignment = TextAlignment.Center,
		};
		ThemeManager.BindResource(roleCountText, TextBlock.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");
		panel.Children.Add(roleCountText);

		var sizeText = new TextBlock
		{
			Text = $"{entry.BaseSize} {PixelSuffix}",
			FontSize = CellCountFontSize,
			TextAlignment = TextAlignment.Center,
		};
		ThemeManager.BindResource(sizeText, TextBlock.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");
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
				Background = new SolidColorBrush(Color.Parse(GroupColors.ResolveHex(colorKey))),
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Top,
				Margin = new Thickness(6),
			});
		}

		if (entry.UseScaling)
		{
			content.Children.Add(new Image
			{
				Source = IconHelper.Load("StairIcon24.png"),
				Width = 12,
				Height = 12,
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Bottom,
				Margin = new Thickness(0, 0, 4, 4),
			});
		}

		var cell = new Border
		{
			Width = CellSize,
			Height = CellSize,
			Margin = new Thickness(CellMargin),
			CornerRadius = new CornerRadius(CellCornerRadius),
			BorderThickness = new Thickness(CellBorderThickness),
			Child = content,
		};

		SetSelected(cell, true);

		cell.PointerPressed += (_, _) =>
		{
			SetSelected(cell, !IsSelected(cell));
			UpdateSelectionCount();
		};

		gallery.Children.Add(cell);
		return (cell, sizeText);
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

	private static bool IsSelected(Border cell) => cell.Tag is true;

	private static void SetSelected(Border cell, bool selected)
	{
		cell.Tag = selected;
		ThemeManager.BindResource(cell, Border.BorderBrushProperty,
			selected ? "SystemAccentColor" : "SystemControlBorderBrush");
	}

	private void UpdateSelectionCount()
	{
		SyncGroupTileSelections();

		if (_selectionCountText == null)
			return;

		var selected = _tiles.Count(tile => IsSelected(tile.Cell));
		_selectionCountText.Text = Loc.Format(LocSelectionCount, selected, _tiles.Count);

		if (_importButton != null)
			_importButton.IsEnabled = selected > 0;
	}

	private void SelectAll()
	{
		foreach (var (_, cell, _) in _tiles)
			SetSelected(cell, true);
		UpdateSelectionCount();
	}

	private void SelectNone()
	{
		foreach (var (_, cell, _) in _tiles)
			SetSelected(cell, false);
		UpdateSelectionCount();
	}

	private void OnImportClick(object? sender, RoutedEventArgs e)
	{
		SelectedEntries = _tiles.Where(tile => IsSelected(tile.Cell)).Select(tile => tile.Entry).ToList();
		SelectedGroups = _groupTiles.Where(tile => IsSelected(tile.Cell)).Select(tile => tile.Group).ToList();

		if (SelectedEntries.Count == 0)
			return;

		IgnoreIndividualSizes = _ignoreSizesCheck?.IsChecked == true;
		UniformSize = CursorConstants.SizeStep + (int)Math.Round(_uniformSizeSlider?.Value ?? 0) * CursorConstants.SizeStep;

		Close(true);
	}

	private void OnIgnoreSizesChanged(object? sender, EventArgs e)
	{
		var ignore = _ignoreSizesCheck?.IsChecked == true;
		if (_uniformSizeRow != null)
			_uniformSizeRow.IsVisible = ignore;
		UpdateTileSizes();
	}

	private void OnUniformSizeSliderChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
	{
		UpdateUniformSizeText();
		UpdateTileSizes();
	}

	private void UpdateUniformSizeText()
	{
		if (_uniformSizeValueText == null || _uniformSizeSlider == null)
			return;

		var sizePx = CursorConstants.SizeStep + (int)Math.Round(_uniformSizeSlider.Value) * CursorConstants.SizeStep;
		_uniformSizeValueText.Text = $"{sizePx} {PixelSuffix}";
	}

	private void UpdateTileSizes()
	{
		var ignore = _ignoreSizesCheck?.IsChecked == true;
		var uniformPx = CursorConstants.SizeStep + (int)Math.Round(_uniformSizeSlider?.Value ?? 0) * CursorConstants.SizeStep;

		foreach (var (entry, _, sizeText) in _tiles)
			sizeText.Text = ignore ? $"{uniformPx} {PixelSuffix}" : $"{entry.BaseSize} {PixelSuffix}";
	}
}
