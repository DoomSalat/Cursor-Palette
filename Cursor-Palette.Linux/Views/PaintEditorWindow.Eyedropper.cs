using Avalonia.Input;
using CursorPalette.Services;

namespace CursorPalette.Linux.Views;

public partial class PaintEditorWindow
{
	private bool _eyedropperActive;

	private bool IsEyedropperActive(PointerEventArgs e) =>
		e.KeyModifiers.HasFlag(KeyModifiers.Alt) || _eyedropperActive;

	private void OnEyedropperClick(object? sender, EventArgs e)
	{
		_eyedropperActive = !_eyedropperActive;
		_colorWheel.SetEyedropperActive(_eyedropperActive);

		if (_eyedropperActive)
		{
			_previousToolForEyedropper = _currentTool;
		}
		else if (_previousToolForEyedropper != null)
		{
			SetTool(_previousToolForEyedropper);
			_previousToolForEyedropper = null;
		}
	}

	private void PickColorUnderCursor()
	{
		if (ScreenColorPickerProvider.Current != null)
		{
			if (ScreenColorPickerProvider.Current.TryGetScreenPixelColor(out var color))
			{
				_colorWheel.SetColorFromRgb(color.R, color.G, color.B);
			}
		}

		_eyedropperActive = false;
		_colorWheel.SetEyedropperActive(false);

		if (_previousToolForEyedropper != null)
		{
			SetTool(_previousToolForEyedropper);
			_previousToolForEyedropper = null;
		}
	}
}
