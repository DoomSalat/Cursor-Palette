using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace CursorPalette.Views;

public partial class PaintEditorWindow
{
	private void OnResizeStarted(object sender, DragStartedEventArgs e)
	{
		_resizeOriginalWidth = _canvasWidth;
		_resizeOriginalHeight = _canvasHeight;
		_resizeOriginalOffsetX = _offsetX;
		_resizeOriginalOffsetY = _offsetY;
		_resizeAccumX = 0;
		_resizeAccumY = 0;

		ShadowRect.Visibility = Visibility.Visible;
		ShadowRect.StrokeThickness = ShadowStrokePx / _zoom;
		Canvas.SetLeft(ShadowRect, 0);
		Canvas.SetTop(ShadowRect, 0);
		ShadowRect.Width = _canvasWidth;
		ShadowRect.Height = _canvasHeight;

		ResizeSizeLabel.Visibility = Visibility.Visible;
	}

	private void OnResizeCompleted(object sender, DragCompletedEventArgs e)
	{
		ShadowRect.Visibility = Visibility.Collapsed;
		ResizeSizeLabel.Visibility = Visibility.Collapsed;
	}

	private void UpdateResizeLabel()
	{
		var offset = 4 / _zoom;

		ResizeSizeLabel.FontSize = ResizeLabelFontSizePx / _zoom;
		Canvas.SetLeft(ResizeSizeLabel, _canvasWidth + offset);
		Canvas.SetTop(ResizeSizeLabel, _canvasHeight + offset);

		ResizeSizeLabel.Text = $"{_canvasWidth}x{_canvasHeight}";
	}

	private void OnThumbRightDrag(object sender, DragDeltaEventArgs e)
	{
		_resizeAccumX += e.HorizontalChange;
		var delta = (int)Math.Round(_resizeAccumX);
		_canvasWidth = Math.Clamp(_resizeOriginalWidth + delta, MinCanvasDimension, MaxCanvasDimension);

		ClampOffset();
		RenderAll();
		UpdateResizeLabel();
	}

	private void OnThumbBottomDrag(object sender, DragDeltaEventArgs e)
	{
		_resizeAccumY += e.VerticalChange;
		var delta = (int)Math.Round(_resizeAccumY);
		_canvasHeight = Math.Clamp(_resizeOriginalHeight + delta, MinCanvasDimension, MaxCanvasDimension);

		ClampOffset();
		RenderAll();
		UpdateResizeLabel();
	}

	private void OnThumbLeftDrag(object sender, DragDeltaEventArgs e)
	{
		_resizeAccumX += e.HorizontalChange;
		var delta = (int)Math.Round(_resizeAccumX);
		var newWidth = Math.Clamp(_resizeOriginalWidth - delta, MinCanvasDimension, MaxCanvasDimension);
		_offsetX = _resizeOriginalOffsetX + (newWidth - _resizeOriginalWidth);
		_canvasWidth = newWidth;

		ClampOffset();
		RenderAll();
		UpdateResizeLabel();
	}

	private void OnThumbTopDrag(object sender, DragDeltaEventArgs e)
	{
		_resizeAccumY += e.VerticalChange;
		var delta = (int)Math.Round(_resizeAccumY);
		var newHeight = Math.Clamp(_resizeOriginalHeight - delta, MinCanvasDimension, MaxCanvasDimension);
		_offsetY = _resizeOriginalOffsetY + (newHeight - _resizeOriginalHeight);
		_canvasHeight = newHeight;

		ClampOffset();
		RenderAll();
		UpdateResizeLabel();
	}

	private void OnThumbTopLeftDrag(object sender, DragDeltaEventArgs e)
	{
		_resizeAccumX += e.HorizontalChange;
		_resizeAccumY += e.VerticalChange;
		var dx = (int)Math.Round(_resizeAccumX);
		var dy = (int)Math.Round(_resizeAccumY);
		var newWidth = Math.Clamp(_resizeOriginalWidth - dx, MinCanvasDimension, MaxCanvasDimension);
		var newHeight = Math.Clamp(_resizeOriginalHeight - dy, MinCanvasDimension, MaxCanvasDimension);
		_offsetX = _resizeOriginalOffsetX + (newWidth - _resizeOriginalWidth);
		_offsetY = _resizeOriginalOffsetY + (newHeight - _resizeOriginalHeight);
		_canvasWidth = newWidth;
		_canvasHeight = newHeight;

		ClampOffset();
		RenderAll();
		UpdateResizeLabel();
	}

	private void OnThumbTopRightDrag(object sender, DragDeltaEventArgs e)
	{
		_resizeAccumX += e.HorizontalChange;
		_resizeAccumY += e.VerticalChange;
		var dx = (int)Math.Round(_resizeAccumX);
		var dy = (int)Math.Round(_resizeAccumY);
		var newWidth = Math.Clamp(_resizeOriginalWidth + dx, MinCanvasDimension, MaxCanvasDimension);
		var newHeight = Math.Clamp(_resizeOriginalHeight - dy, MinCanvasDimension, MaxCanvasDimension);
		_offsetY = _resizeOriginalOffsetY + (newHeight - _resizeOriginalHeight);
		_canvasWidth = newWidth;
		_canvasHeight = newHeight;

		ClampOffset();
		RenderAll();
		UpdateResizeLabel();
	}

	private void OnThumbBottomLeftDrag(object sender, DragDeltaEventArgs e)
	{
		_resizeAccumX += e.HorizontalChange;
		_resizeAccumY += e.VerticalChange;
		var dx = (int)Math.Round(_resizeAccumX);
		var dy = (int)Math.Round(_resizeAccumY);
		var newWidth = Math.Clamp(_resizeOriginalWidth - dx, MinCanvasDimension, MaxCanvasDimension);
		var newHeight = Math.Clamp(_resizeOriginalHeight + dy, MinCanvasDimension, MaxCanvasDimension);
		_offsetX = _resizeOriginalOffsetX + (newWidth - _resizeOriginalWidth);
		_canvasWidth = newWidth;
		_canvasHeight = newHeight;

		ClampOffset();
		RenderAll();
		UpdateResizeLabel();
	}

	private void OnThumbBottomRightDrag(object sender, DragDeltaEventArgs e)
	{
		_resizeAccumX += e.HorizontalChange;
		_resizeAccumY += e.VerticalChange;
		var dx = (int)Math.Round(_resizeAccumX);
		var dy = (int)Math.Round(_resizeAccumY);
		_canvasWidth = Math.Clamp(_resizeOriginalWidth + dx, MinCanvasDimension, MaxCanvasDimension);
		_canvasHeight = Math.Clamp(_resizeOriginalHeight + dy, MinCanvasDimension, MaxCanvasDimension);

		ClampOffset();
		RenderAll();
		UpdateResizeLabel();
	}
}
