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
	private const double DialogWidth = 640;
	private const double DialogHeight = 560;
	private const double DialogMinWidth = 400;
	private const double DialogMinHeight = 360;
	private const double DialogPadding = 16;
	private const double CloseButtonMinWidth = 90;

	private const string LocSelectionCount = "S.Import.SelectionCount";
	private const string LocToastExportedBundle = "S.Toast.ExportedBundle";
	private const string LocToastExportedArchive = "S.Toast.ExportedArchive";
	private const string LocToastExportedLinuxArchive = "S.Toast.ExportedLinuxArchive";
	private const string LocToastExportedXcursorTheme = "S.Toast.ExportedXcursorTheme";
	private const string LocExportBundle = "S.Export.Bundle";
	private const string LocExportArchive = "S.Export.Archive";
	private const string LocExportAsLinuxArchive = "S.Export.AsLinuxArchive";
	private const string LocExportAsXcursorTheme = "S.Export.AsXcursorTheme";
	private const string LocExportSelectAll = "S.Export.SelectAll";
	private const string LocExportSelectNone = "S.Export.SelectNone";
	private const string LocExportName = "S.Export.Name";
	private const string LocExportTitle = "S.Export.Title";
	private const string LocExportClose = "S.Export.Close";
	private const string LocGroupMembersCount = "S.Group.MembersCount";

	private readonly List<(Preset Preset, Border Cell)> _tiles = new();
	private TextBlock? _selectionCountText;
	private TextBox? _nameBox;
	private readonly Panel _toastHost;

	public ExportWindow(IReadOnlyList<Preset> presets, IReadOnlyList<PresetGroup>? groups, Panel toastHost)
	{
		_toastHost = toastHost;

		Title = Loc.Get(LocExportTitle);
		Width = DialogWidth;
		Height = DialogHeight;
		MinWidth = DialogMinWidth;
		MinHeight = DialogMinHeight;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;

		var root = new StackPanel
		{
			Margin = new Thickness(DialogPadding),
			Spacing = 8,
		};

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

		root.Children.Add(headerGrid);

		var scrollViewer = new ScrollViewer
		{
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
		};

		var gallery = new WrapPanel();
		var presetToColorKey = new Dictionary<string, string>();

		foreach (var group in groups ?? Array.Empty<PresetGroup>())
		{
			foreach (var presetId in group.MemberPresetIds)
				presetToColorKey[presetId] = group.ColorKey;
		}

		foreach (var preset in presets)
			_tiles.Add((preset, CreateTile(preset, gallery, presetToColorKey)));

		scrollViewer.Content = gallery;
		root.Children.Add(scrollViewer);

		var nameGrid = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("Auto,*"),
			Margin = new Thickness(0, 4, 0, 0),
		};

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

		var buttonPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Spacing = 8,
			Margin = new Thickness(0, 8, 0, 0),
		};

		var bundleButton = new Button
		{
			Content = Loc.Get(LocExportBundle),
			MinWidth = CloseButtonMinWidth,
		};
		bundleButton.Click += (_, _) => ExportBundle();
		buttonPanel.Children.Add(bundleButton);

		var archiveButton = new Button
		{
			Content = Loc.Get(LocExportArchive),
			MinWidth = CloseButtonMinWidth,
		};
		archiveButton.Click += (_, _) => ExportArchive();
		buttonPanel.Children.Add(archiveButton);

		var moreButton = new Button
		{
			Content = "⋮",
			Padding = new Thickness(8, 2),
		};
		moreButton.Click += (_, _) => ShowMoreMenu(moreButton);
		buttonPanel.Children.Add(moreButton);

		var spacer = new Border();
		buttonPanel.Children.Add(spacer);

		var closeButton = new Button
		{
			Content = Loc.Get(LocExportClose),
			MinWidth = CloseButtonMinWidth,
			HorizontalAlignment = HorizontalAlignment.Right,
		};
		closeButton.Click += (_, _) => Close();
		buttonPanel.Children.Add(closeButton);

		root.Children.Add(buttonPanel);

		Content = root;

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

		panel.Children.Add(new TextBlock
		{
			Text = $"{preset.Roles.Count + preset.RoleRefs.Count}/{CursorRoles.All.Length}",
			FontSize = CellCountFontSize,
			Foreground = Brushes.Gray,
			TextAlignment = TextAlignment.Center,
		});

		if (preset.UseScaling)
		{
			panel.Children.Add(new TextBlock
			{
				Text = "📐",
				FontSize = 10,
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
			BorderBrush = Brushes.CornflowerBlue,
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
		cell.BorderBrush = selected ? Brushes.CornflowerBlue : Brushes.Gray;
	}

	private List<Preset> GetSelectedPresets() =>
		_tiles.Where(tile => IsSelected(tile.Cell)).Select(tile => tile.Preset).ToList();

	private void UpdateSelectionCount()
	{
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
		}
		catch { }
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
		}
		catch { }
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

		menu.Open(target);
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
		}
		catch { }
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
		}
		catch { }
	}
}
