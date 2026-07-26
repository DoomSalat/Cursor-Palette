using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using CursorPalette.Services;

namespace CursorPalette.Linux.Controls;

public class ColorWheelControl : UserControl
{
	private const int WheelSize = 140;
	private const int BitmapDpi = 96;
	private const int ByteMax = 255;
	private const double AlphaToPercentFactor = 255.0 / 100.0;
	private const double FullCircleDegrees = 360;
	private const double HueSegmentDegrees = 60;
	private const double DeltaEpsilon = 0.00001;
	private const double IndicatorHalfSize = 5;

	private enum PickerMode { Wheel, Square }

	private double _hue;
	private double _saturation = 1.0;
	private double _value = 1.0;
	private double _alphaPercent = 100;

	private bool _isDraggingWheel;
	private bool _isDraggingSquare;
	private bool _initialized;
	private PickerMode _mode = PickerMode.Wheel;

	public event EventHandler? ColorChanged;
	public event EventHandler? EyedropperRequested;

	public Color SelectedColor
	{
		get
		{
			var (red, green, blue) = HsvToRgb(_hue, _saturation, _value);
			var alpha = (byte)Math.Round(_alphaPercent * AlphaToPercentFactor);

			return Color.FromArgb(alpha, red, green, blue);
		}
	}

	public (double Hue, double Saturation, double Value, double Alpha) GetHsv() =>
		(_hue, _saturation, _value, _alphaPercent);

	private readonly Image _wheelImage;
	private readonly Image _squareImage;
	private readonly Ellipse _indicator;
	private readonly Ellipse _squareIndicator;
	private readonly Ellipse _brightnessOverlay;
	private readonly Canvas _indicatorLayer;
	private readonly Canvas _squareIndicatorLayer;
	private readonly Slider _brightnessSlider;
	private readonly Slider _hueSlider;
	private readonly Slider _alphaSlider;
	private readonly TextBlock _alphaPercentText;
	private readonly Rectangle _colorPreview;
	private readonly TextBox _hexText;
	private readonly Border _eyedropperButton;
	private readonly Border _wheelModeButton;
	private readonly Border _squareModeButton;
	private readonly StackPanel _hueRow;
	private readonly StackPanel _brightnessRow;
	private readonly Ellipse _wheelModeIcon;
	private readonly Rectangle _squareModeIcon;

	public ColorWheelControl()
	{
		_wheelImage = new Image
		{
			Width = WheelSize,
			Height = WheelSize,
			Cursor = new Cursor(StandardCursorType.Cross),
		};
		_squareImage = new Image
		{
			Width = WheelSize,
			Height = WheelSize,
			Cursor = new Cursor(StandardCursorType.Cross),
			IsVisible = false,
		};
		_indicator = new Ellipse
		{
			Width = 10,
			Height = 10,
			Stroke = Brushes.White,
			StrokeThickness = 2,
			Fill = Brushes.Transparent,
		};
		_squareIndicator = new Ellipse
		{
			Width = 10,
			Height = 10,
			Stroke = Brushes.White,
			StrokeThickness = 2,
			Fill = Brushes.Transparent,
		};
		_brightnessOverlay = new Ellipse
		{
			Width = WheelSize,
			Height = WheelSize,
			IsHitTestVisible = false,
			Fill = Brushes.Black,
			Opacity = 0,
		};
		_indicatorLayer = new Canvas
		{
			Width = WheelSize,
			Height = WheelSize,
			IsHitTestVisible = false,
			Children = { _indicator },
		};
		_squareIndicatorLayer = new Canvas
		{
			Width = WheelSize,
			Height = WheelSize,
			IsHitTestVisible = false,
			IsVisible = false,
			Children = { _squareIndicator },
		};

		_wheelImage.Source = GenerateWheelBitmap();

		var wheelGrid = new Grid
		{
			Width = WheelSize,
			Height = WheelSize,
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0, 0, 0, 10),
			Children = { _wheelImage, _brightnessOverlay, _indicatorLayer, _squareImage, _squareIndicatorLayer },
		};

		_eyedropperButton = new Border
		{
			Width = 24,
			Height = 20,
			HorizontalAlignment = HorizontalAlignment.Left,
			CornerRadius = new CornerRadius(4),
			Background = Brushes.Transparent,
			BorderBrush = Brushes.Gray,
			BorderThickness = new Thickness(1),
			Cursor = new Cursor(StandardCursorType.Hand),
			Child = new TextBlock { Text = "💧", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
		};

		_wheelModeIcon = new Ellipse
		{
			Width = 10,
			Height = 10,
			Stroke = Brushes.White,
			StrokeThickness = 1.5,
			Fill = Brushes.Transparent,
		};
		_squareModeIcon = new Rectangle
		{
			Width = 10,
			Height = 10,
			Stroke = Brushes.White,
			StrokeThickness = 1.5,
			Fill = Brushes.Transparent,
		};

		_wheelModeButton = new Border
		{
			Width = 24,
			Height = 20,
			CornerRadius = new CornerRadius(4, 0, 0, 4),
			Background = GetAccentBrush(),
			BorderBrush = Brushes.Gray,
			BorderThickness = new Thickness(1, 1, 0, 1),
			Cursor = new Cursor(StandardCursorType.Hand),
			Child = _wheelModeIcon,
		};
		_squareModeButton = new Border
		{
			Width = 24,
			Height = 20,
			CornerRadius = new CornerRadius(0, 4, 4, 0),
			Background = Brushes.Transparent,
			BorderBrush = Brushes.Gray,
			BorderThickness = new Thickness(1),
			Cursor = new Cursor(StandardCursorType.Hand),
			Child = _squareModeIcon,
		};

		var modeToggle = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Children = { _wheelModeButton, _squareModeButton },
		};

		var topBar = new Grid
		{
			Width = WheelSize,
			Margin = new Thickness(0, 0, 0, 6),
			Children = { _eyedropperButton, modeToggle },
		};

		_hueSlider = new Slider
		{
			Minimum = 0,
			Maximum = 360,
			Value = 0,
			SmallChange = 1,
			LargeChange = 10,
		};
		_brightnessSlider = new Slider
		{
			Minimum = 0,
			Maximum = 1,
			Value = 1,
			SmallChange = 0.01,
			LargeChange = 0.1,
			Margin = new Thickness(0, 0, 0, 10),
		};
		_alphaSlider = new Slider
		{
			Minimum = 0,
			Maximum = 100,
			Value = 100,
			SmallChange = 1,
			LargeChange = 10,
			Margin = new Thickness(0, 0, 0, 10),
		};

		_hueRow = new StackPanel
		{
			IsVisible = false,
			Children =
			{
				new TextBlock { Text = "Hue", FontSize = 11, Margin = new Thickness(0, 0, 0, 4) },
				_hueSlider,
			},
		};
		_brightnessRow = new StackPanel
		{
			Children =
			{
				new TextBlock { Text = "Brightness", FontSize = 11, Margin = new Thickness(0, 0, 0, 4) },
				_brightnessSlider,
			},
		};

		_alphaPercentText = new TextBlock
		{
			FontSize = 11,
			VerticalAlignment = VerticalAlignment.Center,
		};

		_colorPreview = new Rectangle { Width = 24, Height = 24 };
		_hexText = new TextBox
		{
			FontSize = 12,
			Width = 90,
			Margin = new Thickness(8, 0, 0, 0),
		};

		Content = new StackPanel
		{
			Children =
			{
				topBar,
				wheelGrid,
				_hueRow,
				_brightnessRow,
				new Grid
				{
					Margin = new Thickness(0, 0, 0, 10),
					ColumnDefinitions = new ColumnDefinitions("*,Auto"),
					Children =
					{
						new TextBlock { Text = "Alpha", FontSize = 11, VerticalAlignment = VerticalAlignment.Center },
						_alphaPercentText,
					},
				},
				_alphaSlider,
				new StackPanel
				{
					Orientation = Orientation.Horizontal,
					HorizontalAlignment = HorizontalAlignment.Center,
					Margin = new Thickness(0, 4, 0, 0),
					Children = { _colorPreview, _hexText },
				},
			},
		};

		Grid.SetColumn(_alphaPercentText, 1);

		_eyedropperButton.PointerPressed += OnEyedropperClick;
		_wheelModeButton.PointerPressed += OnWheelModeClick;
		_squareModeButton.PointerPressed += OnSquareModeClick;
		_wheelImage.PointerPressed += OnWheelMouseDown;
		_wheelImage.PointerMoved += OnWheelMouseMove;
		_wheelImage.PointerReleased += OnWheelMouseUp;
		_squareImage.PointerPressed += OnSquareMouseDown;
		_squareImage.PointerMoved += OnSquareMouseMove;
		_squareImage.PointerReleased += OnSquareMouseUp;
		_hueSlider.ValueChanged += OnHueChanged;
		_brightnessSlider.ValueChanged += OnBrightnessChanged;
		_alphaSlider.ValueChanged += OnAlphaChanged;
		_hexText.KeyDown += OnHexTextKeyDown;
		_hexText.LostFocus += OnHexTextLostFocus;

		UpdateIndicator();
		UpdateSquareIndicator();
		UpdatePreview();

		_initialized = true;
	}

	public void SetColor(double hue, double saturation, double value, double alphaPercent)
	{
		_hue = hue;
		_saturation = saturation;
		_value = value;
		_alphaPercent = alphaPercent;

		_brightnessSlider.Value = _value;
		_hueSlider.Value = _hue;
		_alphaSlider.Value = _alphaPercent;

		UpdateIndicator();
		UpdateSquareIndicator();

		if (_mode == PickerMode.Square)
			_squareImage.Source = GenerateSquareBitmap(_hue);

		UpdatePreview();
		UpdateAlphaPercent();
	}

	public void SetColorFromRgb(byte red, byte green, byte blue) => ApplyRgb(ByteMax, red, green, blue);

	public void SetEyedropperActive(bool active) =>
		_eyedropperButton.Background = active ? GetAccentBrush() : Brushes.Transparent;

	public string GetColorMode() => _mode == PickerMode.Square ?
		AppState.PaintEditorColorModeSquare : AppState.PaintEditorColorModeWheel;

	public void SetColorMode(string mode) =>
		SetMode(string.Equals(mode, AppState.PaintEditorColorModeSquare, StringComparison.OrdinalIgnoreCase) ?
			PickerMode.Square : PickerMode.Wheel);

	private static IBrush GetAccentBrush() =>
		Application.Current?.TryFindResource("SystemAccentColor", out var accent) == true && accent is Color c
			? new SolidColorBrush(c)
			: Brushes.DodgerBlue;

	private void ApplyRgb(byte alpha, byte red, byte green, byte blue)
	{
		var (hue, saturation, value) = RgbToHsv(red, green, blue);

		_hue = hue;
		_saturation = saturation;
		_value = value;
		_alphaPercent = alpha / AlphaToPercentFactor;

		_brightnessSlider.Value = _value;
		_hueSlider.Value = _hue;
		_alphaSlider.Value = _alphaPercent;

		UpdateIndicator();
		UpdateSquareIndicator();

		if (_mode == PickerMode.Square)
			_squareImage.Source = GenerateSquareBitmap(_hue);

		UpdatePreview();
		UpdateAlphaPercent();

		ColorChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OnEyedropperClick(object? sender, PointerPressedEventArgs e) =>
		EyedropperRequested?.Invoke(this, EventArgs.Empty);

	private void OnWheelModeClick(object? sender, PointerPressedEventArgs e) => SetMode(PickerMode.Wheel);

	private void OnSquareModeClick(object? sender, PointerPressedEventArgs e) => SetMode(PickerMode.Square);

	private void SetMode(PickerMode mode)
	{
		if (_mode == mode)
			return;

		_mode = mode;
		var isSquare = mode == PickerMode.Square;

		_wheelImage.IsVisible = !isSquare;
		_brightnessOverlay.IsVisible = !isSquare;
		_indicator.IsVisible = !isSquare;
		_squareImage.IsVisible = isSquare;
		_squareIndicatorLayer.IsVisible = isSquare;
		_hueRow.IsVisible = isSquare;
		_brightnessRow.IsVisible = !isSquare;

		_wheelModeButton.Background = isSquare ? Brushes.Transparent : GetAccentBrush();
		_squareModeButton.Background = isSquare ? GetAccentBrush() : Brushes.Transparent;

		if (isSquare)
		{
			_squareImage.Source = GenerateSquareBitmap(_hue);
			UpdateSquareIndicator();
		}
	}

	private void OnWheelMouseDown(object? sender, PointerPressedEventArgs e)
	{
		_isDraggingWheel = true;
		_wheelImage.Cursor = new Cursor(StandardCursorType.Cross);
		UpdateFromMouse(e.GetPosition(_wheelImage));
		e.Handled = true;
	}

	private void OnWheelMouseMove(object? sender, PointerEventArgs e)
	{
		if (!_isDraggingWheel)
			return;

		UpdateFromMouse(e.GetPosition(_wheelImage));
		e.Handled = true;
	}

	private void OnWheelMouseUp(object? sender, PointerReleasedEventArgs e)
	{
		_isDraggingWheel = false;
		e.Handled = true;
	}

	private void UpdateFromMouse(Point position)
	{
		var center = WheelSize / 2.0;
		var deltaX = position.X - center;
		var deltaY = position.Y - center;
		var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

		_hue = (Math.Atan2(deltaY, deltaX) * 180.0 / Math.PI + FullCircleDegrees) % FullCircleDegrees;
		_saturation = Math.Min(1, distance / center);

		UpdateIndicator();
		UpdatePreview();
		ColorChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OnSquareMouseDown(object? sender, PointerPressedEventArgs e)
	{
		_isDraggingSquare = true;
		UpdateFromSquareMouse(e.GetPosition(_squareImage));
		e.Handled = true;
	}

	private void OnSquareMouseMove(object? sender, PointerEventArgs e)
	{
		if (!_isDraggingSquare)
			return;
		UpdateFromSquareMouse(e.GetPosition(_squareImage));
		e.Handled = true;
	}

	private void OnSquareMouseUp(object? sender, PointerReleasedEventArgs e)
	{
		_isDraggingSquare = false;
		e.Handled = true;
	}

	private void UpdateFromSquareMouse(Point position)
	{
		_saturation = Math.Min(1, Math.Max(0, position.X / WheelSize));
		_value = Math.Min(1, Math.Max(0, 1 - position.Y / WheelSize));

		UpdateSquareIndicator();
		UpdatePreview();
		ColorChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OnHueChanged(object? sender, RangeBaseValueChangedEventArgs e)
	{
		_hue = e.NewValue;
		if (_mode == PickerMode.Square)
			_squareImage.Source = GenerateSquareBitmap(_hue);
		if (!_initialized)
			return;
		UpdatePreview();
		ColorChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OnBrightnessChanged(object? sender, RangeBaseValueChangedEventArgs e)
	{
		_value = e.NewValue;
		_brightnessOverlay.Opacity = 1 - _value;
		if (!_initialized)
			return;
		UpdatePreview();
		ColorChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OnAlphaChanged(object? sender, RangeBaseValueChangedEventArgs e)
	{
		_alphaPercent = e.NewValue;
		if (!_initialized)
			return;
		UpdateAlphaPercent();
		UpdatePreview();
		ColorChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OnHexTextKeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key != Key.Enter)
			return;
		ApplyHexInput(_hexText.Text ?? "");
		e.Handled = true;
	}

	private void OnHexTextLostFocus(object? sender, RoutedEventArgs e) => ApplyHexInput(_hexText.Text ?? "");

	private void ApplyHexInput(string text)
	{
		if (!TryParseHex(text, out var alpha, out var red, out var green, out var blue))
		{
			UpdatePreview();
			return;
		}
		ApplyRgb(alpha, red, green, blue);
	}

	private void UpdateIndicator()
	{
		var center = WheelSize / 2.0;
		var angle = _hue * Math.PI / 180.0;
		var radius = _saturation * center;
		var indicatorX = center + radius * Math.Cos(angle) - IndicatorHalfSize;
		var indicatorY = center + radius * Math.Sin(angle) - IndicatorHalfSize;
		Canvas.SetLeft(_indicator, indicatorX);
		Canvas.SetTop(_indicator, indicatorY);
	}

	private void UpdateSquareIndicator()
	{
		var indicatorX = _saturation * WheelSize - IndicatorHalfSize;
		var indicatorY = (1 - _value) * WheelSize - IndicatorHalfSize;
		Canvas.SetLeft(_squareIndicator, indicatorX);
		Canvas.SetTop(_squareIndicator, indicatorY);
	}

	private void UpdatePreview()
	{
		var (red, green, blue) = HsvToRgb(_hue, _saturation, _value);
		var alpha = (byte)Math.Round(_alphaPercent * AlphaToPercentFactor);
		_colorPreview.Fill = new SolidColorBrush(Color.FromArgb(alpha, red, green, blue));
		_hexText.Text = $"#{alpha:X2}{red:X2}{green:X2}{blue:X2}";
	}

	private void UpdateAlphaPercent() =>
		_alphaPercentText.Text = $"{(int)Math.Round(_alphaPercent)}%";

	private static WriteableBitmap GenerateWheelBitmap()
	{
		var size = WheelSize;
		var bitmap = new WriteableBitmap(
			new PixelSize(size, size),
			new Vector(BitmapDpi, BitmapDpi),
			Avalonia.Platform.PixelFormat.Bgra8888,
			Avalonia.Platform.AlphaFormat.Unpremul);
		var pixels = new byte[size * size * 4];
		var center = size / 2.0;

		for (var y = 0; y < size; y++)
		{
			for (var x = 0; x < size; x++)
			{
				var deltaX = x - center;
				var deltaY = y - center;
				var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
				var pixelIndex = (y * size + x) * 4;

				if (distance > center)
				{
					pixels[pixelIndex + 3] = 0;
					continue;
				}

				var hue = (Math.Atan2(deltaY, deltaX) * 180.0 / Math.PI + FullCircleDegrees) % FullCircleDegrees;
				var saturation = Math.Min(1, distance / center);
				var (red, green, blue) = HsvToRgb(hue, saturation, 1.0);

				pixels[pixelIndex] = blue;
				pixels[pixelIndex + 1] = green;
				pixels[pixelIndex + 2] = red;
				pixels[pixelIndex + 3] = ByteMax;
			}
		}

		using var lockedBitmap = bitmap.Lock();
		Marshal.Copy(pixels, 0, lockedBitmap.Address, pixels.Length);
		return bitmap;
	}

	private static WriteableBitmap GenerateSquareBitmap(double hue)
	{
		var size = WheelSize;
		var bitmap = new WriteableBitmap(
			new PixelSize(size, size),
			new Vector(BitmapDpi, BitmapDpi),
			Avalonia.Platform.PixelFormat.Bgra8888,
			Avalonia.Platform.AlphaFormat.Unpremul);
		var pixels = new byte[size * size * 4];

		for (var y = 0; y < size; y++)
		{
			var value = 1 - (y / (double)(size - 1));
			for (var x = 0; x < size; x++)
			{
				var saturation = x / (double)(size - 1);
				var pixelIndex = (y * size + x) * 4;
				var (red, green, blue) = HsvToRgb(hue, saturation, value);

				pixels[pixelIndex] = blue;
				pixels[pixelIndex + 1] = green;
				pixels[pixelIndex + 2] = red;
				pixels[pixelIndex + 3] = ByteMax;
			}
		}

		using var lockedBitmap = bitmap.Lock();
		Marshal.Copy(pixels, 0, lockedBitmap.Address, pixels.Length);
		return bitmap;
	}

	private static (byte Red, byte Green, byte Blue) HsvToRgb(double hue, double saturation, double value)
	{
		hue = hue % FullCircleDegrees;
		if (hue < 0) hue += FullCircleDegrees;

		var chroma = value * saturation;
		var intermediate = chroma * (1 - Math.Abs((hue / HueSegmentDegrees) % 2 - 1));
		var match = value - chroma;

		double red, green, blue;

		if (hue < HueSegmentDegrees) { red = chroma; green = intermediate; blue = 0; }
		else if (hue < HueSegmentDegrees * 2) { red = intermediate; green = chroma; blue = 0; }
		else if (hue < HueSegmentDegrees * 3) { red = 0; green = chroma; blue = intermediate; }
		else if (hue < HueSegmentDegrees * 4) { red = 0; green = intermediate; blue = chroma; }
		else if (hue < HueSegmentDegrees * 5) { red = intermediate; green = 0; blue = chroma; }
		else { red = chroma; green = 0; blue = intermediate; }

		return (
			(byte)Math.Round((red + match) * ByteMax),
			(byte)Math.Round((green + match) * ByteMax),
			(byte)Math.Round((blue + match) * ByteMax));
	}

	private static (double Hue, double Saturation, double Value) RgbToHsv(byte red, byte green, byte blue)
	{
		var redNormalized = red / 255.0;
		var greenNormalized = green / 255.0;
		var blueNormalized = blue / 255.0;

		var max = Math.Max(redNormalized, Math.Max(greenNormalized, blueNormalized));
		var min = Math.Min(redNormalized, Math.Min(greenNormalized, blueNormalized));
		var delta = max - min;

		double hue;
		if (delta < DeltaEpsilon) hue = 0;
		else if (max == redNormalized) hue = HueSegmentDegrees * (((greenNormalized - blueNormalized) / delta) % 6);
		else if (max == greenNormalized) hue = HueSegmentDegrees * (((blueNormalized - redNormalized) / delta) + 2);
		else hue = HueSegmentDegrees * (((redNormalized - greenNormalized) / delta) + 4);

		if (hue < 0) hue += FullCircleDegrees;

		var saturation = max <= 0 ? 0 : delta / max;
		var value = max;

		return (hue, saturation, value);
	}

	private static bool TryParseHex(string text, out byte alpha, out byte red, out byte green, out byte blue)
	{
		alpha = ByteMax;
		red = 0;
		green = 0;
		blue = 0;

		var hex = text.Trim().TrimStart('#');

		if (hex.Length == 6)
		{
			if (!byte.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out red)) return false;
			if (!byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out green)) return false;
			if (!byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out blue)) return false;
			return true;
		}

		if (hex.Length == 8)
		{
			if (!byte.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out alpha)) return false;
			if (!byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out red)) return false;
			if (!byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out green)) return false;
			if (!byte.TryParse(hex.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out blue)) return false;
			return true;
		}

		return false;
	}
}
