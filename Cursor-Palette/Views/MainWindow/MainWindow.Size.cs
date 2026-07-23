using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class MainWindow
{
	private void OnSizeSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (SizeValueText == null)
			return;

		var sizeInPixels = RegistryCursorService.SizeStep + (int)e.NewValue * RegistryCursorService.SizeStep;
		SizeValueText.Text = $"{sizeInPixels} {PixelSuffix}";
		UpdateApplySizeButtonHighlight(sizeInPixels);
	}

	private void UpdateApplySizeButtonHighlight(int sizeInPixels) =>
		ApplySizeButton.Style = (Style)Application.Current.Resources[
			sizeInPixels != _baselineSizePx ? StyleAccentButton : StyleButton];

	private void OnApplySizeButtonClick(object sender, RoutedEventArgs e)
	{
		var sizeInPixels = RegistryCursorService.SizeStep + (int)SizeSlider.Value * RegistryCursorService.SizeStep;
		ApplyAndPersistSize(sizeInPixels);
	}

	public async void ApplyAndPersistSize(int sizeInPixels)
	{
		try
		{
			ShowLoadingOverlay();
			await Dispatcher.Yield(DispatcherPriority.Render);

			await Task.Run(() => RegistryCursorService.SetBaseSize(sizeInPixels));

			if (_activePresetId != null)
			{
				PresetStore.UpdateBaseSize(_activePresetId, sizeInPixels);

				var preset = _presets.FirstOrDefault(preset => preset.Id == _activePresetId);
				if (preset != null)
					preset.BaseSize = sizeInPixels;
			}
			else
			{
				AppState.SetDefaultBaseSize(sizeInPixels);
			}

			if (_activeCellSizeText != null)
				_activeCellSizeText.Text = $"{sizeInPixels} {PixelSuffix}";

			_baselineSizePx = sizeInPixels;
			UpdateApplySizeButtonHighlight(sizeInPixels);

			ToastService.Show(RootGrid, Loc.Get(LocToastSizeApplied));
		}
		catch (Exception exception)
		{
			MessageBox.Show(Loc.Format(LocErrorApplyFailed, exception.Message),
				Loc.Get(LocErrorTitle), MessageBoxButton.OK, MessageBoxImage.Error);
		}
		finally
		{
			HideLoadingOverlay();
		}
	}

	public void SyncSizeSlider(int sizeInPixels) => SetSliderSilently(sizeInPixels);
}
