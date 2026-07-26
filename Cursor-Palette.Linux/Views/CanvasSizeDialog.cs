using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using CursorPalette.Services;

namespace CursorPalette.Linux.Views;

public class CanvasSizeDialog : Window
{
	private const int MinDimension = 1;
	private const int MaxDimension = 256;
	private const double DialogWidth = 400;
	private const double DialogHeight = 420;
	private const double DialogPadding = 16;
	private const double PreviewMaxW = 232;
	private const double PreviewMaxH = 140;
	private const double AnchorGridSpacing = 4;
	private const double AnchorButtonSize = 28;

	private static readonly (int W, int H)[] CanvasPresets =
	{
		(16, 16), (24, 24), (32, 32), (48, 48),
		(64, 64), (96, 96), (128, 128), (256, 256),
	};

	private readonly int _originalWidth;
	private readonly int _originalHeight;
	private int _anchorX = 1;
	private int _anchorY = 1;

	private readonly TextBox _widthBox;
	private readonly TextBox _heightBox;
	private readonly ComboBox _presetCombo;
	private readonly Canvas _previewCanvas;
	private readonly Border _currentRect;
	private readonly Border _newRect;
	private readonly List<Button> _anchorButtons = new();

	public int ResultWidth { get; private set; }
	public int ResultHeight { get; private set; }
	public int ResultAnchorX => _anchorX;
	public int ResultAnchorY => _anchorY;

	public CanvasSizeDialog(int currentWidth, int currentHeight)
	{
		_originalWidth = currentWidth;
		_originalHeight = currentHeight;

		Title = Loc.Get("S.CanvasSize.Title");
		Width = DialogWidth;
		Height = DialogHeight;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		CanResize = false;

		var root = new StackPanel
		{
			Margin = new Thickness(DialogPadding),
			Spacing = 12,
		};

		var presetLabel = new TextBlock { Text = Loc.Get("S.CanvasSize.Preset"), FontSize = 13 };
		root.Children.Add(presetLabel);

		_presetCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
		PopulatePresets();
		_presetCombo.SelectionChanged += OnPresetSelectionChanged;
		root.Children.Add(_presetCombo);

		var sizeGrid = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,*,Auto"),
			Margin = new Thickness(0, 4, 0, 0),
		};

		var widthLabel = new TextBlock
		{
			Text = Loc.Get("S.CanvasSize.Width"),
			FontSize = 13,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0, 0, 8, 0),
		};
		Grid.SetColumn(widthLabel, 0);
		sizeGrid.Children.Add(widthLabel);

		_widthBox = new TextBox
		{
			Text = currentWidth.ToString(),
			FontSize = 13,
		};
		_widthBox.TextChanged += (_, _) => UpdatePreview();
		Grid.SetColumn(_widthBox, 1);
		sizeGrid.Children.Add(_widthBox);

		var heightLabel = new TextBlock
		{
			Text = Loc.Get("S.CanvasSize.Height"),
			FontSize = 13,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(8, 0, 8, 0),
		};
		Grid.SetColumn(heightLabel, 2);
		sizeGrid.Children.Add(heightLabel);

		_heightBox = new TextBox
		{
			Text = currentHeight.ToString(),
			FontSize = 13,
		};
		_heightBox.TextChanged += (_, _) => UpdatePreview();
		Grid.SetColumn(_heightBox, 3);
		sizeGrid.Children.Add(_heightBox);

		root.Children.Add(sizeGrid);

		var anchorLabel = new TextBlock
		{
			Text = Loc.Get("S.CanvasSize.Anchor"),
			FontSize = 13,
			Margin = new Thickness(0, 4, 0, 0),
		};
		root.Children.Add(anchorLabel);

		var anchorGrid = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions($"{AnchorButtonSize},{AnchorButtonSize},{AnchorButtonSize}"),
			RowDefinitions = new RowDefinitions($"{AnchorButtonSize},{AnchorButtonSize},{AnchorButtonSize}"),
			HorizontalAlignment = HorizontalAlignment.Left,
			Margin = new Thickness(0, 2, 0, 0),
		};

		for (var ay = 0; ay < 3; ay++)
		{
			for (var ax = 0; ax < 3; ax++)
			{
				var btn = new Button
				{
					Content = "",
					Width = AnchorButtonSize - AnchorGridSpacing,
					Height = AnchorButtonSize - AnchorGridSpacing,
					Margin = new Thickness(0),
					Padding = new Thickness(0),
					HorizontalAlignment = HorizontalAlignment.Stretch,
					VerticalAlignment = VerticalAlignment.Stretch,
					Tag = $"{ax},{ay}",
				};
				btn.Click += OnAnchorClick;
				Grid.SetColumn(btn, ax);
				Grid.SetRow(btn, ay);
				anchorGrid.Children.Add(btn);
				_anchorButtons.Add(btn);
			}
		}

		root.Children.Add(anchorGrid);

		var previewLabel = new TextBlock
		{
			Text = Loc.Get("S.CanvasSize.Current") + " / " + Loc.Get("S.CanvasSize.New"),
			FontSize = 13,
			Margin = new Thickness(0, 4, 0, 0),
		};
		root.Children.Add(previewLabel);

		_previewCanvas = new Canvas
		{
			Width = PreviewMaxW,
			Height = PreviewMaxH,
			Background = Brushes.DarkGray,
			Margin = new Thickness(0, 2, 0, 0),
		};

		_currentRect = new Border
		{
			BorderBrush = Brushes.Gray,
			BorderThickness = new Thickness(1),
			Background = new SolidColorBrush(Color.FromArgb(80, 128, 128, 128)),
		};

		_newRect = new Border
		{
			BorderBrush = Brushes.CornflowerBlue,
			BorderThickness = new Thickness(2),
			Background = new SolidColorBrush(Color.FromArgb(40, 79, 140, 255)),
		};

		_previewCanvas.Children.Add(_currentRect);
		_previewCanvas.Children.Add(_newRect);
		root.Children.Add(_previewCanvas);

		var bottomBar = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Spacing = 8,
			Margin = new Thickness(0, 4, 0, 0),
		};

		var cancelButton = new Button
		{
			Content = Loc.Get("S.CanvasSize.Cancel"),
			MinWidth = 80,
		};
		cancelButton.Click += (_, _) => Close();
		bottomBar.Children.Add(cancelButton);

		var applyButton = new Button
		{
			Content = Loc.Get("S.CanvasSize.Apply"),
			MinWidth = 80,
		};
		applyButton.Click += OnApplyClick;
		bottomBar.Children.Add(applyButton);

		root.Children.Add(bottomBar);

		Content = root;

		var uiScale = AppState.GetUiScale();
		if (uiScale != 1.0)
		{
			root.RenderTransform = new ScaleTransform(uiScale, uiScale);
			root.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
		}

		ResultWidth = currentWidth;
		ResultHeight = currentHeight;

		UpdateAnchorHighlight();
		UpdatePreview();
	}

	private void PopulatePresets()
	{
		_presetCombo.Items.Add(new ComboBoxItem { Content = "—" });

		foreach (var (w, h) in CanvasPresets)
			_presetCombo.Items.Add(new ComboBoxItem { Content = $"{w}×{h}", Tag = (w, h) });

		_presetCombo.SelectedIndex = 0;
	}

	private void OnPresetSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (_presetCombo.SelectedItem is not ComboBoxItem item)
			return;

		if (item.Tag is not (int w, int h))
			return;

		_widthBox.Text = w.ToString();
		_heightBox.Text = h.ToString();

		UpdatePreview();
	}

	private void OnAnchorClick(object? sender, RoutedEventArgs e)
	{
		if (sender is not Button button || button.Tag is not string tag)
			return;

		var parts = tag.Split(',');
		_anchorX = int.Parse(parts[0]);
		_anchorY = int.Parse(parts[1]);

		UpdateAnchorHighlight();
		UpdatePreview();
	}

	private void UpdateAnchorHighlight()
	{
		foreach (var btn in _anchorButtons)
		{
			if (btn.Tag is not string tag)
				continue;

			var parts = tag.Split(',');
			var ax = int.Parse(parts[0]);
			var ay = int.Parse(parts[1]);
			var isCurrent = ax == _anchorX && ay == _anchorY;

			btn.Background = isCurrent ? Brushes.CornflowerBlue : null;
		}
	}

	private static int ParseDimension(string? text, int fallback) =>
		int.TryParse(text, out var value)
			? Math.Clamp(value, MinDimension, MaxDimension)
			: fallback;

	private void UpdatePreview()
	{
		var newW = ParseDimension(_widthBox.Text, _originalWidth);
		var newH = ParseDimension(_heightBox.Text, _originalHeight);

		ResultWidth = newW;
		ResultHeight = newH;

		var scale = Math.Min(
			PreviewMaxW / Math.Max(newW, _originalWidth),
			PreviewMaxH / Math.Max(newH, _originalHeight));

		var curW = _originalWidth * scale;
		var curH = _originalHeight * scale;
		var newWd = newW * scale;
		var newHd = newH * scale;

		var offsetX = _anchorX * (newWd - curW) / 2.0;
		var offsetY = _anchorY * (newHd - curH) / 2.0;

		var curX = (PreviewMaxW - newWd) / 2.0 + offsetX;
		var curY = (PreviewMaxH - newHd) / 2.0 + offsetY;

		Canvas.SetLeft(_currentRect, curX);
		Canvas.SetTop(_currentRect, curY);
		_currentRect.Width = curW;
		_currentRect.Height = curH;

		var newX = (PreviewMaxW - newWd) / 2.0;
		var newY = (PreviewMaxH - newHd) / 2.0;

		Canvas.SetLeft(_newRect, newX);
		Canvas.SetTop(_newRect, newY);
		_newRect.Width = newWd;
		_newRect.Height = newHd;
	}

	private void OnApplyClick(object? sender, RoutedEventArgs e)
	{
		UpdatePreview();
		Close(true);
	}
}
