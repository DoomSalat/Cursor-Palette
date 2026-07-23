using System.Windows;
using CursorPalette.Models;
using CursorPalette.Services;
using Microsoft.Win32;

namespace CursorPalette.Views;

public partial class PresetEditorWindow
{
	private void BrowseForSlot(Slot slot)
	{
		var dialog = new OpenFileDialog
		{
			Filter = Loc.Get(LocEditorFileFilter),
			CheckFileExists = true,
			InitialDirectory = AppPaths.DownloadsDir,
		};
		if (dialog.ShowDialog(this) == true)
		{
			if (IsCursorFullyTransparent(dialog.FileName))
			{
				MessageBox.Show(Loc.Get(LocEditorEmptyCursorWarning), Title,
					MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			SetSlotSource(slot, dialog.FileName);
		}
	}

	private void DownloadSlot(Slot slot)
	{
		var resolvedPath = GetSlotResolvedPath(slot);

		if (resolvedPath == null || !File.Exists(resolvedPath))
			return;

		Directory.CreateDirectory(AppPaths.DownloadsDir);

		var baseName = ExportFileNaming.Build(NameBox.Text, slot.Role.RegistryName, slot.Role.RegistryName);
		var extension = Path.GetExtension(resolvedPath);
		var destPath = Path.Combine(AppPaths.DownloadsDir, $"{baseName}{extension}");

		var attempt = 1;

		while (File.Exists(destPath))
			destPath = Path.Combine(AppPaths.DownloadsDir, $"{baseName} ({attempt++}){extension}");

		File.Copy(resolvedPath, destPath);
		var now = DateTime.Now;
		File.SetCreationTime(destPath, now);
		File.SetLastWriteTime(destPath, now);
		ToastService.Show(EditorRootGrid, Loc.Format(LocToastDownloaded, Path.GetFileName(destPath)));
	}

	private void OpenHotspotEditor(Slot slot)
	{
		var resolvedPath = GetSlotResolvedPath(slot);

		if (resolvedPath == null)
			return;

		var hotspot = CursorHotspotService.Read(resolvedPath);

		if (hotspot == null)
			return;

		var editor = new HotspotEditorWindow(resolvedPath, hotspot) { Owner = this };

		if (editor.ShowDialog() != true)
			return;

		var tempPath = Path.Combine(Path.GetTempPath(),
			$"cursor-palette-hotspot-{Guid.NewGuid():N}{Path.GetExtension(resolvedPath)}");
		CursorHotspotService.WriteWithHotspot(resolvedPath, tempPath, editor.ResultX, editor.ResultY);
		CursorPreviewService.Invalidate(tempPath);

		SetSlotSource(slot, tempPath);
	}

	private void OpenPaintEditor(Slot slot)
	{
		var resolvedPath = GetSlotResolvedPath(slot);

		CursorCanvasImage? image;

		if (resolvedPath != null)
		{
			image = CursorCanvasService.TryRead(resolvedPath);
			if (image == null)
				return;
		}
		else
		{
			image = new CursorCanvasImage(32, 32, 0, 0, new byte[32 * 32 * 4]);
		}

		var editor = new PaintEditorWindow(image, NameBox.Text, slot.Role.RegistryName) { Owner = this };

		if (editor.ShowDialog() != true || editor.Result == null)
			return;

		var tempPath = Path.Combine(Path.GetTempPath(),
			$"cursor-palette-position-{Guid.NewGuid():N}{CurExtension}");
		CursorCanvasService.Write(tempPath, editor.Result);
		CursorPreviewService.Invalidate(tempPath);

		SetSlotSource(slot, tempPath);
	}

	private void PickExistingForSlot(Slot slot)
	{
		var presetPicker = new ExistingPresetPickerWindow(PresetStore.LoadAll()) { Owner = this };

		if (presetPicker.ShowDialog() != true || presetPicker.SelectedPreset == null)
			return;

		var rolePicker = new RolePickerWindow(presetPicker.SelectedPreset, slot.Role.RegistryName) { Owner = this };

		if (rolePicker.ShowDialog() != true || rolePicker.SelectedRole == null)
			return;

		var flatRef = PresetStore.ResolveLeafRef(presetPicker.SelectedPreset, rolePicker.SelectedRole);

		if (flatRef == null)
			return;

		SetSlotReference(slot, flatRef.PresetId, flatRef.FileName);
	}
}
