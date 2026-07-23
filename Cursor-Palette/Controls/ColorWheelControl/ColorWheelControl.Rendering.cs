using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CursorPalette.Controls;

public partial class ColorWheelControl : UserControl
{
	private static WriteableBitmap GenerateWheelBitmap()
	{
		var size = WheelSize;
		var bitmap = new WriteableBitmap(size, size, BitmapDpi, BitmapDpi, PixelFormats.Bgra32, null);
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

		bitmap.WritePixels(new Int32Rect(0, 0, size, size), pixels, size * 4, 0);

		return bitmap;
	}

	private static WriteableBitmap GenerateSquareBitmap(double hue)
	{
		var size = WheelSize;
		var bitmap = new WriteableBitmap(size, size, BitmapDpi, BitmapDpi, PixelFormats.Bgra32, null);
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

		bitmap.WritePixels(new Int32Rect(0, 0, size, size), pixels, size * 4, 0);

		return bitmap;
	}

	private void UpdateIndicator()
	{
		var center = WheelSize / 2.0;
		var angle = _hue * Math.PI / 180.0;
		var radius = _saturation * center;
		var indicatorX = center + radius * Math.Cos(angle) - IndicatorHalfSize;
		var indicatorY = center + radius * Math.Sin(angle) - IndicatorHalfSize;

		Canvas.SetLeft(Indicator, indicatorX);
		Canvas.SetTop(Indicator, indicatorY);
	}

	private void UpdateSquareIndicator()
	{
		var indicatorX = _saturation * WheelSize - IndicatorHalfSize;
		var indicatorY = (1 - _value) * WheelSize - IndicatorHalfSize;

		Canvas.SetLeft(SquareIndicator, indicatorX);
		Canvas.SetTop(SquareIndicator, indicatorY);
	}

	private void UpdatePreview()
	{
		var (red, green, blue) = HsvToRgb(_hue, _saturation, _value);
		var alpha = (byte)Math.Round(_alphaPercent * AlphaToPercentFactor);
		var color = Color.FromArgb(alpha, red, green, blue);
		ColorPreview.Fill = new SolidColorBrush(color);
		HexText.Text = $"#{alpha:X2}{red:X2}{green:X2}{blue:X2}";
	}

	private void UpdateAlphaPercent()
	{
		AlphaPercentText.Text = $"{(int)Math.Round(_alphaPercent)}%";
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
		var r = red / 255.0;
		var g = green / 255.0;
		var b = blue / 255.0;

		var max = Math.Max(r, Math.Max(g, b));
		var min = Math.Min(r, Math.Min(g, b));
		var delta = max - min;

		double hue;
		if (delta < DeltaEpsilon) hue = 0;
		else if (max == r) hue = HueSegmentDegrees * (((g - b) / delta) % 6);
		else if (max == g) hue = HueSegmentDegrees * (((b - r) / delta) + 2);
		else hue = HueSegmentDegrees * (((r - g) / delta) + 4);

		if (hue < 0) hue += FullCircleDegrees;

		var saturation = max <= 0 ? 0 : delta / max;
		var value = max;

		return (hue, saturation, value);
	}
}
