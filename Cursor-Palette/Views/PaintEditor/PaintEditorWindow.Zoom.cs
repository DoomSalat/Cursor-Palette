using System.Windows;
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
		var isHandDrag = e.ChangedButton == MouseButton.Left && _currentTool == AppState.PaintEditorToolHand;

		if (e.ChangedButton != MouseButton.Middle && !isHandDrag)
			return;

		_isPanning = true;
		_panStart = e.GetPosition(ViewportHost);
		_panStartHOffset = CanvasPanTransform.X;
		_panStartVOffset = CanvasPanTransform.Y;
		ViewportHost.CaptureMouse();
		e.Handled = true;
	}

	private void OnViewportPreviewMouseUp(object sender, MouseButtonEventArgs e)
	{
		var isHandDrag = e.ChangedButton == MouseButton.Left && _currentTool == AppState.PaintEditorToolHand;

		if ((e.ChangedButton != MouseButton.Middle && !isHandDrag) || !_isPanning)
			return;

		_isPanning = false;
		ViewportHost.ReleaseMouseCapture();
	}

	private void OnViewportPreviewMouseMove(object sender, MouseEventArgs e)
	{
		if (!_isPanning)
			return;

		var pos = e.GetPosition(ViewportHost);

		CanvasPanTransform.X = _panStartHOffset + (pos.X - _panStart.X);
		CanvasPanTransform.Y = _panStartVOffset + (pos.Y - _panStart.Y);
	}

	private void CenterViewport()
	{
		if (ViewportHost.ActualWidth <= 0 || ViewportHost.ActualHeight <= 0)
			return;

		CanvasPanTransform.X = Math.Round((ViewportHost.ActualWidth - _canvasWidth * _zoom) / 2.0);
		CanvasPanTransform.Y = Math.Round((ViewportHost.ActualHeight - _canvasHeight * _zoom) / 2.0);
	}

	private void OnWindowLoaded(object sender, RoutedEventArgs e) => CenterViewport();

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
