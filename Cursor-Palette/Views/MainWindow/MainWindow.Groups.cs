using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CursorPalette.Models;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class MainWindow
{
	private void BuildGroupColorSwatches()
	{
		GroupColorSwatches.Children.Clear();
		_groupColorSwatches.Clear();

		foreach (var (key, hex) in GroupColors.Palette)
		{
			var colorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);

			var swatch = new Border
			{
				Width = GroupSwatchSize,
				Height = GroupSwatchSize,
				CornerRadius = new CornerRadius(GroupSwatchSize),
				Background = colorBrush,
				BorderBrush = Brush(BrushText),
				BorderThickness = new Thickness(0),
				Margin = new Thickness(4, 0, 4, 0),
				Cursor = Cursors.Hand,
			};

			swatch.MouseLeftButtonUp += (_, _) =>
			{
				_pendingGroupColorKey = key;

				foreach (var other in _groupColorSwatches)
					other.BorderThickness = new Thickness(0);

				swatch.BorderThickness = new Thickness(GroupSwatchRingThickness);
			};

			GroupColorSwatches.Children.Add(swatch);
			_groupColorSwatches.Add(swatch);
		}
	}

	private void EditGroup(PresetGroup group)
	{
		var dialog = new GroupEditWindow(group) { Owner = this };
		if (dialog.ShowDialog() != true)
			return;

		var newName = dialog.GroupName;
		if (string.IsNullOrWhiteSpace(newName))
			newName = Loc.Get(LocGroupDefaultName);

		group.Name = newName;
		group.ColorKey = dialog.ColorKey;

		GroupStore.Save(group);

		ReloadGallery();
	}

	private void DeleteGroup(PresetGroup group)
	{
		if (group.MemberPresetIds.Count > 0)
		{
			var answer = MessageBox.Show(
				Loc.Format(LocConfirmDeleteText, group.Name),
				Loc.Get(LocConfirmDeleteTitle),
				MessageBoxButton.YesNo, MessageBoxImage.Question);

			if (answer != MessageBoxResult.Yes)
				return;
		}

		var memberIds = group.MemberPresetIds.ToList();

		foreach (var presetId in memberIds)
		{
			PresetStore.Delete(presetId);

			if (_activePresetId == presetId)
			{
				_activePresetId = null;
				AppState.SetActivePresetId(null);
			}
		}

		GroupStore.Delete(group.Id);
		ReloadGallery();
		ToastService.Show(RootGrid, Loc.Format(LocGroupToastDeleted, group.Name));
	}

	private void CreateEmptyGroup()
	{
		var dialog = new GroupEditWindow() { Owner = this };
		if (dialog.ShowDialog() != true)
			return;

		var name = dialog.GroupName;
		if (string.IsNullOrWhiteSpace(name))
			name = Loc.Get(LocGroupDefaultName);

		var group = new PresetGroup
		{
			Id = Guid.NewGuid().ToString("N"),
			Name = name,
			ColorKey = dialog.ColorKey,
			Collapsed = false,
			MemberPresetIds = new List<string>(),
		};

		GroupStore.Save(group);

		if (!_boardOrderIds.Contains(group.Id))
			_boardOrderIds.Add(group.Id);

		PersistBoardOrder();
		ToastService.Show(RootGrid, Loc.Format(LocGroupToastCreated, group.Name));
	}

	private void ClearGroupSelection()
	{
		_selectedPresetIds.Clear();
		_pendingGroupColorKey = null;

		if (GroupNameBox != null)
			GroupNameBox.Text = Loc.Get(LocGroupDefaultName);

		foreach (var swatch in _groupColorSwatches)
			swatch.BorderThickness = new Thickness(0);

		if (GroupToolbar != null)
			GroupToolbar.Visibility = Visibility.Collapsed;
	}

	private void ToggleSelection(Preset preset, Border cell, Border selectionBadge)
	{
		var nowSelected = !_selectedPresetIds.Contains(preset.Id);

		if (nowSelected)
			_selectedPresetIds.Add(preset.Id);
		else
			_selectedPresetIds.Remove(preset.Id);

		selectionBadge.Visibility = nowSelected ? Visibility.Visible : Visibility.Collapsed;
		cell.BorderBrush = nowSelected || preset.Id == _activePresetId ? Brush(BrushAccent) : Brush(BrushBorder);

		UpdateGroupToolbar();
	}

	private void UpdateGroupToolbar()
	{
		if (_selectedPresetIds.Count == 0)
		{
			GroupToolbar.Visibility = Visibility.Collapsed;
			return;
		}

		GroupToolbar.Visibility = Visibility.Visible;
		GroupSelectionCountText.Text = Loc.Format(LocGroupSelectedCount, _selectedPresetIds.Count);
	}

	private void OnGroupCreateClick(object sender, RoutedEventArgs e)
	{
		if (_selectedPresetIds.Count == 0 || _pendingGroupColorKey == null)
			return;

		var name = GroupNameBox.Text.Trim();
		if (name.Length == 0)
			name = Loc.Get(LocGroupDefaultName);

		foreach (var presetId in _selectedPresetIds)
		{
			if (_presetToGroup.TryGetValue(presetId, out var oldGroup))
				GroupStore.RemoveMember(oldGroup.Id, presetId);
		}

		var group = new PresetGroup
		{
			Id = Guid.NewGuid().ToString("N"),
			Name = name,
			ColorKey = _pendingGroupColorKey,
			Collapsed = false,
			MemberPresetIds = _selectedPresetIds.ToList(),
		};

		GroupStore.Save(group);
		ReloadGallery();
		ToastService.Show(RootGrid, Loc.Format(LocGroupToastCreated, group.Name));
	}

	private void OnGroupCancelClick(object sender, RoutedEventArgs e) => ReloadGallery();

	private void OnGalleryRightClick(object sender, MouseButtonEventArgs e)
	{
		var menu = new ContextMenu();
		var createGroupItem = new MenuItem { Header = Loc.Get(LocMenuCreateGroup) };
		createGroupItem.Click += (_, _) => CreateEmptyGroup();
		menu.Items.Add(createGroupItem);

		menu.PlacementTarget = GalleryScroll;
		menu.Placement = PlacementMode.MousePoint;
		menu.IsOpen = true;
		e.Handled = true;
	}

	private void StartInlineGroupRename(PresetGroup group, TextBlock nameText, StackPanel panel)
	{
		var index = panel.Children.IndexOf(nameText);
		if (index < 0)
			return;

		var done = false;

		var textBox = new TextBox
		{
			Text = group.Name,
			FontSize = nameText.FontSize,
			FontWeight = FontWeights.SemiBold,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(nameText.Margin.Left, nameText.Margin.Top - 2, nameText.Margin.Right, nameText.Margin.Bottom),
			Style = (Style)Application.Current.Resources[StyleTextBox],
			Background = Brush(BrushBg),
			BorderBrush = System.Windows.Media.Brushes.White,
			BorderThickness = new Thickness(1.5),
			Padding = new Thickness(6, 4, 6, 4),
		};

		void Restore()
		{
			var currentIndex = panel.Children.IndexOf(textBox);
			if (currentIndex < 0)
				return;

			panel.Children.RemoveAt(currentIndex);
			panel.Children.Insert(currentIndex, nameText);
		}

		void Commit()
		{
			if (done)
				return;
			done = true;

			var newName = textBox.Text.Trim();
			Restore();

			if (!string.IsNullOrWhiteSpace(newName) && newName != group.Name)
			{
				GroupStore.Rename(group.Id, newName);
				ReloadGallery();
			}
		}

		void Cancel()
		{
			if (done)
				return;
			done = true;
			Restore();
		}

		textBox.PreviewMouseLeftButtonDown += (_, e) => e.Handled = true;
		textBox.PreviewMouseLeftButtonUp += (_, e) => e.Handled = true;
		textBox.KeyDown += (_, e) =>
		{
			if (e.Key == Key.Enter)
			{
				Commit();
				e.Handled = true;
			}
			else if (e.Key == Key.Escape)
			{
				Cancel();
				e.Handled = true;
			}
		};
		textBox.LostFocus += (_, _) => Commit();

		panel.Children.RemoveAt(index);
		panel.Children.Insert(index, textBox);
		textBox.Focus();
		textBox.SelectAll();
	}
}
