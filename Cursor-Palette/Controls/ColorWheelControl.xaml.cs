using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CursorPalette.Controls;

public partial class ColorWheelControl : UserControl
{
	private const int WheelSize = 140;

	private double _hue;
	private double _saturation = 1.0;
	private double _value = 1.0;
	private double _alphaPercent = 100;

	private bool _isDraggingWheel;
	private bool _initialized;

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
		AlphaSlider.Value = _alphaPercent;

		UpdateIndicator();
		UpdatePreview();
		UpdateAlphaPercent();
	}

	public ColorWheelControl()
	{
		InitializeComponent();

		WheelImage.Source = GenerateWheelBitmap();

		UpdateIndicator();
		UpdatePreview();

		_initialized = true;
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
