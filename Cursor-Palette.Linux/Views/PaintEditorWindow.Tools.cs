using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using CursorPalette.Linux.Services;
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
		_moveLeftButton = new Button { Content = IconHelper.CreateIcon("ArrowIcon48.png", 18, Brushes.White, 180), Padding = new Thickness(ToolButtonPadding), MinWidth = ToolButtonMinWidth };
		_moveRightButton = new Button { Content = IconHelper.CreateIcon("ArrowIcon48.png", 18, Brushes.White), Padding = new Thickness(ToolButtonPadding), MinWidth = ToolButtonMinWidth };
		_moveUpButton = new Button { Content = IconHelper.CreateIcon("ArrowIcon48.png", 18, Brushes.White, -90), Padding = new Thickness(ToolButtonPadding), MinWidth = ToolButtonMinWidth };
		_moveDownButton = new Button { Content = IconHelper.CreateIcon("ArrowIcon48.png", 18, Brushes.White, 90), Padding = new Thickness(ToolButtonPadding), MinWidth = ToolButtonMinWidth };

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
			("0,0", CreateSnapIcon("ArrowIcon48.png", -45)), ("0.5,0", CreateSnapIcon("ArrowIcon48.png", -90)), ("1,0", CreateSnapIcon("ArrowIcon48.png", 45)),
			("0,0.5", CreateSnapIcon("ArrowIcon48.png", 180)), ("0.5,0.5", CreateSnapIcon("CenterIcon26.png", 0)), ("1,0.5", CreateSnapIcon("ArrowIcon48.png", 0)),
			("0,1", CreateSnapIcon("ArrowIcon48.png", 135)), ("0.5,1", CreateSnapIcon("ArrowIcon48.png", 90)), ("1,1", CreateSnapIcon("ArrowIcon48.png", -45)),
		};

		for (var i = 0; i < fractions.Length; i++)
		{
			var (tag, icon) = fractions[i];
			var row = i / 3;
			var col = i % 3;
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

	private static Control CreateSnapIcon(string fileName, double rotation)
	{
		return IconHelper.CreateIcon(fileName, 18, Brushes.White, rotation);
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

	private void BuildIconSizesToolPanel()
	{
		_iconSizesUnavailableHint = new TextBlock
		{
			FontSize = 12,
			HorizontalAlignment = HorizontalAlignment.Center,
			TextWrapping = TextWrapping.Wrap,
			TextAlignment = TextAlignment.Center,
			IsVisible = false,
		};

		_iconSizesContentPanel = new StackPanel();

		_iconSizesHintText = new TextBlock
		{
			FontSize = 11,
			HorizontalAlignment = HorizontalAlignment.Center,
			TextWrapping = TextWrapping.Wrap,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(0, 0, 0, 10),
		};
		_iconSizesContentPanel.Children.Add(_iconSizesHintText);

		_iconSizesScaleModeResetButton = new Button
		{
			Content = "⟲",
			Padding = new Thickness(6, 0, 6, 1),
			Margin = new Thickness(6, 0, 0, 0),
			IsVisible = false,
		};
		_iconSizesScaleModeResetButton.Click += OnIconSizesScaleModeResetClick;

		_iconSizesScaleModeIcon = new Image
		{
			Width = 16,
			Height = 16,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
		};

		_iconSizesScaleModeIconBorder = new Border
		{
			Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
			BorderBrush = Brushes.Gray,
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(4),
			Padding = new Thickness(4),
			HorizontalAlignment = HorizontalAlignment.Right,
			Cursor = new Cursor(StandardCursorType.Hand),
			Child = _iconSizesScaleModeIcon,
		};
		_iconSizesScaleModeIconBorder.PointerPressed += OnIconSizesScaleModeIconClick;

		_iconSizesScaleModeLabel = new TextBlock
		{
			FontSize = 11,
			VerticalAlignment = VerticalAlignment.Center,
			TextWrapping = TextWrapping.Wrap,
		};

		var scaleModeBar = new DockPanel
		{
			Children =
			{
				_iconSizesScaleModeResetButton,
				_iconSizesScaleModeIconBorder,
				_iconSizesScaleModeLabel,
			},
		};
		DockPanel.SetDock(_iconSizesScaleModeResetButton, Dock.Right);
		DockPanel.SetDock(_iconSizesScaleModeIconBorder, Dock.Right);

		var scaleModeBorder = new Border
		{
			BorderBrush = Brushes.Gray,
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(4),
			Padding = new Thickness(8, 6),
			Margin = new Thickness(0, 0, 0, 10),
			Child = scaleModeBar,
		};
		_iconSizesContentPanel.Children.Add(scaleModeBorder);

		_iconSizesEditModeCheck = new ToggleButton
		{
			Margin = new Thickness(0, 0, 0, 10),
		};
		_iconSizesEditModeCheck.IsCheckedChanged += OnIconSizesEditModeChanged;
		_iconSizesContentPanel.Children.Add(_iconSizesEditModeCheck);

		_iconSizesAddSizeButton = new Button
		{
			Content = "+",
			Padding = new Thickness(10, 4),
			Margin = new Thickness(6, 0, 0, 0),
		};
		_iconSizesAddSizeButton.Click += OnIconSizesAddSizeClick;
		ToolTip.SetTip(_iconSizesAddSizeButton, Loc.Get("S.Editor.Tool.IconSizesAdd"));

		_iconSizesAddSizeBox = new TextBox
		{
			Padding = new Thickness(4, 2),
			HorizontalContentAlignment = HorizontalAlignment.Center,
		};

		var addSizeRow = new DockPanel
		{
			Margin = new Thickness(0, 0, 0, 10),
			Children = { _iconSizesAddSizeButton, _iconSizesAddSizeBox },
		};
		DockPanel.SetDock(_iconSizesAddSizeButton, Dock.Right);
		_iconSizesContentPanel.Children.Add(addSizeRow);

		_iconSizesListPanel = new StackPanel();
		_iconSizesContentPanel.Children.Add(_iconSizesListPanel);

		_iconSizesSummaryText = new TextBlock
		{
			FontSize = 11,
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0, 10, 0, 10),
		};
		_iconSizesContentPanel.Children.Add(_iconSizesSummaryText);

		_iconSizesApplyButton = new Button
		{
			HorizontalAlignment = HorizontalAlignment.Stretch,
			HorizontalContentAlignment = HorizontalAlignment.Center,
		};
		_iconSizesApplyButton.Click += OnIconSizesApplyClick;
		_iconSizesContentPanel.Children.Add(_iconSizesApplyButton);

		_iconSizesToolPanel.Children.Add(_iconSizesUnavailableHint);
		_iconSizesToolPanel.Children.Add(_iconSizesContentPanel);
	}
}
