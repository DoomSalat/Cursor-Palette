using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class PaintEditorWindow
{
	private void OnCanvasZoomInClick(object sender, RoutedEventArgs e) =>
		ZoomAtPoint(CanvasZoomStep, ViewportCenter());

	private void OnCanvasZoomOutClick(object sender, RoutedEventArgs e) =>
		ZoomAtPoint(1 / CanvasZoomStep, ViewportCenter());

	private Point ViewportCenter() => new(ViewportHost.ActualWidth / 2.0, ViewportHost.ActualHeight / 2.0);

	private void ZoomAtPoint(double factor, Point anchor)
	{
		var newZoom = Math.Clamp(_zoom * factor, AppState.PaintEditorZoomMin, AppState.PaintEditorZoomMax);

		if (newZoom == _zoom)
			return;

		var canvasX = (anchor.X - CanvasPanTransform.X) / _zoom;
		var canvasY = (anchor.Y - CanvasPanTransform.Y) / _zoom;

		_zoom = newZoom;
		AppState.SetPaintEditorZoom(_zoom);

		CanvasPanTransform.X = anchor.X - canvasX * _zoom;
		CanvasPanTransform.Y = anchor.Y - canvasY * _zoom;

		RenderAll();
	}

	private void OnViewportMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
		{
			var factor = e.Delta > 0 ? CanvasZoomStep : 1 / CanvasZoomStep;
			ZoomAtPoint(factor, e.GetPosition(ViewportHost));
			e.Handled = true;
		}
	}

	private void OnViewportPreviewMouseDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ChangedButton == MouseButton.Left && IsPaintTool)
		{
			PaintBegin(GetCanvasPosition(e));
			ViewportHost.CaptureMouse();
			e.Handled = true;

			return;
		}

		var isHandDrag = e.ChangedButton == MouseButton.Left && _currentTool == AppState.PaintEditorToolHand;

		if (e.ChangedButton != MouseButton.Middle && !isHandDrag)
			return;

		_isPanning = true;
		_panStartPosition = e.GetPosition(ViewportHost);
		_panStartHorizontalOffset = CanvasPanTransform.X;
		_panStartVerticalOffset = CanvasPanTransform.Y;

		ViewportHost.CaptureMouse();
		e.Handled = true;
	}

	private void OnViewportPreviewMouseUp(object sender, MouseButtonEventArgs e)
	{
		if (_isPainting)
		{
			PaintEnd();
			ViewportHost.ReleaseMouseCapture();
			e.Handled = true;

			return;
		}

		var isHandDrag = e.ChangedButton == MouseButton.Left && _currentTool == AppState.PaintEditorToolHand;

		if ((e.ChangedButton != MouseButton.Middle && !isHandDrag) || !_isPanning)
			return;

		_isPanning = false;
		ViewportHost.ReleaseMouseCapture();
	}

	private void OnViewportPreviewMouseMove(object sender, MouseEventArgs e)
	{
		if (_isPainting)
		{
			var paintPosition = GetCanvasPosition(e);
			PaintStrokeTo(paintPosition);
			UpdatePaintCursor(paintPosition);
			e.Handled = true;

			return;
		}

		if (IsPaintTool)
		{
			UpdatePaintCursor(GetCanvasPosition(e));
		}

		if (!_isPanning)
			return;

		var panPosition = e.GetPosition(ViewportHost);

		CanvasPanTransform.X = _panStartHorizontalOffset + (panPosition.X - _panStartPosition.X);
		CanvasPanTransform.Y = _panStartVerticalOffset + (panPosition.Y - _panStartPosition.Y);
	}

	private Point GetCanvasPosition(MouseEventArgs e)
	{
		return e.GetPosition(ViewportContent);
	}

	private void OnViewportMouseLeave(object sender, MouseEventArgs e)
	{
		PaintCursorRect.Visibility = Visibility.Collapsed;
	}

	private void UpdatePaintCursor(Point canvasPosition)
	{
		var pixelX = (int)Math.Floor(canvasPosition.X);
		var pixelY = (int)Math.Floor(canvasPosition.Y);

		if (pixelX < 0 || pixelX >= _canvasWidth || pixelY < 0 || pixelY >= _canvasHeight)
		{
			PaintCursorRect.Visibility = Visibility.Collapsed;
			return;
		}

		var strokeThickness = 1.0 / _zoom;
		PaintCursorRect.StrokeThickness = strokeThickness;
		PaintCursorRect.Width = 1 + strokeThickness;
		PaintCursorRect.Height = 1 + strokeThickness;

		Canvas.SetLeft(PaintCursorRect, pixelX - strokeThickness / 2.0);
		Canvas.SetTop(PaintCursorRect, pixelY - strokeThickness / 2.0);

		PaintCursorRect.Visibility = Visibility.Visible;
	}

	private void CenterViewport()
	{
		if (ViewportHost.ActualWidth <= 0 || ViewportHost.ActualHeight <= 0)
			return;

		CanvasPanTransform.X = Math.Round((ViewportHost.ActualWidth - _canvasWidth * _zoom) / 2.0);
		CanvasPanTransform.Y = Math.Round((ViewportHost.ActualHeight - _canvasHeight * _zoom) / 2.0);
	}

	private void OnWindowLoaded(object sender, RoutedEventArgs e)
	{
		if (_savedPanX.HasValue && _savedPanY.HasValue)
		{
			CanvasPanTransform.X = _savedPanX.Value;
			CanvasPanTransform.Y = _savedPanY.Value;
		}
		else
		{
			CenterViewport();
		}
	}

	private void OnUiZoomOutClick(object sender, RoutedEventArgs e) => AdjustUiZoom(-UiZoomStep);
	private void OnUiZoomInClick(object sender, RoutedEventArgs e) => AdjustUiZoom(UiZoomStep);

	private void AdjustUiZoom(double delta)
	{
		var scale = Math.Clamp(Math.Round(AppState.GetEditorUiScale() + delta, 2), AppState.EditorUiScaleMin, AppState.EditorUiScaleMax);
		UiScaleTransform.ScaleX = scale;
		UiScaleTransform.ScaleY = scale;
		UiZoomText.Text = $"{(int)Math.Round(scale * 100)}%";

		AppState.SetEditorUiScale(scale);
	}
}
