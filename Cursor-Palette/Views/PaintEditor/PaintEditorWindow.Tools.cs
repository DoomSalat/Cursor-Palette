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
		var isBrushOrEraser = !isMove && !isHand && !isCanvas;

		ToolMoveButton.IsChecked = isMove;
		ToolHandButton.IsChecked = isHand;
		ToolBrushButton.IsChecked = _currentTool == AppState.PaintEditorToolBrush;
		ToolEraserButton.IsChecked = _currentTool == AppState.PaintEditorToolEraser;
		ToolCanvasButton.IsChecked = isCanvas;

		MoveToolPanel.Visibility = isMove ? Visibility.Visible : Visibility.Collapsed;
		HandToolPanel.Visibility = isHand ? Visibility.Visible : Visibility.Collapsed;
		CanvasToolPanel.Visibility = isCanvas ? Visibility.Visible : Visibility.Collapsed;
		BrushToolPanel.Visibility = isBrushOrEraser ? Visibility.Visible : Visibility.Collapsed;

		PreviewImage.IsHitTestVisible = isMove;
		ResizeOverlay.Visibility = isCanvas ? Visibility.Visible : Visibility.Collapsed;
		ViewportHost.Cursor = isHand ? Cursors.Hand : Cursors.Arrow;

		if (isCanvas)
			UpdateResizeOverlay();
	}

	// Entering the Canvas tool snapshots the current size/position so that switching away
	// from it without hitting Apply reverts the live preview instead of leaving it applied.
	private void SetTool(string tool)
	{
		if (_currentTool == AppState.PaintEditorToolCanvas && tool != AppState.PaintEditorToolCanvas && _hasCanvasResizeSnapshot)
		{
			_canvasWidth = _canvasResizeSnapshotWidth;
			_canvasHeight = _canvasResizeSnapshotHeight;
			_offsetX = _canvasResizeSnapshotOffsetX;
			_offsetY = _canvasResizeSnapshotOffsetY;
			RenderAll();
		}

		_hasCanvasResizeSnapshot = false;

		if (tool == AppState.PaintEditorToolCanvas)
		{
			_canvasResizeSnapshotWidth = _canvasWidth;
			_canvasResizeSnapshotHeight = _canvasHeight;
			_canvasResizeSnapshotOffsetX = _offsetX;
			_canvasResizeSnapshotOffsetY = _offsetY;
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

	private void OnToolCanvasClick(object sender, RoutedEventArgs e) => SetTool(AppState.PaintEditorToolCanvas);

	private void OnCanvasToolApplyClick(object sender, RoutedEventArgs e)
	{
		// Commit: clear the snapshot first so SetTool's revert-on-leave branch doesn't fire.
		_hasCanvasResizeSnapshot = false;
		SetTool(AppState.PaintEditorToolMove);
	}
}
