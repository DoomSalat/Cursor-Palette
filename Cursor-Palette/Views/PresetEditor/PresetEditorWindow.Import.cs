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
		var convertibleFiles = Directory.EnumerateFiles(folder, "*.*", searchOption)
			.Where(ImageToCursorService.IsConvertibleFile)
			.ToList();

		if (convertibleFiles.Count == 0)
		{
			MessageBox.Show(Loc.Get(LocEditorNoCursorInFolder), Title,
				MessageBoxButton.OK, MessageBoxImage.Information);
			return;
		}

		var matched = 0;
		var emptySkipped = 0;

		foreach (var file in convertibleFiles)
		{
			var role = CursorRoles.MatchByFileName(file);
			if (role == null)
				continue;

			var slot = _slots.First(slot => slot.Role.RegistryName == role.RegistryName);
			matched++;

			if (slot.IsLocked)
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

		if (emptySkipped > 0)
		{
			MessageBox.Show(Loc.Format(LocEditorEmptySkipped, emptySkipped), Title,
				MessageBoxButton.OK, MessageBoxImage.Information);
		}

		if (matched == 0)
		{
			MessageBox.Show(Loc.Format(LocEditorNoMatchInFolder, convertibleFiles.Count), Title,
				MessageBoxButton.OK, MessageBoxImage.Information);
		}
	}
}
