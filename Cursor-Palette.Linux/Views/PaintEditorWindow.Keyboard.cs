using Avalonia.Input;
using CursorPalette.Services;

namespace CursorPalette.Linux.Views;

public partial class PaintEditorWindow
{
	private void OnWindowKeyDown(object? sender, KeyEventArgs e)
	{
		if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
		{
			switch (e.Key)
			{
				case Key.Z:
					Undo();
					e.Handled = true;
					return;
				case Key.Y:
					Redo();
					e.Handled = true;
					return;
			}
		}

		switch (e.Key)
		{
			case Key.V:
				SetTool(AppState.PaintEditorToolMove);
				e.Handled = true;
				break;
			case Key.H:
				SetTool(AppState.PaintEditorToolHand);
				e.Handled = true;
				break;
			case Key.B:
				SetTool(AppState.PaintEditorToolBrush);
				e.Handled = true;
				break;
			case Key.E:
				SetTool(AppState.PaintEditorToolEraser);
				e.Handled = true;
				break;
			case Key.G:
				SetTool(AppState.PaintEditorToolFill);
				e.Handled = true;
				break;
			case Key.C:
				SetTool(AppState.PaintEditorToolCanvas);
				e.Handled = true;
				break;
			case Key.O:
				SetTool(AppState.PaintEditorToolHotspot);
				e.Handled = true;
				break;
			case Key.R:
				SetTool(AppState.PaintEditorToolBgRef);
				e.Handled = true;
				break;
		}
	}
}
