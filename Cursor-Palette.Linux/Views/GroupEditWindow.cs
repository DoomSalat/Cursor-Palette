using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using CursorPalette.Models;
using CursorPalette.Services;

namespace CursorPalette.Linux.Views;

public class GroupEditWindow : Window
{
	private const double SwatchSize = 24;
	private const double SwatchRingThickness = 2.5;
	private const double DialogWidth = 360;
	private const double DialogHeight = 240;
	private const double DialogPadding = 16;
	private const double CloseButtonMinWidth = 90;

	private const string LocGroupEditTitle = "S.Group.Edit.Title";
	private const string LocGroupCreateTitle = "S.Group.Create";
	private const string LocGroupName = "S.Group.Edit.Name";
	private const string LocGroupSave = "S.Group.Edit.Save";
	private const string LocGroupCancel = "S.Group.Cancel";
	private const string LocGroupDefaultName = "S.Group.DefaultName";

	private string? _selectedColorKey;
	private readonly TextBox _nameBox;
	private readonly WrapPanel _colorSwatches;

	public string GroupName => _nameBox.Text?.Trim() ?? "";
	public string ColorKey => _selectedColorKey ?? GroupColors.Palette.First().Key;

	public GroupEditWindow(PresetGroup? group = null)
	{
		Title = group != null ? Loc.Get(LocGroupEditTitle) : Loc.Get(LocGroupCreateTitle);
		Width = DialogWidth;
		Height = DialogHeight;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		CanResize = false;

		if (group != null)
		{
			_nameBox = new TextBox { Text = group.Name };
			_selectedColorKey = group.ColorKey;
		}
		else
		{
			_nameBox = new TextBox();
			_selectedColorKey = GroupColors.Palette.First().Key;
		}

		_colorSwatches = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };

		var root = new StackPanel
		{
			Margin = new Thickness(DialogPadding),
			Spacing = 8,
		};

		var nameLabel = new TextBlock
		{
			Text = Loc.Get(LocGroupName),
			FontSize = 13,
		};
		root.Children.Add(nameLabel);
		root.Children.Add(_nameBox);

		root.Children.Add(_colorSwatches);

		var buttonPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Spacing = 8,
			Margin = new Thickness(0, 12, 0, 0),
			HorizontalAlignment = HorizontalAlignment.Right,
		};

		var cancelButton = new Button
		{
			Content = Loc.Get(LocGroupCancel),
			MinWidth = CloseButtonMinWidth,
		};
		cancelButton.Click += (_, _) => Close();
		buttonPanel.Children.Add(cancelButton);

		var saveButton = new Button
		{
			Content = Loc.Get(LocGroupSave),
			MinWidth = CloseButtonMinWidth,
		};
		saveButton.Click += (_, _) => OnSave();
		buttonPanel.Children.Add(saveButton);

		root.Children.Add(buttonPanel);

		Content = root;

		var uiScale = AppState.GetUiScale();
		if (uiScale != 1.0)
		{
			root.RenderTransform = new ScaleTransform(uiScale, uiScale);
			root.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
		}

		BuildColorSwatches();
		_nameBox.Focus();
	}

	private void BuildColorSwatches()
	{
		foreach (var (key, hex) in GroupColors.Palette)
		{
			var colorBrush = new SolidColorBrush(Color.Parse(hex));
			var capturedKey = key;

			var swatch = new Border
			{
				Width = SwatchSize + SwatchRingThickness * 2,
				Height = SwatchSize + SwatchRingThickness * 2,
				CornerRadius = new CornerRadius(SwatchSize),
				Background = colorBrush,
				BorderThickness = key == _selectedColorKey
					? new Thickness(SwatchRingThickness)
					: new Thickness(0),
				BorderBrush = Brushes.White,
				Margin = new Thickness(3),
			};

			swatch.PointerPressed += (_, _) =>
			{
				_selectedColorKey = capturedKey;
				foreach (var child in _colorSwatches.Children.OfType<Border>())
					child.BorderThickness = new Thickness(0);
				swatch.BorderThickness = new Thickness(SwatchRingThickness);
			};

			_colorSwatches.Children.Add(swatch);
		}
	}

	private void OnSave()
	{
		if (string.IsNullOrWhiteSpace(GroupName))
			_nameBox.Text = Loc.Get(LocGroupDefaultName);

		Close(true);
	}
}
