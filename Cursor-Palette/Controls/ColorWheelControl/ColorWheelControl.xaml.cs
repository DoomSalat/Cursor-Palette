using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CursorPalette.Services;

namespace CursorPalette.Controls;

public partial class ColorWheelControl : UserControl
{
	private const int WheelSize = 140;
	private const int BitmapDpi = 96;
	private const int ByteMax = 255;
	private const double AlphaToPercentFactor = 255.0 / 100.0;
	private const double FullCircleDegrees = 360;
	private const double HueSegmentDegrees = 60;
	private const double DeltaEpsilon = 0.00001;
	private const double IndicatorHalfSize = 5;
	private const string BrushAccent = "Brush.Accent";
	private const string BrushSurface = "Brush.Surface";

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

	public void SetColorFromRgb(byte red, byte green, byte blue) => ApplyRgb(ByteMax, red, green, blue);

	private void ApplyRgb(byte alpha, byte red, byte green, byte blue)
	{
		var (hue, saturation, value) = RgbToHsv(red, green, blue);

		_hue = hue;
		_saturation = saturation;
		_value = value;
		_alphaPercent = alpha / AlphaToPercentFactor;

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

		ApplyRgb(alpha, red, green, blue);
	}

	public void SetEyedropperActive(bool active) =>
		EyedropperButton.Background = active
			? (Brush)FindResource(BrushAccent)
			: (Brush)FindResource(BrushSurface);

	public string GetColorMode() => _mode == PickerMode.Square ? AppState.PaintEditorColorModeSquare : AppState.PaintEditorColorModeWheel;

	public void SetColorMode(string mode) =>
		SetMode(string.Equals(mode, AppState.PaintEditorColorModeSquare, StringComparison.OrdinalIgnoreCase) ? PickerMode.Square : PickerMode.Wheel);

	private static bool TryParseHex(string text, out byte alpha, out byte red, out byte green, out byte blue)
	{
		alpha = ByteMax;
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
}
