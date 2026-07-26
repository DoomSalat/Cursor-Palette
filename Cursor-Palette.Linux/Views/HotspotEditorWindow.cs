using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using CursorPalette.Linux.Services;
using CursorPalette.Services;

namespace CursorPalette.Linux.Views;

public class HotspotEditorWindow : Window
{
	private const double MaxDisplaySize = 320;
	private const double MaxScaleFactor = 10;
	private const double MarkerSize = 12;
	private const double MarkerGlowSize = 20;
	private const double PresetGridSize = 36;
	private const double PresetGridSpacing = 2;

	private readonly int _nativeWidth;
	private readonly int _nativeHeight;
	private readonly double _scale;
	private readonly Canvas _previewCanvas;
	private readonly Image _previewImage;
	private readonly Ellipse _marker;
	private readonly Ellipse _markerGlow;
	private readonly TextBlock _coordsText;
	private readonly List<Button> _presetButtons = new();
	private int _x;
	private int _y;
	private bool _dragging;

	public int ResultX { get; private set; }
	public int ResultY { get; private set; }

	public HotspotEditorWindow(string filePath, CursorHotspot hotspot)
	{
		Title = Loc.Get("S.HotspotEditor.Title");
		Width = AppState.GetHotspotEditorWidth();
		Height = AppState.GetHotspotEditorHeight();
		WindowStartupLocation = WindowStartupLocation.CenterOwner;

		_nativeWidth = hotspot.Width;
		_nativeHeight = hotspot.Height;
		_x = Math.Clamp(hotspot.X, 0, _nativeWidth - 1);
		_y = Math.Clamp(hotspot.Y, 0, _nativeHeight - 1);
		_scale = ComputeScale(_nativeWidth, _nativeHeight);

		var displayWidth = _nativeWidth * _scale;
		var displayHeight = _nativeHeight * _scale;

		var root = new Grid
		{
			RowDefinitions = new RowDefinitions("Auto,*,Auto"),
		};

		var headerBar = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(16, 12, 16, 8),
			Spacing = 8,
		};

		headerBar.Children.Add(new TextBlock
		{
			Text = Loc.Get("S.Hotspot.Hint"),
			FontSize = 13,
			VerticalAlignment = VerticalAlignment.Center,
		});

		var infoButton = new Button
		{
			Content = "ⓘ",
			FontSize = 15,
			Padding = new Thickness(8, 4),
			HorizontalAlignment = HorizontalAlignment.Right,
		};
		infoButton.Click += (_, _) =>
		{
			var info = new InfoHelpWindow(Loc.Get("S.Info.Title"), HelpTextService.Get("Hotspot"));
			info.ShowDialog(this);
		};
		headerBar.Children.Add(infoButton);

		Grid.SetRow(headerBar, 0);
		root.Children.Add(headerBar);

		var contentPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Center,
			Spacing = 16,
			Margin = new Thickness(16, 0),
		};

		_previewCanvas = new Canvas
		{
			Width = displayWidth,
			Height = displayHeight,
			Background = Brushes.Transparent,
			Cursor = new Cursor(StandardCursorType.Cross),
		};

		_previewImage = new Image
		{
			Width = displayWidth,
			Height = displayHeight,
		};

		var preview = CursorPreviewService.GetPreview(filePath);
		if (preview != null)
			_previewImage.Source = preview;

		_previewCanvas.Children.Add(_previewImage);

		_markerGlow = new Ellipse
		{
			Width = MarkerGlowSize,
			Height = MarkerGlowSize,
			Fill = new SolidColorBrush(Color.FromArgb(80, 255, 100, 100)),
		};

		_marker = new Ellipse
		{
			Width = MarkerSize,
			Height = MarkerSize,
			Fill = Brushes.Red,
			Stroke = Brushes.White,
			StrokeThickness = 1,
		};

		_previewCanvas.Children.Add(_markerGlow);
		_previewCanvas.Children.Add(_marker);

		_previewCanvas.PointerPressed += OnCanvasPointerPressed;
		_previewCanvas.PointerMoved += OnCanvasPointerMoved;
		_previewCanvas.PointerReleased += OnCanvasPointerReleased;

		var previewBorder = new Border
		{
			Child = _previewCanvas,
			BorderBrush = Brushes.Gray,
			BorderThickness = new Thickness(1),
		};
		contentPanel.Children.Add(previewBorder);

		var presetPanel = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center,
			Spacing = 6,
		};

		presetPanel.Children.Add(new TextBlock
		{
			Text = Loc.Get("S.Hotspot.Presets"),
			FontSize = 11,
			Foreground = Brushes.Gray,
			HorizontalAlignment = HorizontalAlignment.Center,
		});

		var presetGrid = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions($"{PresetGridSize},{PresetGridSize},{PresetGridSize}"),
			RowDefinitions = new RowDefinitions($"{PresetGridSize},{PresetGridSize},{PresetGridSize}"),
		};

		var fractions = new[]
		{
			(0.0, 0.0), (0.5, 0.0), (1.0, 0.0),
			(0.0, 0.5), (0.5, 0.5), (1.0, 0.5),
			(0.0, 1.0), (0.5, 1.0), (1.0, 1.0),
		};

		for (var i = 0; i < 9; i++)
		{
			var (fx, fy) = fractions[i];
			var btn = new Button
			{
				Content = "•",
				FontSize = 14,
				Padding = new Thickness(0),
				Margin = new Thickness(PresetGridSpacing),
				Tag = $"{fx},{fy}",
			};
			btn.Click += OnPresetClick;
			Grid.SetColumn(btn, i % 3);
			Grid.SetRow(btn, i / 3);
			presetGrid.Children.Add(btn);
			_presetButtons.Add(btn);
		}

		presetPanel.Children.Add(presetGrid);
		contentPanel.Children.Add(presetPanel);

		Grid.SetRow(contentPanel, 1);
		root.Children.Add(contentPanel);

		var bottomBar = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
			Margin = new Thickness(16, 8, 16, 16),
		};

		_coordsText = new TextBlock
		{
			FontSize = 13,
			VerticalAlignment = VerticalAlignment.Center,
		};
		Grid.SetColumn(_coordsText, 0);
		bottomBar.Children.Add(_coordsText);

		var cancelButton = new Button
		{
			Content = Loc.Get("S.Editor.Cancel"),
			MinWidth = 90,
		};
		cancelButton.Click += (_, _) => Close();
		Grid.SetColumn(cancelButton, 1);
		bottomBar.Children.Add(cancelButton);

		var saveButton = new Button
		{
			Content = Loc.Get("S.Editor.Save"),
			MinWidth = 90,
		};
		saveButton.Click += OnSaveClick;
		Grid.SetColumn(saveButton, 2);
		bottomBar.Children.Add(saveButton);

		Grid.SetRow(bottomBar, 2);
		root.Children.Add(bottomBar);

		Content = root;

		var uiScale = AppState.GetUiScale();
		if (uiScale != 1.0)
		{
			root.RenderTransform = new ScaleTransform(uiScale, uiScale);
			root.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
		}

		RefreshMarker();
	}

	private static double ComputeScale(int width, int height)
	{
		var factor = Math.Min(MaxScaleFactor, Math.Min(MaxDisplaySize / width, MaxDisplaySize / height));
		return Math.Max(1, factor);
	}

	private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		_dragging = true;
		SetHotspotFromPointer(e.GetPosition(_previewCanvas));
		e.Pointer.Capture(_previewCanvas);
	}

	private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
	{
		if (_dragging)
			SetHotspotFromPointer(e.GetPosition(_previewCanvas));
	}

	private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		_dragging = false;
		e.Pointer.Capture(_previewCanvas);
	}

	private void SetHotspotFromPointer(Point position)
	{
		_x = Math.Clamp((int)Math.Round(position.X / _scale), 0, _nativeWidth - 1);
		_y = Math.Clamp((int)Math.Round(position.Y / _scale), 0, _nativeHeight - 1);
		RefreshMarker();
	}

	private void OnPresetClick(object? sender, RoutedEventArgs e)
	{
		if (sender is not Button button || button.Tag is not string tag)
			return;

		var parts = tag.Split(',');
		var fx = double.Parse(parts[0]);
		var fy = double.Parse(parts[1]);

		_x = PixelForFraction(fx, _nativeWidth);
		_y = PixelForFraction(fy, _nativeHeight);
		RefreshMarker();
	}

	private void RefreshMarker()
	{
		UpdateMarkerPosition();
		UpdateCoordsText();
		UpdatePresetHighlight();
	}

	private void UpdateMarkerPosition()
	{
		var displayX = (_x + 0.5) * _scale;
		var displayY = (_y + 0.5) * _scale;

		Canvas.SetLeft(_marker, displayX - MarkerSize / 2);
		Canvas.SetTop(_marker, displayY - MarkerSize / 2);
		Canvas.SetLeft(_markerGlow, displayX - MarkerGlowSize / 2);
		Canvas.SetTop(_markerGlow, displayY - MarkerGlowSize / 2);
	}

	private void UpdateCoordsText() =>
		_coordsText.Text = $"X: {_x}   Y: {_y}";

	private void UpdatePresetHighlight()
	{
		foreach (var btn in _presetButtons)
		{
			if (btn.Tag is not string tag)
				continue;

			var parts = tag.Split(',');
			var fx = double.Parse(parts[0]);
			var fy = double.Parse(parts[1]);

			var isCurrent = PixelForFraction(fx, _nativeWidth) == _x &&
				PixelForFraction(fy, _nativeHeight) == _y;

			btn.Background = isCurrent ? Brushes.CornflowerBlue : null;
		}
	}

	private static int PixelForFraction(double fraction, int nativeSize) =>
		Math.Clamp((int)Math.Round(fraction * (nativeSize - 1)), 0, nativeSize - 1);

	private void OnSaveClick(object? sender, RoutedEventArgs e)
	{
		ResultX = _x;
		ResultY = _y;
		Close(true);
	}

	protected override void OnClosed(EventArgs e)
	{
		AppState.SetHotspotEditorSize(Width, Height);
		base.OnClosed(e);
	}
}
