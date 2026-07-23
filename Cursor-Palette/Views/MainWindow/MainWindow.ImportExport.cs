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

		DetectedPackage? detected;
		try
		{
			detected = PresetPackageService.TryDetectPackage(dialog.FileName);
		}
		catch (PackageVersionUnsupportedException exception)
		{
			MessageBox.Show(Loc.Format(LocErrorImportVersionUnsupported, exception.FoundVersion, exception.MaxSupportedVersion),
				Loc.Get(LocErrorTitle), MessageBoxButton.OK, MessageBoxImage.Warning);
			return;
		}

		if (detected == null)
		{
			MessageBox.Show(Loc.Get(LocErrorImportUnrecognized),
				Loc.Get(LocErrorTitle), MessageBoxButton.OK, MessageBoxImage.Warning);
			return;
		}

		ImportPackage(detected);
	}

	private void ImportPackage(DetectedPackage detected)
	{
		var picker = new ImportPickerWindow(detected.Entries, detected.Groups) { Owner = this };

		if (picker.ShowDialog() == true)
		{
			var imported = PresetPackageService.ImportSelected(detected, picker.SelectedEntries,
				picker.SelectedGroups, picker.IgnoreIndividualSizes, picker.UniformSize);
			ReloadGallery();

			if (imported > 0)
				ToastService.Show(RootGrid, Loc.Format(LocToastImported, imported));
		}

		PresetPackageService.CleanupPackage(detected);
	}
}
