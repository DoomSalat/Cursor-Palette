using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
			var cursorPath = ImageToCursorService.ConvertToCursorTempFile(dialog.FileName);

			if (cursorPath == null)
				return;

			if (ImageToCursorService.IsFullyTransparent(cursorPath))
			{
				MessageBox.Show(Loc.Get(LocEditorEmptyCursorWarning), Title,
					MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			SetSlotSource(slot, cursorPath);
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

		if (AppState.GetOpenFolderAfterDownload())
			ExplorerService.RevealFile(destPath);
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

	private const int AniOpenFrameLimit = 60;

	private void OpenPaintEditor(Slot slot)
	{
		var resolvedPath = GetSlotResolvedPath(slot);

		PaintEditorWindow editor;

		if (resolvedPath != null && string.Equals(Path.GetExtension(resolvedPath), AniExtension, StringComparison.OrdinalIgnoreCase))
		{
			var seed = ReadAniAsFrames(resolvedPath);
			if (seed == null)
				return;

			editor = new PaintEditorWindow(seed.Value.Frames, seed.Value.DelaysMs, NameBox.Text, slot.Role.RegistryName) { Owner = this };
		}
		else
		{
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

			editor = new PaintEditorWindow(image, NameBox.Text, slot.Role.RegistryName) { Owner = this };
		}

		if (editor.ShowDialog() != true)
			return;

		string tempPath;

		if (editor.ResultFrames is { Count: > 1 } resultFrames)
		{
			tempPath = Path.Combine(Path.GetTempPath(), $"cursor-palette-position-{Guid.NewGuid():N}{AniExtension}");
			AniCursorWriter.Save(tempPath, resultFrames, editor.ResultFrameDelaysMs!);
		}
		else if (editor.Result != null)
		{
			tempPath = Path.Combine(Path.GetTempPath(), $"cursor-palette-position-{Guid.NewGuid():N}{CurExtension}");
			CursorCanvasService.Write(tempPath, editor.Result);
		}
		else
		{
			return;
		}

		CursorPreviewService.Invalidate(tempPath);

		SetSlotSource(slot, tempPath);
	}

	private static (List<CursorCanvasImage> Frames, List<int> DelaysMs)? ReadAniAsFrames(string path)
	{
		var animated = AniCursorReader.Read(path);

		if (animated == null || animated.Frames.Count == 0 || animated.StepFrameIndices.Count == 0)
			return null;

		var hotspot = CursorHotspotService.Read(path);
		var hotspotX = hotspot?.X ?? 0;
		var hotspotY = hotspot?.Y ?? 0;

		var frames = new List<CursorCanvasImage>();
		var delays = new List<int>();
		var stepCount = Math.Min(animated.StepFrameIndices.Count, AniOpenFrameLimit);

		for (var i = 0; i < stepCount; i++)
		{
			var frameIndex = Math.Clamp(animated.StepFrameIndices[i], 0, animated.Frames.Count - 1);
			var bitmap = animated.Frames[frameIndex];
			var bgra = BitmapSourceToBgra(bitmap);

			frames.Add(new CursorCanvasImage(bitmap.PixelWidth, bitmap.PixelHeight, hotspotX, hotspotY, bgra));
			delays.Add((int)animated.StepDurations[i].TotalMilliseconds);
		}

		return frames.Count > 0 ? (frames, delays) : null;
	}

	private static byte[] BitmapSourceToBgra(BitmapSource source)
	{
		var converted = source.Format == PixelFormats.Bgra32
			? source
			: new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

		var width = converted.PixelWidth;
		var height = converted.PixelHeight;
		var pixels = new byte[width * height * 4];
		converted.CopyPixels(pixels, width * 4, 0);

		return pixels;
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
