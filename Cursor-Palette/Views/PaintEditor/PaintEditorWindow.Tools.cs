using System.Windows;
using System.Windows.Input;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class PaintEditorWindow
{
	private void UpdateToolButtons()
	{
		var isMove = _currentTool == AppState.PaintEditorToolMove;
		var isHand = _currentTool == AppState.PaintEditorToolHand;
		var isCanvas = _currentTool == AppState.PaintEditorToolCanvas;
		var isBrush = _currentTool == AppState.PaintEditorToolBrush;
		var isEraser = _currentTool == AppState.PaintEditorToolEraser;
		var isFill = _currentTool == AppState.PaintEditorToolFill;
		var isHotspot = _currentTool == AppState.PaintEditorToolHotspot;

		ToolMoveButton.IsChecked = isMove;
		ToolHandButton.IsChecked = isHand;
		ToolBrushButton.IsChecked = _currentTool == AppState.PaintEditorToolBrush;
		ToolEraserButton.IsChecked = _currentTool == AppState.PaintEditorToolEraser;
		ToolFillButton.IsChecked = isFill;
		ToolCanvasButton.IsChecked = isCanvas;
		ToolHotspotButton.IsChecked = isHotspot;

		MoveToolPanel.Visibility = isMove ? Visibility.Visible : Visibility.Collapsed;
		HandToolPanel.Visibility = isHand ? Visibility.Visible : Visibility.Collapsed;
		CanvasToolPanel.Visibility = isCanvas ? Visibility.Visible : Visibility.Collapsed;
		BrushToolPanel.Visibility = isBrush ? Visibility.Visible : Visibility.Collapsed;
		EraserToolPanel.Visibility = isEraser ? Visibility.Visible : Visibility.Collapsed;
		FillToolPanel.Visibility = isFill ? Visibility.Visible : Visibility.Collapsed;
		HotspotToolPanel.Visibility = isHotspot ? Visibility.Visible : Visibility.Collapsed;

		PreviewImage.IsHitTestVisible = isMove;
		ResizeOverlay.Visibility = isCanvas ? Visibility.Visible : Visibility.Collapsed;
		ViewportHost.Cursor = isHand ? Cursors.Hand : (isBrush || isEraser) ? Cursors.Cross : Cursors.Arrow;
		PaintCursorRect.Visibility = Visibility.Collapsed;

		if (isCanvas)
			UpdateResizeOverlay();
	}

	private void SetTool(string tool)
	{
		if (_currentTool == AppState.PaintEditorToolCanvas && tool != AppState.PaintEditorToolCanvas && _hasCanvasResizeSnapshot)
		{
			_canvasWidth = _canvasResizeSnapshotWidth;
			_canvasHeight = _canvasResizeSnapshotHeight;
			_offsetX = _canvasResizeSnapshotOffsetX;
			_offsetY = _canvasResizeSnapshotOffsetY;
			CanvasPanTransform.X = _canvasResizeSnapshotPanX;
			CanvasPanTransform.Y = _canvasResizeSnapshotPanY;

			RenderAll();
		}

		_hasCanvasResizeSnapshot = false;

		if (tool == AppState.PaintEditorToolCanvas)
		{
			_canvasResizeSnapshotWidth = _canvasWidth;
			_canvasResizeSnapshotHeight = _canvasHeight;
			_canvasResizeSnapshotOffsetX = _offsetX;
			_canvasResizeSnapshotOffsetY = _offsetY;
			_canvasResizeSnapshotPanX = CanvasPanTransform.X;
			_canvasResizeSnapshotPanY = CanvasPanTransform.Y;
			_hasCanvasResizeSnapshot = true;
		}

		_currentTool = tool;
		AppState.SetPaintEditorTool(_currentTool);
		UpdateToolButtons();
	}

	private void OnToolMoveClick(object sender, RoutedEventArgs e) => SetTool(AppState.PaintEditorToolMove);

	private void OnToolHandClick(object sender, RoutedEventArgs e) => SetTool(AppState.PaintEditorToolHand);

	private void OnToolBrushClick(object sender, RoutedEventArgs e) => SetTool(AppState.PaintEditorToolBrush);

	private void OnToolEraserClick(object sender, RoutedEventArgs e) => SetTool(AppState.PaintEditorToolEraser);

	private void OnToolFillClick(object sender, RoutedEventArgs e) => SetTool(AppState.PaintEditorToolFill);

	private void OnToolCanvasClick(object sender, RoutedEventArgs e) => SetTool(AppState.PaintEditorToolCanvas);

	private void OnToolHotspotClick(object sender, RoutedEventArgs e) => SetTool(AppState.PaintEditorToolHotspot);

	private void OnCanvasToolApplyClick(object sender, RoutedEventArgs e)
	{
		_hasCanvasResizeSnapshot = false;
		SetTool(AppState.PaintEditorToolMove);
	}
}
