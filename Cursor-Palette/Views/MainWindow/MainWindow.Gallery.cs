using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CursorPalette.Models;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class MainWindow
{
	private void ReloadGallery()
	{
		_presets = PresetStore.LoadAll();
		_groups = GroupStore.LoadAll();
		_presetToGroup = _groups
			.SelectMany(group => group.MemberPresetIds.Select(presetId => (presetId, group)))
			.GroupBy(entry => entry.presetId)
			.ToDictionary(entry => entry.Key, entry => entry.First().group);

		_boardOrderIds = ReconcileBoardOrder(BoardOrderStore.Load(), _presets, _groups, _presetToGroup);
		BoardOrderStore.Save(_boardOrderIds);
		_visibleBoardIds = _boardOrderIds.Where(IsBoardIdVisible).ToList();

		ClearGroupSelection();

		Gallery.Items.Clear();
		_activeCellSizeText = null;

		if (_activePresetId != null && _presets.All(preset => preset.Id != _activePresetId))
		{
			_activePresetId = null;
			AppState.SetActivePresetId(null);
		}

		Gallery.Items.Add(CreateDefaultCell());

		_boardOrder.Clear();
		var presetsById = _presets.ToDictionary(preset => preset.Id);
		var groupsById = _groups.ToDictionary(group => group.Id);

		for (var boardIndex = 0; boardIndex < _boardOrderIds.Count; boardIndex++)
		{
			var id = _boardOrderIds[boardIndex];

			if (groupsById.TryGetValue(id, out var group))
			{
				Gallery.Items.Add(CreateGroupCell(group));
				_boardOrder.Add(new BoardEntry(null, group, boardIndex));
				continue;
			}

			if (!presetsById.TryGetValue(id, out var preset))
				continue;

			if (_presetToGroup.TryGetValue(preset.Id, out var owningGroup) && owningGroup.Collapsed)
				continue;

			Gallery.Items.Add(CreatePresetCell(preset, _presetToGroup.GetValueOrDefault(preset.Id)));
			_boardOrder.Add(new BoardEntry(preset, null, boardIndex));
		}

		Gallery.Items.Add(CreateAddCell());
		EmptyHint.Visibility = _presets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
	}

	private bool IsBoardIdVisible(string id)
	{
		if (_presetToGroup.TryGetValue(id, out var group))
			return !group.Collapsed;

		return true;
	}

	private static List<string> ReconcileBoardOrder(List<string> persisted, List<Preset> presets,
		List<PresetGroup> groups, Dictionary<string, PresetGroup> presetToGroup)
	{
		var validIds = new HashSet<string>(presets.Select(preset => preset.Id));
		validIds.UnionWith(groups.Select(group => group.Id));

		var result = persisted.Where(validIds.Contains).ToList();
		var known = new HashSet<string>(result);
		var placedGroups = new HashSet<string>();

		foreach (var preset in presets)
		{
			if (presetToGroup.TryGetValue(preset.Id, out var group) && placedGroups.Add(group.Id) && known.Add(group.Id))
				result.Add(group.Id);

			if (known.Add(preset.Id))
				result.Add(preset.Id);
		}

		foreach (var group in groups)
		{
			if (known.Add(group.Id))
				result.Add(group.Id);
		}

		return result;
	}

	private FrameworkElement CreateDefaultCell()
	{
		var isActive = _activePresetId == null;

		var defaults = RegistryCursorService.GetWindowsDefaultValues();
		var previewPath = defaults.TryGetValue(CursorRoles.ArrowRoleName, out var arrow) ? arrow : null;

		var image = new Image
		{
			Width = CellPreviewSize * _cellScale,
			Height = CellPreviewSize * _cellScale,
			SnapsToDevicePixels = true,
		};
		RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
		CursorPreviewService.ApplyPreview(image, previewPath);

		var nameText = new TextBlock
		{
			Text = Loc.Get(LocWindowsDefault),
			FontSize = CellNameFontSize * CellFontScale,
			TextTrimming = TextTrimming.CharacterEllipsis,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(4, 8, 4, 0),
		};

		var sizeText = new TextBlock
		{
			Text = $"{AppState.GetDefaultBaseSize()} {PixelSuffix}",
			Foreground = Brush(BrushTextDim),
			FontSize = CellSizeFontSize * CellFontScale,
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
			Width = CellSize * _cellScale,
			Height = CellSize * _cellScale,
			Margin = new Thickness(CellMargin),
			CornerRadius = new CornerRadius(CellCornerRadius),
			Background = Brush(BrushBg),
			BorderThickness = new Thickness(CellBorderThickness),
			BorderBrush = isActive ? Brush(BrushAccent) : Brush(BrushBorder),
			Child = panel,
			Cursor = Cursors.Hand,
		};

		cell.MouseEnter += (_, _) =>
		{
			if (_activePresetId != null) cell.Background = Brush(BrushSurfaceHover);
		};
		cell.MouseLeave += (_, _) => cell.Background = Brush(BrushBg);
		cell.MouseLeftButtonUp += (_, _) => ApplyDefault();
		cell.MouseLeftButtonDown += (_, _) => { };
		cell.MouseRightButtonUp += (_, e) =>
		{
			cell.ContextMenu!.IsOpen = true;
			e.Handled = true;
		};

		var menu = new ContextMenu();
		var downloadSystemItem = new MenuItem { Header = Loc.Get(LocMenuDownloadSystemCursors) };
		var pngGifItem = new MenuItem { Header = Loc.Get(LocMenuDownloadSystemPngGif) };
		pngGifItem.Click += (_, _) => DownloadSystemCursors(asImages: true);
		var curAniItem = new MenuItem { Header = Loc.Get(LocMenuDownloadSystemCurAni) };
		curAniItem.Click += (_, _) => DownloadSystemCursors(asImages: false);
		downloadSystemItem.Items.Add(pngGifItem);
		downloadSystemItem.Items.Add(curAniItem);
		menu.Items.Add(downloadSystemItem);
		cell.ContextMenu = menu;

		cell.ToolTip = new ToolTip { Content = Loc.Get(LocWindowsDefault) };

		return cell;
	}

	private FrameworkElement CreatePresetCell(Preset preset, PresetGroup? group)
	{
		var isActive = preset.Id == _activePresetId;
		var isSelected = _selectedPresetIds.Contains(preset.Id);

		var previewPath = PresetStore.GetRoleFilePath(preset, CursorRoles.ArrowRoleName)
							?? preset.Roles.Keys.Concat(preset.RoleRefs.Keys)
								.Select(role => PresetStore.GetRoleFilePath(preset, role))
								.FirstOrDefault(path => path != null);

		var image = new Image
		{
			Width = CellPreviewSize * _cellScale,
			Height = CellPreviewSize * _cellScale,
			SnapsToDevicePixels = true,
		};
		RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
		CursorPreviewService.ApplyPreview(image, previewPath);

		var nameText = new TextBlock
		{
			Text = preset.Name,
			FontSize = CellNameFontSize * CellFontScale,
			TextTrimming = TextTrimming.CharacterEllipsis,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(4, 8, 4, 0),
		};

		var countText = new TextBlock
		{
			Text = $"{preset.Roles.Count + preset.RoleRefs.Count}/{CursorRoles.All.Length}",
			Foreground = Brush(BrushTextDim),
			FontSize = CellCountFontSize * CellFontScale,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(0, 2, 0, 0),
		};

		var sizeText = new TextBlock
		{
			Text = $"{preset.BaseSize} {PixelSuffix}",
			Foreground = Brush(BrushTextDim),
			FontSize = CellSizeFontSize * CellFontScale,
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

		var cellContent = new Grid();
		cellContent.Children.Add(panel);

		var isMixed = preset.RoleRefs.Count > 0;
		if (isMixed)
		{
			cellContent.Children.Add(new TextBlock
			{
				Text = MixedBadgeText,
				FontSize = MixedBadgeFontSize * CellFontScale,
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Top,
				Margin = new Thickness(0, 4, 6, 0),
				IsHitTestVisible = false,
			});
		}

		var selectionBadge = new Border
		{
			Width = SelectionBadgeSize * CellFontScale,
			Height = SelectionBadgeSize * CellFontScale,
			CornerRadius = new CornerRadius(SelectionBadgeSize),
			Background = Brush(BrushAccent),
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(6, 4, 0, 0),
			IsHitTestVisible = false,
			Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed,
			Child = new TextBlock
			{
				Text = SelectionBadgeText,
				FontSize = SelectionBadgeFontSize * CellFontScale,
				Foreground = System.Windows.Media.Brushes.White,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
			},
		};
		cellContent.Children.Add(selectionBadge);

		if (preset.UseScaling)
		{
			var stairIcon = new Image
			{
				Width = 20 * CellFontScale,
				Height = 20 * CellFontScale,
				SnapsToDevicePixels = true,
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Bottom,
				Margin = new Thickness(0, 0, 8, 8),
				IsHitTestVisible = false,
				Source = new System.Windows.Media.Imaging.BitmapImage(
					new Uri(ExpandIconUri)),
			};
			RenderOptions.SetBitmapScalingMode(stairIcon, BitmapScalingMode.NearestNeighbor);
			cellContent.Children.Add(stairIcon);
		}

		var cell = new Border
		{
			Width = CellSize * _cellScale,
			Height = CellSize * _cellScale,
			Margin = new Thickness(CellMargin),
			CornerRadius = new CornerRadius(CellCornerRadius),
			Background = Brush(BrushSurface),
			BorderThickness = new Thickness(isSelected ? SelectionBorderThickness : CellBorderThickness),
			BorderBrush = isSelected ? Brush(BrushAccent) : (isActive ? Brush(BrushAccent) : Brush(BrushBorder)),
			Child = cellContent,
			Cursor = Cursors.Hand,
			Tag = preset,
		};

		FrameworkElement result = cell;
		if (group != null)
		{
			var groupBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(GroupColors.ResolveHex(group.ColorKey))!);
			var ringSize = CellSize * _cellScale + GroupOutlinePadding * 2;

			var outlineRect = new System.Windows.Shapes.Rectangle
			{
				Width = ringSize,
				Height = ringSize,
				RadiusX = CellCornerRadius + GroupOutlinePadding,
				RadiusY = CellCornerRadius + GroupOutlinePadding,
				Stroke = groupBrush,
				StrokeThickness = GroupOutlineThickness,
				StrokeDashArray = new DoubleCollection { 4, 3 },
				Opacity = GroupOutlineOpacity,
				IsHitTestVisible = false,
			};

			cell.Margin = new Thickness(GroupOutlinePadding);

			var wrapper = new Grid { Margin = new Thickness(CellMargin) };
			wrapper.Children.Add(cell);
			wrapper.Children.Add(outlineRect);
			result = wrapper;
		}

		cell.MouseEnter += (_, _) =>
		{
			if (preset.Id != _activePresetId) cell.Background = Brush(BrushSurfaceHover);
		};
		cell.MouseLeave += (_, _) => cell.Background = Brush(BrushSurface);
		cell.MouseLeftButtonDown += (_, e) =>
		{
			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
				return;

			_presetDragStartPoint = e.GetPosition(cell);
		};
		cell.MouseMove += (_, e) =>
		{
			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
				return;

			if (_presetDragStartPoint is not { } start || e.LeftButton != MouseButtonState.Pressed)
				return;

			var position = e.GetPosition(cell);
			if (Math.Abs(position.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
				Math.Abs(position.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance)
				return;

			_presetDragStartPoint = null;
			_justDraggedPreset = true;
			BeginDragGhost(preset, previewPath);
			DragDrop.DoDragDrop(cell, new DataObject(PresetDragFormatName, preset.Id), DragDropEffects.Move);
			EndDragGhost();
		};
		cell.MouseLeftButtonUp += (_, _) =>
		{
			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
			{
				_presetDragStartPoint = null;
				ToggleSelection(preset, cell, selectionBadge);
				return;
			}

			_presetDragStartPoint = null;

			if (_justDraggedPreset)
			{
				_justDraggedPreset = false;
				return;
			}

			ApplyPreset(preset);
		};
		cell.MouseRightButtonUp += (_, e) =>
		{
			cell.ContextMenu!.IsOpen = true;
			e.Handled = true;
		};

		var visibleIndex = _visibleBoardIds.IndexOf(preset.Id);
		var isFirst = visibleIndex <= 0;
		var isLast = visibleIndex < 0 || visibleIndex >= _visibleBoardIds.Count - 1;

		var menu = new ContextMenu();
		var editItem = new MenuItem { Header = Loc.Get(LocMenuEdit) };
		editItem.Click += (_, _) => EditPreset(preset);
		var renameItem = new MenuItem { Header = Loc.Get(LocMenuRename) };
		renameItem.Click += (_, _) => StartInlineRename(preset, nameText, panel);
		var moveLeftItem = new MenuItem
		{
			Header = Loc.Get(LocMenuMoveLeft),
			Visibility = isFirst ? Visibility.Collapsed : Visibility.Visible,
		};
		moveLeftItem.Click += (_, _) => MovePreset(preset, -1);
		var moveRightItem = new MenuItem
		{
			Header = Loc.Get(LocMenuMoveRight),
			Visibility = isLast ? Visibility.Collapsed : Visibility.Visible,
		};
		moveRightItem.Click += (_, _) => MovePreset(preset, 1);
		var downloadItem = new MenuItem { Header = Loc.Get(LocMenuDownload) };
		downloadItem.Click += (_, _) => DownloadPreset(preset);
		var deleteItem = new MenuItem { Header = Loc.Get(LocMenuDelete) };
		deleteItem.Click += (_, _) => DeletePreset(preset);
		menu.Items.Add(editItem);
		menu.Items.Add(renameItem);
		menu.Items.Add(moveLeftItem);
		menu.Items.Add(moveRightItem);
		menu.Items.Add(downloadItem);

		var useScalingItem = new MenuItem
		{
			Header = Loc.Get(LocMenuUseScaling),
			IsCheckable = true,
			IsChecked = preset.UseScaling,
		};
		useScalingItem.Click += (_, _) =>
		{
			var newValue = !preset.UseScaling;
			PresetStore.UpdateUseScaling(preset.Id, newValue);
			preset.UseScaling = newValue;
			ReloadGallery();
		};
		menu.Items.Add(useScalingItem);

		var assignableGroups = _groups.Where(candidate => group == null || candidate.Id != group.Id).ToList();
		if (assignableGroups.Count > 0)
		{
			var assignToGroupItem = new MenuItem { Header = Loc.Get(LocMenuAssignToGroup) };

			foreach (var targetGroup in assignableGroups)
			{
				var targetGroupItem = new MenuItem
				{
					Header = targetGroup.Name,
					Icon = new Border
					{
						Width = 10,
						Height = 10,
						CornerRadius = new CornerRadius(10),
						Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(GroupColors.ResolveHex(targetGroup.ColorKey))!),
					},
				};
				targetGroupItem.Click += (_, _) =>
				{
					if (_presetToGroup.TryGetValue(preset.Id, out var currentGroup))
						GroupStore.RemoveMember(currentGroup.Id, preset.Id);

					GroupStore.AddMember(targetGroup.Id, preset.Id);
					ReloadGallery();
				};
				assignToGroupItem.Items.Add(targetGroupItem);
			}

			menu.Items.Add(assignToGroupItem);
		}

		if (group != null)
		{
			var removeFromGroupItem = new MenuItem { Header = Loc.Get(LocMenuRemoveFromGroup) };
			removeFromGroupItem.Click += (_, _) =>
			{
				GroupStore.RemoveMember(group.Id, preset.Id);
				ReloadGallery();
			};
			menu.Items.Add(removeFromGroupItem);
		}

		menu.Items.Add(new Separator());
		menu.Items.Add(deleteItem);
		cell.ContextMenu = menu;

		var hintPanel = new StackPanel();
		hintPanel.Children.Add(new TextBlock { Text = preset.Name, FontWeight = FontWeights.SemiBold });
		if (isMixed)
		{
			hintPanel.Children.Add(new TextBlock
			{
				Text = Loc.Get(LocMixedBadgeTooltip),
				FontSize = 11,
				Foreground = Brush(BrushTextDim),
				Margin = new Thickness(0, 2, 0, 0),
			});
		}
		hintPanel.Children.Add(new TextBlock
		{
			Text = Loc.Get(LocPresetContextHint),
			FontSize = 11,
			Foreground = Brush(BrushTextDim),
			Margin = new Thickness(0, 2, 0, 0),
		});
		cell.ToolTip = new ToolTip { Content = hintPanel };

		cell.InputBindings.Add(new MouseBinding(
			new RelayUiCommand(() => EditPreset(preset)),
			new MouseGesture(MouseAction.LeftDoubleClick)));

		return result;
	}

	private FrameworkElement CreateGroupCell(PresetGroup group)
	{
		var colorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(GroupColors.ResolveHex(group.ColorKey))!);

		var nameText = new TextBlock
		{
			Text = group.Name,
			FontSize = CellNameFontSize * CellFontScale,
			FontWeight = FontWeights.SemiBold,
			Foreground = System.Windows.Media.Brushes.White,
			TextTrimming = TextTrimming.CharacterEllipsis,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(4, 8, 4, 0),
		};

		var countText = new TextBlock
		{
			Text = Loc.Format(LocGroupMembersCount, group.MemberPresetIds.Count),
			Foreground = System.Windows.Media.Brushes.White,
			Opacity = 0.85,
			FontSize = CellCountFontSize * CellFontScale,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(0, 2, 0, 0),
		};

		var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
		panel.Children.Add(nameText);
		panel.Children.Add(countText);

		var tile = new Border
		{
			Width = CellSize * _cellScale,
			Height = CellSize * _cellScale,
			CornerRadius = new CornerRadius(CellCornerRadius),
			Background = colorBrush,
			BorderThickness = new Thickness(0),
			SnapsToDevicePixels = true,
			Child = panel,
			Cursor = Cursors.Hand,
		};
		Panel.SetZIndex(tile, GroupDeckMaxPeek + 1);

		tile.MouseLeftButtonDown += (_, e) =>
		{
			if (!group.Collapsed || Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
				return;

			_presetDragStartPoint = e.GetPosition(tile);
		};
		tile.MouseMove += (_, e) =>
		{
			if (!group.Collapsed || Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
				return;

			if (_presetDragStartPoint is not { } start || e.LeftButton != MouseButtonState.Pressed)
				return;

			var position = e.GetPosition(tile);
			if (Math.Abs(position.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
				Math.Abs(position.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance)
				return;

			_presetDragStartPoint = null;
			_justDraggedGroup = true;
			BeginGroupDragGhost(group);
			DragDrop.DoDragDrop(tile, new DataObject(GroupDragFormatName, group.Id), DragDropEffects.Move);
			EndDragGhost();
		};
		tile.MouseLeftButtonUp += (_, _) =>
		{
			_presetDragStartPoint = null;

			if (_justDraggedGroup)
			{
				_justDraggedGroup = false;
				return;
			}

			GroupStore.SetCollapsed(group.Id, !group.Collapsed);
			ReloadGallery();
		};

		var menu = new ContextMenu();
		var randomItem = new MenuItem { Header = Loc.Get(LocMenuRandomPreset) };
		randomItem.Click += (_, _) => ApplyRandomFromGroup(group);
		var editItem = new MenuItem { Header = Loc.Get(LocMenuEditGroup) };
		editItem.Click += (_, _) => EditGroup(group);
		var consolidateItem = new MenuItem { Header = Loc.Get(LocMenuConsolidateGroup) };
		consolidateItem.Click += (_, _) => ConsolidateGroup(group.Id);
		var ungroupItem = new MenuItem { Header = Loc.Get(LocMenuUngroup) };
		ungroupItem.Click += (_, _) =>
		{
			foreach (var presetId in group.MemberPresetIds.ToList())
				GroupStore.RemoveMember(group.Id, presetId);

			ReloadGallery();
			ToastService.Show(RootGrid, Loc.Format(LocGroupToastUngrouped, group.Name));
		};
		menu.Items.Add(randomItem);
		menu.Items.Add(new Separator());
		menu.Items.Add(editItem);
		menu.Items.Add(consolidateItem);
		menu.Items.Add(ungroupItem);
		menu.Items.Add(new Separator());
		var deleteGroupItem = new MenuItem { Header = Loc.Get(LocMenuDeleteGroup) };
		deleteGroupItem.Click += (_, _) => DeleteGroup(group);
		menu.Items.Add(deleteGroupItem);
		tile.ContextMenu = menu;

		tile.MouseRightButtonUp += (_, e) =>
		{
			tile.ContextMenu!.IsOpen = true;
			e.Handled = true;
		};

		tile.ToolTip = new ToolTip
		{
			Content = Loc.Get(group.Collapsed ? LocGroupExpandedTooltip : LocGroupCollapsedTooltip),
		};

		if (!group.Collapsed)
		{
			var wrapper = new Border { Margin = new Thickness(CellMargin), Child = tile };
			tile.Margin = new Thickness(0);
			return wrapper;
		}

		var deckGrid = new Grid
		{
			Margin = new Thickness(CellMargin),
			Width = CellSize * _cellScale + GroupDeckMaxPeek * GroupDeckPeekOffsetX * _cellScale,
			Height = CellSize * _cellScale + GroupDeckMaxPeek * GroupDeckPeekOffsetY * _cellScale,
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Top,
		};

		var peekCount = Math.Min(GroupDeckMaxPeek, group.MemberPresetIds.Count);
		for (var i = peekCount; i >= 1; i--)
		{
			var ghost = new Border
			{
				Width = CellSize * _cellScale,
				Height = CellSize * _cellScale,
				CornerRadius = new CornerRadius(CellCornerRadius),
				Background = Brush(BrushSurface),
				BorderThickness = new Thickness(CellBorderThickness),
				BorderBrush = Brush(BrushBorder),
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Top,
				Margin = new Thickness(i * GroupDeckPeekOffsetX * _cellScale, i * GroupDeckPeekOffsetY * _cellScale, 0, 0),
			};
			Panel.SetZIndex(ghost, GroupDeckMaxPeek + 1 - i);
			deckGrid.Children.Add(ghost);
		}

		tile.Margin = new Thickness(0);
		tile.HorizontalAlignment = HorizontalAlignment.Left;
		tile.VerticalAlignment = VerticalAlignment.Top;
		deckGrid.Children.Add(tile);

		return deckGrid;
	}

	private FrameworkElement CreateAddCell()
	{
		var plus = new TextBlock
		{
			Text = AddCellPlusText,
			FontSize = AddCellPlusFontSize * CellFontScale,
			Foreground = Brush(BrushTextDim),
			TextAlignment = TextAlignment.Center,
		};
		var label = new TextBlock
		{
			Text = Loc.Get(LocAddPreset),
			FontSize = CellNameFontSize * CellFontScale,
			Foreground = Brush(BrushTextDim),
			TextAlignment = TextAlignment.Center,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(8, 4, 8, 0),
		};
		var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
		panel.Children.Add(plus);
		panel.Children.Add(label);

		var cell = new Border
		{
			Width = CellSize * _cellScale,
			Height = CellSize * _cellScale,
			Margin = new Thickness(CellMargin),
			CornerRadius = new CornerRadius(CellCornerRadius),
			Background = System.Windows.Media.Brushes.Transparent,
			BorderThickness = new Thickness(CellBorderThickness),
			BorderBrush = Brush(BrushBorder),
			Child = panel,
			Cursor = Cursors.Hand,
			ToolTip = new ToolTip { Content = Loc.Get(LocAddPresetHint) },
			AllowDrop = true,
		};

		cell.MouseEnter += (_, _) => cell.BorderBrush = Brush(BrushAccent);
		cell.MouseLeave += (_, _) => cell.BorderBrush = Brush(BrushBorder);
		cell.MouseLeftButtonUp += (_, _) => OpenEditor(null, Array.Empty<string>());
		cell.Drop += OnWindowDrop;

		return cell;
	}
}
