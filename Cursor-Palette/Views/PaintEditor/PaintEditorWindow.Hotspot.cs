using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class PaintEditorWindow
{
	private const double HotspotMarkerScreenPx = 12;
	private const double HotspotGlowScreenPx = 20;
	private const double HotspotMarkerStrokeScreenPx = 2;
	private const string HotspotCoordsFormat = "X: {0}   Y: {1}";

	private bool _isDraggingHotspot;

	private int HotspotAbsoluteX => _offsetX + _hotspotOffsetX;
	private int HotspotAbsoluteY => _offsetY + _hotspotOffsetY;

	private void UpdateHotspotMarker()
	{
		if (HotspotMarker.Visibility != Visibility.Visible)
			return;

		var markerSize = HotspotMarkerScreenPx / _zoom;
		var glowSize = HotspotGlowScreenPx / _zoom;
		var strokeThickness = HotspotMarkerStrokeScreenPx / _zoom;

		HotspotMarker.Width = markerSize;
		HotspotMarker.Height = markerSize;
		HotspotMarker.StrokeThickness = strokeThickness;

		HotspotMarkerGlow.Width = glowSize;
		HotspotMarkerGlow.Height = glowSize;

		var centerX = HotspotAbsoluteX + 0.5;
		var centerY = HotspotAbsoluteY + 0.5;

		Canvas.SetLeft(HotspotMarker, centerX - markerSize / 2);
		Canvas.SetTop(HotspotMarker, centerY - markerSize / 2);
		Canvas.SetLeft(HotspotMarkerGlow, centerX - glowSize / 2);
		Canvas.SetTop(HotspotMarkerGlow, centerY - glowSize / 2);
	}

	private void UpdateHotspotCoords() =>
		HotspotCoordsText.Text = string.Format(HotspotCoordsFormat, HotspotAbsoluteX, HotspotAbsoluteY);

	private void UpdateHotspotPresetHighlight()
	{
		foreach (var child in HotspotPresetGrid.Children)
		{
			if (child is not Button button)
				continue;

			var (fractionX, fractionY) = ParseFraction(button);
			var targetX = Math.Clamp((int)Math.Round(fractionX * (_canvasWidth - 1)), 0, _canvasWidth - 1);
			var targetY = Math.Clamp((int)Math.Round(fractionY * (_canvasHeight - 1)), 0, _canvasHeight - 1);
			var isCurrent = targetX == HotspotAbsoluteX && targetY == HotspotAbsoluteY;

			button.Style = (Style)Application.Current.Resources[isCurrent ? StyleAccentButton : StyleButton];
		}
	}

	private void SetHotspotFromCanvasPosition(Point position)
	{
		var canvasX = Math.Clamp((int)Math.Floor(position.X), 0, _canvasWidth - 1);
		var canvasY = Math.Clamp((int)Math.Floor(position.Y), 0, _canvasHeight - 1);

		_hotspotOffsetX = canvasX - _offsetX;
		_hotspotOffsetY = canvasY - _offsetY;

		UpdateHotspotMarker();
		UpdateHotspotCoords();
		UpdateHotspotPresetHighlight();
	}

	private void OnHotspotPresetClick(object sender, RoutedEventArgs e)
	{
		var (fractionX, fractionY) = ParseFraction((Button)sender);

		var canvasX = Math.Clamp((int)Math.Round(fractionX * (_canvasWidth - 1)), 0, _canvasWidth - 1);
		var canvasY = Math.Clamp((int)Math.Round(fractionY * (_canvasHeight - 1)), 0, _canvasHeight - 1);

		_hotspotOffsetX = canvasX - _offsetX;
		_hotspotOffsetY = canvasY - _offsetY;

		UpdateHotspotMarker();
		UpdateHotspotCoords();
		UpdateHotspotPresetHighlight();
	}
}
