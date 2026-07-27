using System.Collections.Generic;
using System.Windows;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class PaintEditorWindow
{
	private readonly record struct EditorSnapshot(
		byte[] SpriteBgra,
		int CanvasWidth,
		int CanvasHeight,
		int OffsetX,
		int OffsetY,
		int HotspotOffsetX,
		int HotspotOffsetY,
		double PanX,
		double PanY);

	private const int MaxHistorySize = 50;

	private readonly Stack<EditorSnapshot> _undoStack = new();
	private readonly Stack<EditorSnapshot> _redoStack = new();

	private EditorSnapshot CaptureSnapshot() =>
		new(
			(byte[])_spriteBgra.Clone(),
			_canvasWidth,
			_canvasHeight,
			_offsetX,
			_offsetY,
			_hotspotOffsetX,
			_hotspotOffsetY,
			CanvasPanTransform.X,
			CanvasPanTransform.Y);

	private void RestoreSnapshot(in EditorSnapshot snapshot)
	{
		_spriteBgra = (byte[])snapshot.SpriteBgra.Clone();
		_canvasWidth = snapshot.CanvasWidth;
		_canvasHeight = snapshot.CanvasHeight;
		_offsetX = snapshot.OffsetX;
		_offsetY = snapshot.OffsetY;
		_hotspotOffsetX = snapshot.HotspotOffsetX;
		_hotspotOffsetY = snapshot.HotspotOffsetY;
		CanvasPanTransform.X = snapshot.PanX;
		CanvasPanTransform.Y = snapshot.PanY;

		_hasLastStrokeEnd = false;

		RenderAll();
	}

	private void PushHistory() => PushHistory(CaptureSnapshot());

	private void PushHistory(EditorSnapshot snapshot)
	{
		_undoStack.Push(snapshot);

		if (_undoStack.Count > MaxHistorySize)
		{
			var array = _undoStack.ToArray();
			_undoStack.Clear();
			for (var i = array.Length - 1; i >= 1; i--)
				_undoStack.Push(array[i]);
		}

		_redoStack.Clear();
		UpdateUndoRedoButtons();
	}

	private void Undo()
	{
		if (_undoStack.Count == 0)
			return;

		_redoStack.Push(CaptureSnapshot());
		RestoreSnapshot(_undoStack.Pop());
		UpdateUndoRedoButtons();
	}

	private void Redo()
	{
		if (_redoStack.Count == 0)
			return;

		_undoStack.Push(CaptureSnapshot());
		RestoreSnapshot(_redoStack.Pop());
		UpdateUndoRedoButtons();
	}

	private void UpdateUndoRedoButtons()
	{
		var iconSizesPreviewActive = _currentTool == AppState.PaintEditorToolIconSizes && _hasIconSizesSnapshot;

		UndoButton.IsEnabled = !iconSizesPreviewActive && _undoStack.Count > 0;
		RedoButton.IsEnabled = !iconSizesPreviewActive && _redoStack.Count > 0;
	}

	private void ClearHistory()
	{
		_undoStack.Clear();
		_redoStack.Clear();
	}

	private void OnUndoClick(object sender, RoutedEventArgs e) => Undo();

	private void OnRedoClick(object sender, RoutedEventArgs e) => Redo();
}
