using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using CursorPalette.Linux.Services;
using CursorPalette.Models;
using CursorPalette.Services;

namespace CursorPalette.Linux.Views;

public class ExportWindow : Window
{
	private const double CellSize = 120;
	private const double CellMargin = 6;
	private const double CellPreviewSize = 40;
	private const double CellCornerRadius = 10;
	private const double CellBorderThickness = 2;
	private const double CellNameFontSize = 12;
	private const double CellCountFontSize = 10;
	private const double DialogWidth = 580;
	private const double DialogHeight = 600;
	private const double DialogMinWidth = 400;
	private const double DialogMinHeight = 360;
	private const double DialogPadding = 16;
	private const double CloseButtonMinWidth = 90;

	private const string LocSelectionCount = "S.Import.SelectionCount";
	private const string LocToastExportedBundle = "S.Toast.ExportedBundle";
	private const string LocToastExportedArchive = "S.Toast.ExportedArchive";
	private const string LocToastExportedLinuxArchive = "S.Toast.ExportedLinuxArchive";
	private const string LocToastExportedXcursorTheme = "S.Toast.ExportedXcursorTheme";
	private const string LocExportBundle = "S.Export.AsBundle";
	private const string LocExportArchive = "S.Export.AsArchive";
	private const string LocExportAsLinuxArchive = "S.Export.AsLinuxArchive";
	private const string LocExportAsXcursorTheme = "S.Export.AsXcursorTheme";
	private const string LocDownloadReadme = "S.Export.DownloadReadme";
	private const string LocToastReadmeDownloaded = "S.Toast.ReadmeDownloaded";
	private const string LocExportSelectAll = "S.SelectAll";
	private const string LocExportSelectNone = "S.SelectNone";
	private const string LocExportName = "S.Export.Name";
	private const string LocExportTitle = "S.Export.Title";
	private const string LocExportClose = "S.Editor.Cancel";
	private const string LocGroupMembersCount = "S.Group.MembersCount";
	private const string LocErrorSaveFailed = "S.Error.SaveFailed";

	private readonly List<(Preset Preset, Border Cell)> _tiles = new();
	private readonly List<(PresetGroup Group, Border Cell)> _groupTiles = new();
	private TextBlock? _selectionCountText;
	private TextBox? _nameBox;
	private readonly Panel _toastHost;
	private const double GroupIndicatorSize = 10;

	public ExportWindow(IReadOnlyList<Preset> presets, IReadOnlyList<PresetGroup>? groups, Panel toastHost)
	{
		_toastHost = toastHost;

		Title = Loc.Get(LocExportTitle);
		Width = DialogWidth;
		Height = DialogHeight;
		MinWidth = DialogMinWidth;
		MinHeight = DialogMinHeight;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;

		var root = new Grid
		{
			RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto"),
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
		var presetToColorKey = new Dictionary<string, string>();

		foreach (var group in groups ?? Array.Empty<PresetGroup>())
		{
			_groupTiles.Add((group, CreateGroupTile(group, gallery)));

			foreach (var presetId in group.MemberPresetIds)
				presetToColorKey[presetId] = group.ColorKey;
		}

		foreach (var preset in presets)
			_tiles.Add((preset, CreateTile(preset, gallery, presetToColorKey)));

		if (presets.Count == 0)
		{
			var emptyHint = new TextBlock
			{
				Text = Loc.Get("S.Export.Empty"),
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

		var nameGrid = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("Auto,*"),
			Margin = new Thickness(DialogPadding, 4, DialogPadding, 0),
		};
		Grid.SetRow(nameGrid, 2);

		var nameLabel = new TextBlock
		{
			Text = Loc.Get(LocExportName),
			FontSize = 13,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0, 0, 8, 0),
		};
		Grid.SetColumn(nameLabel, 0);
		nameGrid.Children.Add(nameLabel);

		_nameBox = new TextBox
		{
			VerticalAlignment = VerticalAlignment.Center,
		};
		Grid.SetColumn(_nameBox, 1);
		nameGrid.Children.Add(_nameBox);

		root.Children.Add(nameGrid);

		var footerBorder = new Border
		{
			Padding = new Thickness(DialogPadding, 10),
			BorderThickness = new Thickness(0, 1, 0, 0),
		};
		ThemeManager.BindResource(footerBorder, Border.BackgroundProperty, "SystemControlDefaultChromeMediumBrush");
		ThemeManager.BindResource(footerBorder, Border.BorderBrushProperty, "SystemControlBorderBrush");

		var buttonPanel = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
		};

		var closeButton = new Button
		{
			Content = Loc.Get(LocExportClose),
			MinWidth = CloseButtonMinWidth,
		};
		closeButton.Click += (_, _) => Close();
		Grid.SetColumn(closeButton, 0);
		buttonPanel.Children.Add(closeButton);

		var rightButtons = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Spacing = 8,
		};
		Grid.SetColumn(rightButtons, 2);
		buttonPanel.Children.Add(rightButtons);

		var archiveButton = new Button
		{
			Content = Loc.Get(LocExportArchive),
			MinWidth = CloseButtonMinWidth,
		};
		archiveButton.Click += (_, _) => ExportArchive();
		rightButtons.Children.Add(archiveButton);

		var moreButton = new Button
		{
			Content = "⋮",
			Padding = new Thickness(8, 2),
		};
		moreButton.Click += (_, _) => ShowMoreMenu(moreButton);
		rightButtons.Children.Add(moreButton);

		var bundleButton = new Button
		{
			Content = Loc.Get(LocExportBundle),
			MinWidth = CloseButtonMinWidth,
			Classes = { "accent" },
		};
		bundleButton.Click += (_, _) => ExportBundle();
		rightButtons.Children.Add(bundleButton);

		footerBorder.Child = buttonPanel;
		Grid.SetRow(footerBorder, 3);
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

	private Border CreateTile(Preset preset, WrapPanel gallery, Dictionary<string, string> presetToColorKey)
	{
		var previewPath = PresetStore.GetRoleFilePath(preset, CursorRoles.ArrowRoleName)
							?? preset.Roles.Keys.Concat(preset.RoleRefs.Keys)
								.Select(role => PresetStore.GetRoleFilePath(preset, role))
								.FirstOrDefault(path => path != null);

		var preview = CursorPreviewService.GetPreview(previewPath);

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
			Text = preset.Name,
			FontSize = CellNameFontSize,
			TextTrimming = TextTrimming.CharacterEllipsis,
			TextAlignment = TextAlignment.Center,
			MaxWidth = CellSize - 12,
			Margin = new Thickness(4, 4, 4, 0),
		});

		var countText = new TextBlock
		{
			Text = $"{preset.Roles.Count + preset.RoleRefs.Count}/{CursorRoles.All.Length}",
			FontSize = CellCountFontSize,
			TextAlignment = TextAlignment.Center,
		};
		ThemeManager.BindResource(countText, TextBlock.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");
		panel.Children.Add(countText);

		if (preset.UseScaling)
		{
			panel.Children.Add(new Image
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
			Child = panel,
		};

		SetSelected(cell, true);

		cell.PointerPressed += (_, _) =>
		{
			SetSelected(cell, !IsSelected(cell));
			UpdateSelectionCount();
		};

		gallery.Children.Add(cell);
		return cell;
	}

	private static bool IsSelected(Border cell) => cell.Tag is true;

	private static void SetSelected(Border cell, bool selected)
	{
		cell.Tag = selected;
		ThemeManager.BindResource(cell, Border.BorderBrushProperty,
			selected ? "SystemAccentColor" : "SystemControlBorderBrush");
	}

	private Border CreateGroupTile(PresetGroup group, WrapPanel gallery)
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
			Text = Loc.Format(LocGroupMembersCount, group.MemberPresetIds.Count),
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

		cell.PointerPressed += (_, _) =>
		{
			var selecting = !IsSelected(cell);
			SetSelected(cell, selecting);

			foreach (var memberPresetId in group.MemberPresetIds)
			{
				var memberTile = _tiles.FirstOrDefault(t => t.Preset.Id == memberPresetId);
				if (memberTile.Cell != null)
					SetSelected(memberTile.Cell, selecting);
			}

			UpdateSelectionCount();
		};

		gallery.Children.Add(cell);
		return cell;
	}

	private void SyncGroupTileSelections()
	{
		foreach (var (group, cell) in _groupTiles)
		{
			var allMembersSelected = group.MemberPresetIds.Count > 0 && group.MemberPresetIds.All(memberId =>
				_tiles.FirstOrDefault(t => t.Preset.Id == memberId) is { Cell: not null } match &&
				IsSelected(match.Cell));

			SetSelected(cell, allMembersSelected);
		}
	}

	private List<Preset> GetSelectedPresets() =>
		_tiles.Where(tile => IsSelected(tile.Cell)).Select(tile => tile.Preset).ToList();

	private void UpdateSelectionCount()
	{
		SyncGroupTileSelections();

		if (_selectionCountText == null)
			return;

		var selected = _tiles.Count(tile => IsSelected(tile.Cell));
		_selectionCountText.Text = Loc.Format(LocSelectionCount, selected, _tiles.Count);
	}

	private void SelectAll()
	{
		foreach (var (_, cell) in _tiles)
			SetSelected(cell, true);

		UpdateSelectionCount();
	}

	private void SelectNone()
	{
		foreach (var (_, cell) in _tiles)
			SetSelected(cell, false);

		UpdateSelectionCount();
	}

	private void ExportBundle()
	{
		var selected = GetSelectedPresets();
		if (selected.Count == 0)
			return;

		try
		{
			var (path, count) = PresetPackageService.ExportBundle(selected, _nameBox?.Text);
			ToastService.Show(_toastHost, Loc.Format(LocToastExportedBundle, count, System.IO.Path.GetFileName(path)));

			if (AppState.GetOpenFolderAfterDownload())
				FileExplorerProvider.Current?.RevealFile(path);
		}
		catch (Exception ex)
		{
			ToastService.Show(_toastHost, Loc.Format(LocErrorSaveFailed, ex.Message));
		}
	}

	private void ExportArchive()
	{
		var selected = GetSelectedPresets();
		if (selected.Count == 0)
			return;

		try
		{
			var (path, count) = PresetPackageService.ExportArchive(selected, _nameBox?.Text);
			ToastService.Show(_toastHost, Loc.Format(LocToastExportedArchive, count, System.IO.Path.GetFileName(path)));

			if (AppState.GetOpenFolderAfterDownload())
				FileExplorerProvider.Current?.RevealFile(path);
		}
		catch (Exception ex)
		{
			ToastService.Show(_toastHost, Loc.Format(LocErrorSaveFailed, ex.Message));
		}
	}

	private void ShowMoreMenu(Button target)
	{
		var menu = new ContextMenu
		{
			PlacementTarget = target,
		};

		var xcursorItem = new MenuItem { Header = Loc.Get(LocExportAsXcursorTheme) };
		xcursorItem.Click += (_, _) => ExportXcursorTheme();
		menu.Items.Add(xcursorItem);

		var linuxItem = new MenuItem { Header = Loc.Get(LocExportAsLinuxArchive) };
		linuxItem.Click += (_, _) => ExportLinuxArchive();
		menu.Items.Add(linuxItem);

		var readmeItem = new MenuItem { Header = Loc.Get(LocDownloadReadme) };
		readmeItem.Click += (_, _) => DownloadReadme();
		menu.Items.Add(readmeItem);

		menu.Open(target);
	}

	private void DownloadReadme()
	{
		try
		{
			var path = PresetPackageService.DownloadReadme();
			ToastService.Show(_toastHost, Loc.Format(LocToastReadmeDownloaded, System.IO.Path.GetFileName(path)));

			if (AppState.GetOpenFolderAfterDownload())
				FileExplorerProvider.Current?.RevealFile(path);
		}
		catch (Exception ex)
		{
			ToastService.Show(_toastHost, Loc.Format(LocErrorSaveFailed, ex.Message));
		}
	}

	private void ExportLinuxArchive()
	{
		var selected = GetSelectedPresets();
		if (selected.Count == 0)
			return;

		try
		{
			var (path, count) = PresetPackageService.ExportLinuxArchive(selected, _nameBox?.Text);
			ToastService.Show(_toastHost, Loc.Format(LocToastExportedLinuxArchive, count, System.IO.Path.GetFileName(path)));

			if (AppState.GetOpenFolderAfterDownload())
				FileExplorerProvider.Current?.RevealFile(path);
		}
		catch (Exception ex)
		{
			ToastService.Show(_toastHost, Loc.Format(LocErrorSaveFailed, ex.Message));
		}
	}

	private void ExportXcursorTheme()
	{
		var selected = GetSelectedPresets();
		if (selected.Count == 0)
			return;

		try
		{
			var (path, count) = PresetPackageService.ExportXcursorTheme(selected, _nameBox?.Text);
			ToastService.Show(_toastHost, Loc.Format(LocToastExportedXcursorTheme, count, System.IO.Path.GetFileName(path)));

			if (AppState.GetOpenFolderAfterDownload())
				FileExplorerProvider.Current?.RevealFile(path);
		}
		catch (Exception ex)
		{
			ToastService.Show(_toastHost, Loc.Format(LocErrorSaveFailed, ex.Message));
		}
	}
}
