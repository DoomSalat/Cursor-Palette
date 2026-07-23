using System.Windows;
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

		var destDir = Path.Combine(AppPaths.DownloadsDir, presetName);

		var attempt = 1;

		while (Directory.Exists(destDir))
			destDir = Path.Combine(AppPaths.DownloadsDir, $"{presetName} ({attempt++})");

		Directory.CreateDirectory(destDir);

		var count = 0;

		foreach (var slot in _slots)
		{
			var resolvedPath = GetSlotResolvedPath(slot);
			if (resolvedPath == null || !File.Exists(resolvedPath))
				continue;

			var extension = Path.GetExtension(resolvedPath);
			var destPath = Path.Combine(destDir, $"{slot.Role.RegistryName}{extension}");
			File.Copy(resolvedPath, destPath);
			var now = DateTime.Now;
			File.SetCreationTime(destPath, now);
			File.SetLastWriteTime(destPath, now);
			count++;
		}

		if (count == 0)
		{
			Directory.Delete(destDir);
			return;
		}

		ToastService.Show(EditorRootGrid, Loc.Format(LocToastPresetDownloaded, presetName, count));
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
