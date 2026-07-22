using System.Windows.Input;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class PaintEditorWindow
{
	private bool _eyedropperArmed;

	private bool IsEyedropperTool =>
		_currentTool == AppState.PaintEditorToolBrush ||
		_currentTool == AppState.PaintEditorToolFill;

	private void OnEyedropperClick(object? sender, EventArgs e) => SetEyedropperArmed(!_eyedropperArmed);

	private void SetEyedropperArmed(bool armed)
	{
		_eyedropperArmed = armed;
		RefreshEyedropperVisuals();
	}

	private void PickColorUnderCursor()
	{
		if (NativeColorPicker.TryGetScreenPixelColor(out var color))
			ColorWheel.SetColorFromRgb(color.R, color.G, color.B);

		if (_eyedropperArmed)
			SetEyedropperArmed(false);
	}

	private bool IsEyedropperActive() =>
		_eyedropperArmed || (IsEyedropperTool && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt));

	private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Escape && _eyedropperArmed)
		{
			SetEyedropperArmed(false);
			e.Handled = true;

			return;
		}

		if (e.Key == Key.System && (e.SystemKey == Key.LeftAlt || e.SystemKey == Key.RightAlt))
			RefreshEyedropperVisuals();

		if (Keyboard.Modifiers != ModifierKeys.None)
			return;

		var tool = e.Key switch
		{
			Key.H => AppState.PaintEditorToolHand,
			Key.V => AppState.PaintEditorToolMove,
			Key.B => AppState.PaintEditorToolBrush,
			Key.E => AppState.PaintEditorToolEraser,
			Key.G => AppState.PaintEditorToolFill,
			Key.C => AppState.PaintEditorToolCanvas,
			Key.O => AppState.PaintEditorToolHotspot,
			_ => null
		};

		if (tool != null)
		{
			SetTool(tool);
			e.Handled = true;
		}
	}

	private void OnWindowPreviewKeyUp(object sender, KeyEventArgs e)
	{
		if (e.Key is Key.LeftAlt or Key.RightAlt || e.SystemKey is Key.LeftAlt or Key.RightAlt)
			RefreshEyedropperVisuals();
	}
}
