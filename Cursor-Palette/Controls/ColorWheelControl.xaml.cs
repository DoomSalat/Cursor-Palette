using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CursorPalette.Controls;

public partial class ColorWheelControl : UserControl
{
	private const int WheelSize = 140;

	private enum PickerMode
	{
		Wheel,
		Square
	}

	private double _hue;
	private double _saturation = 1.0;
	private double _value = 1.0;
	private double _alphaPercent = 100;

	private bool _isDraggingWheel;
	private bool _isDraggingSquare;
	private bool _initialized;
	private PickerMode _mode = PickerMode.Wheel;

	public event EventHandler? ColorChanged;

	public Color SelectedColor
	{
		get
		{
			var (red, green, blue) = HsvToRgb(_hue, _saturation, _value);
			var alpha = (byte)Math.Round(_alphaPercent * 2.55);

			return Color.FromArgb(alpha, red, green, blue);
		}
	}

	public (double Hue, double Saturation, double Value, double Alpha) GetHsv() =>
		(_hue, _saturation, _value, _alphaPercent);

	public void SetColor(double hue, double saturation, double value, double alphaPercent)
	{
		_hue = hue;
		_saturation = saturation;
		_value = value;
		_alphaPercent = alphaPercent;

		BrightnessSlider.Value = _value;
		HueSlider.Value = _hue;
		AlphaSlider.Value = _alphaPercent;

		UpdateIndicator();
		UpdateSquareIndicator();
		if (_mode == PickerMode.Square)
			SquareImage.Source = GenerateSquareBitmap(_hue);
		UpdatePreview();
		UpdateAlphaPercent();
	}

	public ColorWheelControl()
	{
		InitializeComponent();

		WheelImage.Source = GenerateWheelBitmap();

		UpdateIndicator();
		UpdateSquareIndicator();
		UpdatePreview();

		_initialized = true;
	}

	private void OnHexTextKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key != Key.Enter)
			return;

		ApplyHexInput(HexText.Text);
		Keyboard.ClearFocus();
		e.Handled = true;
	}

	private void OnHexTextLostFocus(object sender, RoutedEventArgs e) => ApplyHexInput(HexText.Text);

	private void ApplyHexInput(string text)
	{
		if (!TryParseHex(text, out var alpha, out var red, out var green, out var blue))
		{
			UpdatePreview();
			return;
		}

		var (hue, saturation, value) = RgbToHsv(red, green, blue);

		_hue = hue;
		_saturation = saturation;
		_value = value;
		_alphaPercent = alpha / 2.55;

		BrightnessSlider.Value = _value;
		HueSlider.Value = _hue;
		AlphaSlider.Value = _alphaPercent;

		UpdateIndicator();
		UpdateSquareIndicator();
		if (_mode == PickerMode.Square)
			SquareImage.Source = GenerateSquareBitmap(_hue);
		UpdatePreview();
		UpdateAlphaPercent();

		ColorChanged?.Invoke(this, EventArgs.Empty);
	}

	private static bool TryParseHex(string text, out byte alpha, out byte red, out byte green, out byte blue)
	{
		alpha = 255;
		red = 0;
		green = 0;
		blue = 0;

		var hex = text.Trim().TrimStart('#');

		if (hex.Length == 6)
		{
			if (!byte.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out red)) return false;
			if (!byte.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out green)) return false;
			if (!byte.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out blue)) return false;
			return true;
		}

		if (hex.Length == 8)
		{
			if (!byte.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out alpha)) return false;
			if (!byte.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out red)) return false;
			if (!byte.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out green)) return false;
			if (!byte.TryParse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber, null, out blue)) return false;
			return true;
		}

		return false;
	}

	private static (double Hue, double Saturation, double Value) RgbToHsv(byte red, byte green, byte blue)
	{
		var r = red / 255.0;
		var g = green / 255.0;
		var b = blue / 255.0;

		var max = Math.Max(r, Math.Max(g, b));
		var min = Math.Min(r, Math.Min(g, b));
		var delta = max - min;

		double hue;
		if (delta < 0.00001) hue = 0;
		else if (max == r) hue = 60 * (((g - b) / delta) % 6);
		else if (max == g) hue = 60 * (((b - r) / delta) + 2);
		else hue = 60 * (((r - g) / delta) + 4);

		if (hue < 0) hue += 360;

		var saturation = max <= 0 ? 0 : delta / max;
		var value = max;

		return (hue, saturation, value);
	}

	public string GetColorMode() => _mode == PickerMode.Square ? "Square" : "Wheel";

	public void SetColorMode(string mode) =>
		SetMode(string.Equals(mode, "Square", StringComparison.OrdinalIgnoreCase) ? PickerMode.Square : PickerMode.Wheel);

	private void OnWheelModeClick(object sender, MouseButtonEventArgs e) => SetMode(PickerMode.Wheel);

	private void OnSquareModeClick(object sender, MouseButtonEventArgs e) => SetMode(PickerMode.Square);

	private void SetMode(PickerMode mode)
	{
		if (_mode == mode)
			return;

		_mode = mode;

		var isSquare = mode == PickerMode.Square;

		WheelImage.Visibility = isSquare ? Visibility.Collapsed : Visibility.Visible;
		BrightnessOverlay.Visibility = isSquare ? Visibility.Collapsed : Visibility.Visible;
		Indicator.Visibility = isSquare ? Visibility.Collapsed : Visibility.Visible;
		SquareImage.Visibility = isSquare ? Visibility.Visible : Visibility.Collapsed;
		SquareIndicatorLayer.Visibility = isSquare ? Visibility.Visible : Visibility.Collapsed;
		HueRow.Visibility = isSquare ? Visibility.Visible : Visibility.Collapsed;
		BrightnessRow.Visibility = isSquare ? Visibility.Collapsed : Visibility.Visible;

		WheelModeButton.Background = isSquare
			? (Brush)FindResource("Brush.Surface")
			: (Brush)FindResource("Brush.Accent");
		SquareModeButton.Background = isSquare
			? (Brush)FindResource("Brush.Accent")
			: (Brush)FindResource("Brush.Surface");

		if (isSquare)
		{
			SquareImage.Source = GenerateSquareBitmap(_hue);
			UpdateSquareIndicator();
		}
	}

	private static WriteableBitmap GenerateWheelBitmap()
	{
		var size = WheelSize;
		var bitmap = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
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

				var hue = (Math.Atan2(deltaY, deltaX) * 180.0 / Math.PI + 360) % 360;
				var saturation = Math.Min(1, distance / center);
				var (red, green, blue) = HsvToRgb(hue, saturation, 1.0);

				pixels[pixelIndex] = blue;
				pixels[pixelIndex + 1] = green;
				pixels[pixelIndex + 2] = red;
				pixels[pixelIndex + 3] = 255;
			}
		}

		bitmap.WritePixels(new Int32Rect(0, 0, size, size), pixels, size * 4, 0);

		return bitmap;
	}

	private static WriteableBitmap GenerateSquareBitmap(double hue)
	{
		var size = WheelSize;
		var bitmap = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
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
				pixels[pixelIndex + 3] = 255;
			}
		}

		bitmap.WritePixels(new Int32Rect(0, 0, size, size), pixels, size * 4, 0);

		return bitmap;
	}

	private void OnSquareMouseDown(object sender, MouseButtonEventArgs e)
	{
		_isDraggingSquare = true;
		SquareImage.CaptureMouse();
		UpdateFromSquareMouse(e.GetPosition(SquareImage));
		e.Handled = true;
	}

	private void OnSquareMouseMove(object sender, MouseEventArgs e)
	{
		if (!_isDraggingSquare)
			return;

		UpdateFromSquareMouse(e.GetPosition(SquareImage));

		e.Handled = true;
	}

	private void OnSquareMouseUp(object sender, MouseButtonEventArgs e)
	{
		_isDraggingSquare = false;
		SquareImage.ReleaseMouseCapture();
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

	private void UpdateSquareIndicator()
	{
		var indicatorX = _saturation * WheelSize - 5;
		var indicatorY = (1 - _value) * WheelSize - 5;

		Canvas.SetLeft(SquareIndicator, indicatorX);
		Canvas.SetTop(SquareIndicator, indicatorY);
	}

	private void OnHueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		_hue = e.NewValue;

		if (_mode == PickerMode.Square)
			SquareImage.Source = GenerateSquareBitmap(_hue);

		if (!_initialized)
			return;

		UpdatePreview();

		ColorChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OnWheelMouseDown(object sender, MouseButtonEventArgs e)
	{
		_isDraggingWheel = true;
		WheelImage.CaptureMouse();
		UpdateFromMouse(e.GetPosition(WheelImage));
		e.Handled = true;
	}

	private void OnWheelMouseMove(object sender, MouseEventArgs e)
	{
		if (!_isDraggingWheel)
			return;

		UpdateFromMouse(e.GetPosition(WheelImage));

		e.Handled = true;
	}

	private void OnWheelMouseUp(object sender, MouseButtonEventArgs e)
	{
		_isDraggingWheel = false;
		WheelImage.ReleaseMouseCapture();
		e.Handled = true;
	}

	private void UpdateFromMouse(Point position)
	{
		var center = WheelSize / 2.0;
		var deltaX = position.X - center;
		var deltaY = position.Y - center;
		var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

		_hue = (Math.Atan2(deltaY, deltaX) * 180.0 / Math.PI + 360) % 360;
		_saturation = Math.Min(1, distance / center);

		UpdateIndicator();
		UpdatePreview();

		ColorChanged?.Invoke(this, EventArgs.Empty);
	}

	private void UpdateIndicator()
	{
		var center = WheelSize / 2.0;
		var angle = _hue * Math.PI / 180.0;
		var radius = _saturation * center;
		var indicatorX = center + radius * Math.Cos(angle) - 5;
		var indicatorY = center + radius * Math.Sin(angle) - 5;

		Canvas.SetLeft(Indicator, indicatorX);
		Canvas.SetTop(Indicator, indicatorY);
	}

	private void UpdatePreview()
	{
		var (red, green, blue) = HsvToRgb(_hue, _saturation, _value);
		var alpha = (byte)Math.Round(_alphaPercent * 2.55);
		var color = Color.FromArgb(alpha, red, green, blue);
		ColorPreview.Fill = new SolidColorBrush(color);
		HexText.Text = $"#{alpha:X2}{red:X2}{green:X2}{blue:X2}";
	}

	private void UpdateAlphaPercent()
	{
		AlphaPercentText.Text = $"{(int)Math.Round(_alphaPercent)}%";
	}

	private void OnBrightnessChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		_value = e.NewValue;

		BrightnessOverlay.Opacity = 1 - _value;

		if (!_initialized)
			return;

		UpdatePreview();

		ColorChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OnAlphaChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		_alphaPercent = e.NewValue;

		if (!_initialized)
			return;

		UpdateAlphaPercent();
		UpdatePreview();

		ColorChanged?.Invoke(this, EventArgs.Empty);
	}

	private static (byte Red, byte Green, byte Blue) HsvToRgb(double hue, double saturation, double value)
	{
		hue = hue % 360;
		if (hue < 0) hue += 360;

		var chroma = value * saturation;
		var intermediate = chroma * (1 - Math.Abs((hue / 60) % 2 - 1));
		var match = value - chroma;

		double red, green, blue;

		if (hue < 60) { red = chroma; green = intermediate; blue = 0; }
		else if (hue < 120) { red = intermediate; green = chroma; blue = 0; }
		else if (hue < 180) { red = 0; green = chroma; blue = intermediate; }
		else if (hue < 240) { red = 0; green = intermediate; blue = chroma; }
		else if (hue < 300) { red = intermediate; green = 0; blue = chroma; }
		else { red = chroma; green = 0; blue = intermediate; }

		return (
			(byte)Math.Round((red + match) * 255),
			(byte)Math.Round((green + match) * 255),
			(byte)Math.Round((blue + match) * 255));
	}
}
