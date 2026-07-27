using System.Windows;
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
		var isBgRef = _currentTool == AppState.PaintEditorToolBgRef;
		var isIconSizes = _currentTool == AppState.PaintEditorToolIconSizes;

		ToolMoveButton.IsChecked = isMove;
		ToolHandButton.IsChecked = isHand;
		ToolBrushButton.IsChecked = _currentTool == AppState.PaintEditorToolBrush;
		ToolEraserButton.IsChecked = _currentTool == AppState.PaintEditorToolEraser;
		ToolFillButton.IsChecked = isFill;
		ToolCanvasButton.IsChecked = isCanvas;
		ToolHotspotButton.IsChecked = isHotspot;
		ToolBgRefButton.IsChecked = isBgRef;
		ToolIconSizesButton.IsChecked = isIconSizes;

		MoveToolPanel.Visibility = isMove ? Visibility.Visible : Visibility.Collapsed;
		HandToolPanel.Visibility = isHand ? Visibility.Visible : Visibility.Collapsed;
		CanvasToolPanel.Visibility = isCanvas ? Visibility.Visible : Visibility.Collapsed;
		BrushToolPanel.Visibility = (isBrush || isFill) ? Visibility.Visible : Visibility.Collapsed;
		EraserToolPanel.Visibility = isEraser ? Visibility.Visible : Visibility.Collapsed;
		FillToolPanel.Visibility = isFill ? Visibility.Visible : Visibility.Collapsed;
		HotspotToolPanel.Visibility = isHotspot ? Visibility.Visible : Visibility.Collapsed;
		BgRefToolPanel.Visibility = isBgRef ? Visibility.Visible : Visibility.Collapsed;
		IconSizesToolPanel.Visibility = isIconSizes ? Visibility.Visible : Visibility.Collapsed;

		CanvasSizeButton.IsEnabled = !isIconSizes;

		if (isIconSizes)
			RefreshIconSizesPanel();

		ResizeOverlay.Visibility = isCanvas ? Visibility.Visible : Visibility.Collapsed;
		UpdateViewportCursor();
		PaintCursorRect.Visibility = Visibility.Collapsed;

		var markerVisible = isHotspot ? Visibility.Visible : Visibility.Collapsed;
		HotspotMarker.Visibility = markerVisible;
		HotspotMarkerGlow.Visibility = markerVisible;

		if (isCanvas)
			UpdateResizeOverlay();

		if (isHotspot)
		{
			UpdateHotspotMarker();
			UpdateHotspotCoords();
			UpdateHotspotPresetHighlight();
		}

		UpdateBgRefRender();
	}

	private void SetTool(string tool)
	{
		var keepIconSizesPreview = _iconSizesEditMode && _iconSizesPreviewSize != null;

		if (_currentTool == AppState.PaintEditorToolIconSizes && tool != AppState.PaintEditorToolIconSizes && !keepIconSizesPreview)
			RestoreIconSizesPreview();

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

	private void OnToolIconSizesClick(object sender, RoutedEventArgs e) => SetTool(AppState.PaintEditorToolIconSizes);

	private void OnCanvasToolApplyClick(object sender, RoutedEventArgs e)
	{
		if (_hasCanvasResizeSnapshot)
		{
			PushHistory(new EditorSnapshot(
				(byte[])_spriteBgra.Clone(),
				_canvasResizeSnapshotWidth,
				_canvasResizeSnapshotHeight,
				_canvasResizeSnapshotOffsetX,
				_canvasResizeSnapshotOffsetY,
				_hotspotOffsetX,
				_hotspotOffsetY,
				_canvasResizeSnapshotPanX,
				_canvasResizeSnapshotPanY));
		}

		_hasCanvasResizeSnapshot = false;
		SetTool(AppState.PaintEditorToolMove);
	}
}
