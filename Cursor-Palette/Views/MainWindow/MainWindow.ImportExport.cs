using System.Windows;
using CursorPalette.Models;
using CursorPalette.Services;
using Microsoft.Win32;

namespace CursorPalette.Views;

public partial class MainWindow
{
	private void OnExportButtonClick(object sender, RoutedEventArgs e)
	{
		new ExportWindow(_presets, _groups) { Owner = this }.ShowDialog();
	}

	private void OnImportButtonClick(object sender, RoutedEventArgs e)
	{
		var dialog = new OpenFileDialog
		{
			Filter = Loc.Get(LocImportFileFilter),
			CheckFileExists = true,
			InitialDirectory = AppPaths.DownloadsDir,
		};

		if (dialog.ShowDialog(this) != true)
			return;

		ImportPackageFile(dialog.FileName);
	}

	private void ImportPackageFile(string path)
	{
		DetectedPackage? detected;
		try
		{
			detected = PresetPackageService.TryDetectPackage(path);
		}
		catch (PackageVersionUnsupportedException exception)
		{
			ToastService.Show(RootGrid, Loc.Format(LocErrorImportVersionUnsupported, exception.FoundVersion, exception.MaxSupportedVersion));
			return;
		}

		if (detected == null)
		{
			ToastService.Show(RootGrid, Loc.Get(LocErrorImportUnrecognized));
			return;
		}

		ImportPackage(detected);
	}

	private void ImportPackage(DetectedPackage detected)
	{
		var picker = new ImportPickerWindow(detected, _presets) { Owner = this };

		if (picker.ShowDialog() == true)
		{
			var imported = PresetPackageService.ImportSelected(detected, picker.SelectedEntries,
				picker.SelectedGroups, picker.IgnoreIndividualSizes, picker.UniformSize);
			ReloadGallery();

			ToastService.Show(RootGrid, imported > 0
				? Loc.Format(LocToastImported, imported)
				: Loc.Get(LocErrorImportNothingFound));
		}

		PresetPackageService.CleanupPackage(detected);
	}
}
