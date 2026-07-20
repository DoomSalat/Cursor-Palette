using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class HotspotEditorWindow : Window
{
	private const double MaxDisplaySize = 320;
	private const double MaxScaleFactor = 10;
	private const string CoordsFormat = "X: {0}   Y: {1}";

	private readonly int _nativeWidth;
	private readonly int _nativeHeight;
	private readonly double _scale;
	private int _x;
	private int _y;
	private bool _dragging;

	public int ResultX { get; private set; }
	public int ResultY { get; private set; }

	public HotspotEditorWindow(string filePath, CursorHotspot hotspot)
	{
		InitializeComponent();

		var uiScale = AppState.GetUiScale();
		UiScaleTransform.ScaleX = uiScale;
		UiScaleTransform.ScaleY = uiScale;

		_nativeWidth = hotspot.Width;
		_nativeHeight = hotspot.Height;
		_x = Math.Clamp(hotspot.X, 0, _nativeWidth - 1);
		_y = Math.Clamp(hotspot.Y, 0, _nativeHeight - 1);
		_scale = ComputeScale(_nativeWidth, _nativeHeight);

		var displayWidth = _nativeWidth * _scale;
		var displayHeight = _nativeHeight * _scale;

		PreviewCanvas.Width = displayWidth;
		PreviewCanvas.Height = displayHeight;
		PreviewImage.Width = displayWidth;
		PreviewImage.Height = displayHeight;
		RenderOptions.SetBitmapScalingMode(PreviewImage, BitmapScalingMode.NearestNeighbor);
		CursorPreviewService.ApplyPreview(PreviewImage, filePath);

		RefreshMarker();
	}

	private static double ComputeScale(int width, int height)
	{
		var factor = Math.Min(MaxScaleFactor, Math.Min(MaxDisplaySize / width, MaxDisplaySize / height));
		return Math.Max(1, factor);
	}

	private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
	{
		_dragging = true;
		PreviewCanvas.CaptureMouse();
		SetHotspotFromPointer(e.GetPosition(PreviewCanvas));
	}

	private void OnCanvasMouseMove(object sender, MouseEventArgs e)
	{
		if (_dragging)
			SetHotspotFromPointer(e.GetPosition(PreviewCanvas));
	}

	private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
	{
		_dragging = false;
		PreviewCanvas.ReleaseMouseCapture();
	}

	private void SetHotspotFromPointer(Point position)
	{
		_x = Math.Clamp((int)Math.Round(position.X / _scale), 0, _nativeWidth - 1);
		_y = Math.Clamp((int)Math.Round(position.Y / _scale), 0, _nativeHeight - 1);

		RefreshMarker();
	}

	private void OnPresetPositionClick(object sender, RoutedEventArgs e)
	{
		var (fractionX, fractionY) = ParseFraction((Button)sender);

		_x = PixelForFraction(fractionX, _nativeWidth);
		_y = PixelForFraction(fractionY, _nativeHeight);

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

		Canvas.SetLeft(Marker, displayX - Marker.Width / 2);
		Canvas.SetTop(Marker, displayY - Marker.Height / 2);
		Canvas.SetLeft(MarkerGlow, displayX - MarkerGlow.Width / 2);
		Canvas.SetTop(MarkerGlow, displayY - MarkerGlow.Height / 2);
	}

	private void UpdateCoordsText() =>
		CoordsText.Text = string.Format(CoordsFormat, _x, _y);

	private void UpdatePresetHighlight()
	{
		foreach (var child in PresetGrid.Children)
		{
			if (child is not Button button)
				continue;

			var (fractionX, fractionY) = ParseFraction(button);
			var isCurrent = PixelForFraction(fractionX, _nativeWidth) == _x &&
				PixelForFraction(fractionY, _nativeHeight) == _y;

			button.Style = (Style)Application.Current.Resources[isCurrent ? "Style.AccentButton" : "Style.Button"];
		}
	}

	private static (double X, double Y) ParseFraction(Button presetButton)
	{
		var parts = ((string)presetButton.Tag).Split(',');
		return (
			double.Parse(parts[0], CultureInfo.InvariantCulture),
			double.Parse(parts[1], CultureInfo.InvariantCulture));
	}

	private static int PixelForFraction(double fraction, int nativeSize) =>
		Math.Clamp((int)Math.Round(fraction * (nativeSize - 1)), 0, nativeSize - 1);

	private void OnSaveClick(object sender, RoutedEventArgs e)
	{
		ResultX = _x;
		ResultY = _y;
		DialogResult = true;
	}
}
