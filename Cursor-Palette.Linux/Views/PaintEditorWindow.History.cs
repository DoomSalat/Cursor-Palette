using CursorPalette.Services;

namespace CursorPalette.Linux.Views;

public partial class PaintEditorWindow
{
	private sealed record EditorSnapshot(
		byte[] SpriteBgra,
		int SpriteWidth,
		int SpriteHeight,
		int OffsetX,
		int OffsetY,
		int HotspotOffsetX,
		int HotspotOffsetY,
		int CanvasWidth,
		int CanvasHeight);

	private readonly Stack<EditorSnapshot> _undoStack = new();
	private readonly Stack<EditorSnapshot> _redoStack = new();
	private const int MaxHistorySize = 50;

	private EditorSnapshot CaptureSnapshot() =>
		new((byte[])_spriteBgra.Clone(), _spriteWidth, _spriteHeight,
			_offsetX, _offsetY, _hotspotOffsetX, _hotspotOffsetY,
			_canvasWidth, _canvasHeight);

	private void RestoreSnapshot(EditorSnapshot snapshot)
	{
		_spriteBgra = (byte[])snapshot.SpriteBgra.Clone();
		_spriteWidth = snapshot.SpriteWidth;
		_spriteHeight = snapshot.SpriteHeight;
		_offsetX = snapshot.OffsetX;
		_offsetY = snapshot.OffsetY;
		_hotspotOffsetX = snapshot.HotspotOffsetX;
		_hotspotOffsetY = snapshot.HotspotOffsetY;
		_canvasWidth = snapshot.CanvasWidth;
		_canvasHeight = snapshot.CanvasHeight;
		_hasLastStrokeEnd = false;
	}

	private void PushHistory()
	{
		_undoStack.Push(CaptureSnapshot());
		if (_undoStack.Count > MaxHistorySize)
		{
			var temp = _undoStack.ToList();
			_undoStack.Clear();
			foreach (var item in temp.Skip(1).Reverse())
				_undoStack.Push(item);
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
		RenderAll();
		UpdateUndoRedoButtons();
	}

	private void Redo()
	{
		if (_redoStack.Count == 0)
			return;
		_undoStack.Push(CaptureSnapshot());
		RestoreSnapshot(_redoStack.Pop());
		RenderAll();
		UpdateUndoRedoButtons();
	}

	private void ClearHistory()
	{
		_undoStack.Clear();
		_redoStack.Clear();
	}

	private void UpdateUndoRedoButtons()
	{
		_undoButton.IsEnabled = _undoStack.Count > 0;
		_redoButton.IsEnabled = _redoStack.Count > 0;
	}
}
