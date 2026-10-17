using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CursorPalette.Models;
using CursorPalette.Services;
using Microsoft.Win32;

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

		if (_selectedPresetIds.Count > 0)
			ApplyBulkSizeAndScale(sizeInPixels, scaleEnabled, _activeScaleMode);

		if (_activePresetId != null)
		{
			PresetStore.UpdateUseScaling(_activePresetId, scaleEnabled);

			var preset = _presets.FirstOrDefault(preset => preset.Id == _activePresetId);
			if (preset != null)
				preset.UseScaling = scaleEnabled;

			_activeUseScaling = scaleEnabled;
		}
		else
		{
			AppState.SetDefaultCellUseScaling(scaleEnabled);
			_activeUseScaling = scaleEnabled;
		}

		_baselineScaleEnabled = scaleEnabled;
		_baselineScaleMode = _activeScaleMode;

		ApplyAndPersistSize(sizeInPixels);
		ReloadGallery();
	}

	private void ApplyBulkSizeAndScale(int sizeInPixels, bool useScaling, ScaleMode scaleMode)
	{
		foreach (var presetId in _selectedPresetIds)
		{
			PresetStore.UpdateBaseSize(presetId, sizeInPixels);
			PresetStore.UpdateUseScaling(presetId, useScaling);
			PresetStore.UpdateScaleMode(presetId, scaleMode);

			var preset = _presets.FirstOrDefault(preset => preset.Id == presetId);
			if (preset == null)
				continue;

			preset.BaseSize = sizeInPixels;
			preset.UseScaling = useScaling;
			preset.ScaleMode = scaleMode;
		}
	}

	public async void ApplyAndPersistSize(int sizeInPixels)
	{
		try
		{
			ShowLoadingOverlay();
			await Dispatcher.Yield(DispatcherPriority.Render);

			var sourceValues = _activeSourceValues;
			var useScaling = _activeUseScaling;
			var scaleMode = _activeScaleMode;

			await Task.Run(() =>
			{
				if (sourceValues != null)
				{
					var valuesToApply = useScaling
						? CursorScalerService.ScaleValues(sourceValues, sizeInPixels, scaleMode)
						: CursorApplyStagingService.Stage(sourceValues);
					RegistryCursorService.ApplyValuesAndBaseSize(valuesToApply, sizeInPixels);
				}
				else
				{
					RegistryCursorService.SetBaseSize(sizeInPixels);
				}
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

	private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
		Dispatcher.BeginInvoke(new Action(ReapplyScalingForMonitorScales));

	private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) =>
		Dispatcher.BeginInvoke(new Action(ReapplyScalingForMonitorScales));

	private async void ReapplyScalingForMonitorScales()
	{
		var monitorScales = DisplayDpiService.GetDistinctMonitorScales();
		if (monitorScales.SequenceEqual(_appliedMonitorScales))
			return;

		_appliedMonitorScales = monitorScales;

		if (!_activeUseScaling || _activeSourceValues == null)
			return;

		var sourceValues = _activeSourceValues;
		var sizeInPixels = _baselineSizePx;
		var scaleMode = _activeScaleMode;

		try
		{
			await Task.Run(() => RegistryCursorService.ApplyValuesAndBaseSize(
				CursorScalerService.ScaleValues(sourceValues, sizeInPixels, scaleMode), sizeInPixels));
		}
		catch (Exception exception)
		{
			DebugLogger.Log($"{nameof(ReapplyScalingForMonitorScales)} failed: {exception.Message}");
		}
	}

	private bool _scaleCursorsReady;
	private bool _baselineScaleEnabled;
	private ScaleMode _baselineScaleMode;

	public void InitScaleCursorsCheckBox()
	{
		var activePreset = _activePresetId != null
			? _presets.FirstOrDefault(preset => preset.Id == _activePresetId)
			: null;
		var initialValue = activePreset?.UseScaling ?? AppState.GetDefaultCellUseScaling();

		if (activePreset != null)
		{
			_activeSourceValues = BuildPresetSourceValues(activePreset);
			_activeUseScaling = activePreset.UseScaling;
			_activeScaleMode = activePreset.ScaleMode;
		}
		else
		{
			_activeUseScaling = AppState.GetDefaultCellUseScaling();
			_activeScaleMode = AppState.GetScaleMode();
		}

		SetScaleCheckboxSilently(initialValue);
		_baselineScaleMode = _activeScaleMode;
		UpdateScaleIcon(_activeScaleMode);

		var sizeInPixels = RegistryCursorService.SizeStep + (int)SizeSlider.Value * RegistryCursorService.SizeStep;
		UpdateApplySizeButtonHighlight(sizeInPixels);
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

		if (_selectedPresetIds.Count > 0)
		{
			foreach (var presetId in _selectedPresetIds)
			{
				PresetStore.UpdateScaleMode(presetId, _activeScaleMode);

				var selectedPreset = _presets.FirstOrDefault(preset => preset.Id == presetId);
				if (selectedPreset != null)
					selectedPreset.ScaleMode = _activeScaleMode;
			}
		}

		if (_activePresetId != null)
		{
			PresetStore.UpdateScaleMode(_activePresetId, _activeScaleMode);

			var preset = _presets.FirstOrDefault(preset => preset.Id == _activePresetId);
			if (preset != null)
				preset.ScaleMode = _activeScaleMode;
		}
		else if (_selectedPresetIds.Count == 0)
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
