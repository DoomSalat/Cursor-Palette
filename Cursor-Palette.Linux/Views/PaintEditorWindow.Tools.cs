using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using CursorPalette.Services;

namespace CursorPalette.Linux.Views;

public partial class PaintEditorWindow
{
	private Button _moveLeftButton = null!;
	private Button _moveRightButton = null!;
	private Button _moveUpButton = null!;
	private Button _moveDownButton = null!;

	private void BuildMoveToolPanel()
	{
		_moveLeftButton = new Button { Content = "←", Padding = new Thickness(ToolButtonPadding), MinWidth = ToolButtonMinWidth };
		_moveRightButton = new Button { Content = "→", Padding = new Thickness(ToolButtonPadding), MinWidth = ToolButtonMinWidth };
		_moveUpButton = new Button { Content = "↑", Padding = new Thickness(ToolButtonPadding), MinWidth = ToolButtonMinWidth };
		_moveDownButton = new Button { Content = "↓", Padding = new Thickness(ToolButtonPadding), MinWidth = ToolButtonMinWidth };

		var snapGrid = new Grid
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			RowDefinitions = new RowDefinitions("*,*,*"),
			ColumnDefinitions = new ColumnDefinitions("*,*,*"),
			Width = SnapGridSize,
			Height = SnapGridSize,
		};

		var fractions = new[]
		{
			("0,0", "↖"), ("0.5,0", "↑"), ("1,0", "↗"),
			("0,0.5", "←"), ("0.5,0.5", "●"), ("1,0.5", "→"),
			("0,1", "↙"), ("0.5,1", "↓"), ("1,1", "↘"),
		};

		for (var index = 0; index < fractions.Length; index++)
		{
			var (tag, icon) = fractions[index];
			var row = index / 3;
			var col = index % 3;
			var snapButton = new Button
			{
				Content = icon,
				Tag = tag,
				Padding = new Thickness(SnapGridButtonPadding),
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Stretch,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				VerticalContentAlignment = VerticalAlignment.Center,
			};
			snapButton.Click += OnSnapClick;
			Grid.SetRow(snapButton, row);
			Grid.SetColumn(snapButton, col);
			snapGrid.Children.Add(snapButton);
		}

		_moveToolPanel.Children.Add(new TextBlock { Text = "Move Sprite", FontWeight = FontWeight.SemiBold });
		_moveToolPanel.Children.Add(new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Center,
			Spacing = SnapGridButtonPadding,
			Children = { _moveLeftButton, _moveRightButton, _moveUpButton, _moveDownButton },
		});
		_moveToolPanel.Children.Add(new TextBlock { Text = "Snap Position", FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, ToolPanelSpacing, 0, 0) });
		_moveToolPanel.Children.Add(snapGrid);

		_moveLeftButton.Click += OnMoveLeftClick;
		_moveRightButton.Click += OnMoveRightClick;
		_moveUpButton.Click += OnMoveUpClick;
		_moveDownButton.Click += OnMoveDownClick;
	}

	private void BuildHandToolPanel()
	{
		_handToolPanel.Children.Add(new TextBlock { Text = "Hand Tool", FontWeight = FontWeight.SemiBold });
		_handToolPanel.Children.Add(new TextBlock
		{
			Text = "Drag to pan the canvas.\nMiddle mouse also pans.\nCtrl+Scroll to zoom.",
			TextWrapping = Avalonia.Media.TextWrapping.Wrap,
			FontSize = ToolPanelLabelFontSize,
		});
	}

	private void OnMoveLeftClick(object? sender, RoutedEventArgs e)
	{
		var (min, _) = HorizontalRange();
		if (_offsetX <= min) return;
		PushHistory();
		_offsetX = Math.Max(min, _offsetX - 1);
		RenderAll();
	}

	private void OnMoveRightClick(object? sender, RoutedEventArgs e)
	{
		var (_, max) = HorizontalRange();
		if (_offsetX >= max) return;
		PushHistory();
		_offsetX = Math.Min(max, _offsetX + 1);
		RenderAll();
	}

	private void OnMoveUpClick(object? sender, RoutedEventArgs e)
	{
		var (min, _) = VerticalRange();
		if (_offsetY <= min) return;
		PushHistory();
		_offsetY = Math.Max(min, _offsetY - 1);
		RenderAll();
	}

	private void OnMoveDownClick(object? sender, RoutedEventArgs e)
	{
		var (_, max) = VerticalRange();
		if (_offsetY >= max) return;
		PushHistory();
		_offsetY = Math.Min(max, _offsetY + 1);
		RenderAll();
	}

	private void OnSnapClick(object? sender, RoutedEventArgs e)
	{
		if (sender is not Button snapButton || snapButton.Tag is not string tag)
			return;
		var (fractionX, fractionY) = ParseFraction(tag);
		var (minX, maxX) = HorizontalRange();
		var (minY, maxY) = VerticalRange();
		var newOffsetX = SnapOffset(fractionX, minX, maxX);
		var newOffsetY = SnapOffset(fractionY, minY, maxY);
		if (newOffsetX == _offsetX && newOffsetY == _offsetY)
			return;
		PushHistory();
		_offsetX = newOffsetX;
		_offsetY = newOffsetY;
		RenderAll();
	}
}
