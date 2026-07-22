using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CursorPalette.Views;

public partial class PaintEditorWindow
{
	private void RenderAll()
	{
		CanvasZoomTransform.ScaleX = _zoom;
		CanvasZoomTransform.ScaleY = _zoom;

		ViewportContent.Width = _canvasWidth * _zoom;
		ViewportContent.Height = _canvasHeight * _zoom;

		Canvas.SetLeft(CanvasBgRect, 0);
		Canvas.SetTop(CanvasBgRect, 0);
		CanvasBgRect.Width = _canvasWidth;
		CanvasBgRect.Height = _canvasHeight;

		Canvas.SetLeft(PreviewImage, 0);
		Canvas.SetTop(PreviewImage, 0);
		PreviewImage.Width = _canvasWidth;
		PreviewImage.Height = _canvasHeight;
		RenderOptions.SetBitmapScalingMode(PreviewImage, BitmapScalingMode.NearestNeighbor);

		var bitmap = new WriteableBitmap(_canvasWidth, _canvasHeight, 96, 96, PixelFormats.Bgra32, null);
		var pixels = Compose();
		bitmap.WritePixels(new Int32Rect(0, 0, _canvasWidth, _canvasHeight), pixels, _canvasWidth * BytesPerPixel, 0);
		PreviewImage.Source = bitmap;

		UpdateResizeOverlay();
		UpdateSpriteBoundsRect();
		UpdateCoordsText();
		UpdateMoveButtonsEnabled();
		UpdateSnapHighlight();
		UpdateZoomText();
		UpdateCanvasSizeLabel();
	}

	private void UpdateCoordsText() =>
		CoordsText.Text = string.Format(CoordsFormat, _canvasWidth, _canvasHeight, _offsetX, _offsetY);

	private void UpdateMoveButtonsEnabled()
	{
		var (minX, maxX) = HorizontalRange();
		var (minY, maxY) = VerticalRange();

		MoveLeftButton.IsEnabled = _offsetX > minX;
		MoveRightButton.IsEnabled = _offsetX < maxX;
		MoveUpButton.IsEnabled = _offsetY > minY;
		MoveDownButton.IsEnabled = _offsetY < maxY;
	}

	private void UpdateSnapHighlight()
	{
		var (minX, maxX) = HorizontalRange();
		var (minY, maxY) = VerticalRange();

		foreach (var child in SnapGrid.Children)
		{
			if (child is not Button button)
				continue;

			var (fractionX, fractionY) = ParseFraction(button);
			var targetX = SnapOffset(fractionX, minX, maxX);
			var targetY = SnapOffset(fractionY, minY, maxY);
			var isCurrent = targetX == _offsetX && targetY == _offsetY;

			button.Style = (Style)Application.Current.Resources[isCurrent ? StyleAccentButton : StyleButton];
		}
	}

	private static int SnapOffset(double fraction, int min, int max) =>
		fraction == 0 ? min : fraction == 1 ? max : (min + max) / 2;

	private static (double X, double Y) ParseFraction(Button button)
	{
		var parts = ((string)button.Tag).Split(',');

		return (
			double.Parse(parts[0], CultureInfo.InvariantCulture),
			double.Parse(parts[1], CultureInfo.InvariantCulture));
	}

	private void UpdateZoomText() =>
		CanvasZoomText.Text = $"{_zoom:0.#}x";

	private void UpdateCanvasSizeLabel() =>
		CanvasSizeLabel.Text = $"{_canvasWidth}x{_canvasHeight}";

	private void UpdateSpriteBoundsRect()
	{
		SpriteBoundsRect.StrokeThickness = SpriteBoundsStrokePx / _zoom;

		if (SpriteBoundsRect.Visibility != Visibility.Visible)
			return;

		Canvas.SetLeft(SpriteBoundsRect, _offsetX);
		Canvas.SetTop(SpriteBoundsRect, _offsetY);
		SpriteBoundsRect.Width = _spriteWidth;
		SpriteBoundsRect.Height = _spriteHeight;
	}

	// Overlay chrome (border, thumbs, label) is sized in screen pixels regardless of canvas
	// zoom, so it stays usable on small, heavily zoomed-in canvases instead of ballooning.
	private void UpdateResizeOverlay()
	{
		var inv = 1.0 / _zoom;

		Canvas.SetLeft(CanvasBorderRect, 0);
		Canvas.SetTop(CanvasBorderRect, 0);
		CanvasBorderRect.Width = _canvasWidth;
		CanvasBorderRect.Height = _canvasHeight;
		CanvasBorderRect.StrokeThickness = BorderStrokePx * inv;

		var edgeLength = ThumbEdgeLengthPx * inv;
		var edgeThickness = ThumbEdgeThicknessPx * inv;
		var cornerSize = ThumbCornerSizePx * inv;
		var edgeThicknessHalf = edgeThickness / 2.0;
		var cornerHalf = cornerSize / 2.0;

		ThumbTop.Width = edgeLength;
		ThumbTop.Height = edgeThickness;
		ThumbBottom.Width = edgeLength;
		ThumbBottom.Height = edgeThickness;
		ThumbLeft.Width = edgeThickness;
		ThumbLeft.Height = edgeLength;
		ThumbRight.Width = edgeThickness;
		ThumbRight.Height = edgeLength;
		ThumbTopLeft.Width = cornerSize;
		ThumbTopLeft.Height = cornerSize;
		ThumbTopRight.Width = cornerSize;
		ThumbTopRight.Height = cornerSize;
		ThumbBottomLeft.Width = cornerSize;
		ThumbBottomLeft.Height = cornerSize;
		ThumbBottomRight.Width = cornerSize;
		ThumbBottomRight.Height = cornerSize;

		Canvas.SetLeft(ThumbTop, _canvasWidth / 2.0 - edgeLength / 2.0);
		Canvas.SetTop(ThumbTop, -edgeThicknessHalf);
		Canvas.SetLeft(ThumbBottom, _canvasWidth / 2.0 - edgeLength / 2.0);
		Canvas.SetTop(ThumbBottom, _canvasHeight - edgeThicknessHalf);
		Canvas.SetLeft(ThumbLeft, -edgeThicknessHalf);
		Canvas.SetTop(ThumbLeft, _canvasHeight / 2.0 - edgeLength / 2.0);
		Canvas.SetLeft(ThumbRight, _canvasWidth - edgeThicknessHalf);
		Canvas.SetTop(ThumbRight, _canvasHeight / 2.0 - edgeLength / 2.0);

		Canvas.SetLeft(ThumbTopLeft, -cornerHalf);
		Canvas.SetTop(ThumbTopLeft, -cornerHalf);
		Canvas.SetLeft(ThumbTopRight, _canvasWidth - cornerHalf);
		Canvas.SetTop(ThumbTopRight, -cornerHalf);
		Canvas.SetLeft(ThumbBottomLeft, -cornerHalf);
		Canvas.SetTop(ThumbBottomLeft, _canvasHeight - cornerHalf);
		Canvas.SetLeft(ThumbBottomRight, _canvasWidth - cornerHalf);
		Canvas.SetTop(ThumbBottomRight, _canvasHeight - cornerHalf);

		ResizeSizeLabel.FontSize = ResizeLabelFontSizePx * inv;
	}
}
