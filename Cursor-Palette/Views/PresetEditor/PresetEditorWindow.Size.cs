using System.Windows;
using System.Windows.Controls;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class PresetEditorWindow
{
	private void OnEditorSizeSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (EditorSizeValueText == null)
			return;

		var sizePx = RegistryCursorService.SizeStep + (int)e.NewValue * RegistryCursorService.SizeStep;
		EditorSizeValueText.Text = $"{sizePx} {PixelSuffix}";

		if (!_sizeSliderReady)
			return;

		_baseSize = sizePx;

		(Owner as MainWindow)?.SyncSizeSlider(sizePx);

		UpdateApplySizeButtonHighlight();
	}

	private void UpdateApplySizeButtonHighlight() =>
		EditorApplySizeButton.Style = (Style)Application.Current.Resources[
			_baseSize != _appliedPreviewSizePx ? StyleAccentButton : StyleButton];

	private void OnEditorApplySizeClick(object sender, RoutedEventArgs e)
	{
		RegistryCursorService.SetBaseSize(_baseSize);

		_appliedPreviewSizePx = _baseSize;

		UpdateApplySizeButtonHighlight();

		ToastService.Show(EditorRootGrid, Loc.Get(LocToastSizeApplied));
	}
}
