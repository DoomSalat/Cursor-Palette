using System.Windows;
using CursorPalette.Models;
using CursorPalette.Services;
using Microsoft.Win32;

namespace CursorPalette.Views;

public partial class PresetEditorWindow
{
	private void OnBrowseFolderClick(object sender, RoutedEventArgs e)
	{
		var dialog = new OpenFolderDialog
		{
			Title = Loc.Get(LocEditorBrowseFolder),
		};

		if (dialog.ShowDialog(this) == true)
			ImportFolder(dialog.FolderName);
	}

	private void ImportFolder(string folder, bool recursive = false, string? displayName = null)
	{
		if (!Directory.Exists(folder))
			return;

		var folderName = displayName ?? Path.GetFileName(folder);
		if (!string.IsNullOrWhiteSpace(folderName) && string.IsNullOrWhiteSpace(_draftId))
			NameBox.Text = folderName;

		var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
		var allFiles = Directory.EnumerateFiles(folder, "*.*", searchOption)
			.Where(ImageToCursorService.IsConvertibleFile)
			.ToList();

		if (allFiles.Count == 0)
		{
			MessageBox.Show(Loc.Get(LocEditorNoCursorInFolder), Title,
				MessageBoxButton.OK, MessageBoxImage.Information);
			return;
		}

		var cursorFiles = allFiles.Where(ImageToCursorService.IsCursorFile).ToList();
		var imageFiles = allFiles.Where(f => !ImageToCursorService.IsCursorFile(f)).ToList();

		var matched = 0;
		var emptySkipped = 0;

		matched += ImportFilesPass(cursorFiles, ref emptySkipped);

		var allFilled = _slots.Where(slot => !slot.IsLocked)
			.All(slot => slot.SourcePath != null || slot.RefPresetId != null);

		if (!allFilled && imageFiles.Count > 0)
			matched += ImportFilesPass(imageFiles, ref emptySkipped, skipFilled: true);

		if (emptySkipped > 0)
		{
			MessageBox.Show(Loc.Format(LocEditorEmptySkipped, emptySkipped), Title,
				MessageBoxButton.OK, MessageBoxImage.Information);
		}

		if (matched == 0)
		{
			MessageBox.Show(Loc.Format(LocEditorNoMatchInFolder, allFiles.Count), Title,
				MessageBoxButton.OK, MessageBoxImage.Information);
		}
	}

	private int ImportFilesPass(List<string> files, ref int emptySkipped, bool skipFilled = false)
	{
		var matched = 0;

		foreach (var file in files)
		{
			var role = CursorRoles.MatchByFileName(file);
			if (role == null)
				continue;

			var slot = _slots.First(slot => slot.Role.RegistryName == role.RegistryName);
			matched++;

			if (slot.IsLocked)
				continue;

			if (skipFilled && (slot.SourcePath != null || slot.RefPresetId != null))
				continue;

			var cursorPath = ImageToCursorService.ConvertToCursorTempFile(file);
			if (cursorPath == null)
				continue;

			if (ImageToCursorService.IsFullyTransparent(cursorPath))
			{
				emptySkipped++;
				continue;
			}

			SetSlotSource(slot, cursorPath);
		}

		return matched;
	}
}
