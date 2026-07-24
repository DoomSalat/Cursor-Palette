using System.Windows;
using System.Windows.Controls;
using CursorPalette.Models;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class PresetEditorWindow
{
	private void OnDownloadPresetClick(object sender, RoutedEventArgs e)
	{
		var invalid = Path.GetInvalidPathChars();
		var presetName = string.Join("", NameBox.Text.Where(character => !invalid.Contains(character))).Trim();

		if (string.IsNullOrWhiteSpace(presetName))
			presetName = Loc.Get(LocDefaultPresetName);

		var roleFiles = new Dictionary<string, string>();

		foreach (var slot in _slots)
		{
			var resolvedPath = GetSlotResolvedPath(slot);

			if (resolvedPath != null && File.Exists(resolvedPath))
				roleFiles[slot.Role.RegistryName] = resolvedPath;
		}

		if (roleFiles.Count == 0)
			return;

		var lockedRoles = _slots.Where(slot => slot.IsLocked)
			.Select(slot => slot.Role.RegistryName)
			.ToHashSet();

		var path = PresetPackageService.DownloadPresetAsFolder(presetName, roleFiles, _baseSize, lockedRoles);

		if (path == null)
			return;

		ToastService.Show(EditorRootGrid, Loc.Format(LocToastPresetDownloaded, presetName, roleFiles.Count));

		if (AppState.GetOpenFolderAfterDownload())
			ExplorerService.RevealFile(path);
	}

	private void OnDownloadPresetMoreClick(object sender, RoutedEventArgs e)
	{
		var menu = new ContextMenu { PlacementTarget = DownloadPresetMoreButton, IsOpen = true };

		var xcursorItem = new MenuItem { Header = Loc.Get(LocExportAsXcursorTheme) };
		xcursorItem.Click += (_, _) => ExportPresetForLinux(asXcursorTheme: true);
		menu.Items.Add(xcursorItem);

		var linuxItem = new MenuItem { Header = Loc.Get(LocExportAsLinuxArchive) };
		linuxItem.Click += (_, _) => ExportPresetForLinux(asXcursorTheme: false);
		menu.Items.Add(linuxItem);
	}

	private void ExportPresetForLinux(bool asXcursorTheme)
	{
		var invalid = Path.GetInvalidPathChars();
		var presetName = string.Join("", NameBox.Text.Where(character => !invalid.Contains(character))).Trim();

		if (string.IsNullOrWhiteSpace(presetName))
			presetName = Loc.Get(LocDefaultPresetName);

		var roleFiles = new Dictionary<string, string>();

		foreach (var slot in _slots)
		{
			var resolvedPath = GetSlotResolvedPath(slot);

			if (resolvedPath != null && File.Exists(resolvedPath))
				roleFiles[slot.Role.RegistryName] = resolvedPath;
		}

		if (roleFiles.Count == 0)
			return;

		Directory.CreateDirectory(AppPaths.DownloadsDir);

		var path = asXcursorTheme
			? PresetPackageService.ExportXcursorThemeForFiles(presetName, roleFiles)
			: PresetPackageService.ExportLinuxArchiveForFiles(presetName, roleFiles);

		if (path == null)
			return;

		var toastKey = asXcursorTheme ? LocToastExportedXcursorTheme : LocToastExportedLinuxArchive;
		ToastService.Show(EditorRootGrid, Loc.Format(toastKey, 1, Path.GetFileName(path)));

		if (AppState.GetOpenFolderAfterDownload())
			ExplorerService.RevealFile(path);
	}

	private void OnSaveButtonClick(object sender, RoutedEventArgs e)
	{
		if (_slots.All(slot => slot.SourcePath == null && slot.RefPresetId == null))
		{
			MessageBox.Show(Loc.Get(LocEditorNoFiles), Title,
				MessageBoxButton.OK, MessageBoxImage.Information);
			return;
		}

		var draft = new PresetDraft { Id = _draftId, Name = NameBox.Text, BaseSize = _baseSize };

		foreach (var slot in _slots.Where(slot => slot.SourcePath != null || slot.RefPresetId != null))
		{
			draft.RoleSources[slot.Role.RegistryName] = slot.RefPresetId != null
				? new RoleSourceDraft { Ref = new RoleRef { PresetId = slot.RefPresetId, FileName = slot.RefFileName! } }
				: new RoleSourceDraft { OwnFilePath = slot.SourcePath };
		}

		foreach (var slot in _slots.Where(slot => slot.IsLocked))
			draft.LockedRoles.Add(slot.Role.RegistryName);

		Result = draft;
		DialogResult = true;
	}
}
