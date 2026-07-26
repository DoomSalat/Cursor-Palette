using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CursorPalette.Models;
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
	}

	private void OnEditorUseScalingCheckedChanged(object sender, RoutedEventArgs e)
	{
		if (!_sizeSliderReady)
			return;

		_useScaling = EditorUseScalingCheckBox.IsChecked == true;
	}

	private void OnEditorScaleModeIconClick(object sender, MouseButtonEventArgs e)
	{
		_scaleMode = _scaleMode == ScaleMode.NearestNeighbor
			? ScaleMode.AreaWeighted
			: ScaleMode.NearestNeighbor;

		UpdateEditorScaleIcon();
	}

	private void UpdateEditorScaleIcon()
	{
		var iconUri = _scaleMode == ScaleMode.NearestNeighbor ? StairIconUri : ExpandIconUri;
		EditorUseScalingIcon.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconUri));
	}
}
