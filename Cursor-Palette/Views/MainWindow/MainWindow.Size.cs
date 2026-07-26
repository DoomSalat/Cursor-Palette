using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CursorPalette.Models;
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

	private void UpdateApplySizeButtonHighlight(int sizeInPixels)
	{
		var sizeChanged = sizeInPixels != _baselineSizePx;
		var scaleChanged = _scaleCursorsReady && (ScaleCursorsCheckBox.IsChecked == true) != _baselineScaleEnabled;
		var scaleModeChanged = _activeScaleMode != _baselineScaleMode;
		ApplySizeButton.Style = (Style)Application.Current.Resources[
			sizeChanged || scaleChanged || scaleModeChanged ? StyleAccentButton : StyleButton];
	}

	private void OnApplySizeButtonClick(object sender, RoutedEventArgs e)
	{
		var sizeInPixels = RegistryCursorService.SizeStep + (int)SizeSlider.Value * RegistryCursorService.SizeStep;
		var scaleEnabled = ScaleCursorsCheckBox.IsChecked == true;

		if (_activePresetId != null)
		{
			PresetStore.UpdateUseScaling(_activePresetId, scaleEnabled);

			var preset = _presets.FirstOrDefault(preset => preset.Id == _activePresetId);
			if (preset != null)
				preset.UseScaling = scaleEnabled;

			_activeUseScaling = AppState.GetScaleCursorsEnabled() && scaleEnabled;
		}
		else
		{
			AppState.SetScaleCursorsEnabled(scaleEnabled);
			_activeUseScaling = scaleEnabled;
		}

		_baselineScaleEnabled = scaleEnabled;
		_baselineScaleMode = _activeScaleMode;

		ApplyAndPersistSize(sizeInPixels);
		ReloadGallery();
	}

	public async void ApplyAndPersistSize(int sizeInPixels)
	{
		try
		{
			ShowLoadingOverlay();
			await Dispatcher.Yield(DispatcherPriority.Render);

			var sourceValues = _activeSourceValues;
			var useScaling = _activeUseScaling;

			await Task.Run(() =>
			{
				if (sourceValues != null)
				{
					var valuesToApply = useScaling
						? CursorScalerService.ScaleValues(sourceValues, sizeInPixels, _activeScaleMode)
						: sourceValues;
					RegistryCursorService.ApplyValues(valuesToApply);
				}
				RegistryCursorService.SetBaseSize(sizeInPixels);
			});

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

	private bool _scaleCursorsReady;
	private bool _baselineScaleEnabled;
	private ScaleMode _baselineScaleMode;

	public void InitScaleCursorsCheckBox()
	{
		var activePreset = _activePresetId != null
			? _presets.FirstOrDefault(preset => preset.Id == _activePresetId)
			: null;
		var initialValue = activePreset?.UseScaling ?? AppState.GetScaleCursorsEnabled();

		if (activePreset != null)
		{
			_activeSourceValues = new Dictionary<string, string>();
			foreach (var role in CursorRoles.All)
			{
				var path = PresetStore.GetRoleFilePath(activePreset, role.RegistryName);
				_activeSourceValues[role.RegistryName] = path != null && File.Exists(path) ? path : EmptyValue;
			}
			_activeUseScaling = AppState.GetScaleCursorsEnabled() && activePreset.UseScaling;
			_activeScaleMode = activePreset.ScaleMode;
		}
		else
		{
			_activeUseScaling = AppState.GetScaleCursorsEnabled();
			_activeScaleMode = AppState.GetScaleMode();
		}

		SetScaleCheckboxSilently(initialValue);
		_baselineScaleMode = _activeScaleMode;
		UpdateScaleIcon(_activeScaleMode);
	}

	private void SetScaleCheckboxSilently(bool value)
	{
		_scaleCursorsReady = false;
		ScaleCursorsCheckBox.IsChecked = value;
		_baselineScaleEnabled = value;
		_baselineScaleMode = _activeScaleMode;
		_scaleCursorsReady = true;
	}

	private void OnScaleCursorsCheckedChanged(object sender, RoutedEventArgs e)
	{
		if (!_scaleCursorsReady)
			return;

		var sizeInPixels = RegistryCursorService.SizeStep + (int)SizeSlider.Value * RegistryCursorService.SizeStep;
		UpdateApplySizeButtonHighlight(sizeInPixels);
	}

	private void OnScaleModeIconClick(object sender, MouseButtonEventArgs e)
	{
		_activeScaleMode = _activeScaleMode == ScaleMode.NearestNeighbor
			? ScaleMode.AreaWeighted
			: ScaleMode.NearestNeighbor;

		if (_activePresetId != null)
		{
			PresetStore.UpdateScaleMode(_activePresetId, _activeScaleMode);

			var preset = _presets.FirstOrDefault(preset => preset.Id == _activePresetId);
			if (preset != null)
				preset.ScaleMode = _activeScaleMode;
		}
		else
		{
			AppState.SetScaleMode(_activeScaleMode);
		}

		UpdateScaleIcon(_activeScaleMode);

		var sizeInPixels = RegistryCursorService.SizeStep + (int)SizeSlider.Value * RegistryCursorService.SizeStep;
		UpdateApplySizeButtonHighlight(sizeInPixels);
	}

	private void UpdateScaleIcon(ScaleMode scaleMode)
	{
		var iconUri = scaleMode == ScaleMode.NearestNeighbor ? StairIconUri : ExpandIconUri;
		ScaleCursorsIcon.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconUri));
	}
}
