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

		var destPath = slot.SourcePath ?? Path.Combine(Path.GetTempPath(), $"cursor-palette-hotspot-{Guid.NewGuid():N}{Path.GetExtension(resolvedPath)}");

		CursorHotspotService.WriteWithHotspot(resolvedPath, destPath, editor.ResultX, editor.ResultY);
		CursorPreviewService.Invalidate(destPath);

		if (slot.SourcePath != null)
			UpdateHotspotDot(slot);
		else
			SetSlotSource(slot, destPath);
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

			var existingIconImages = ReadAniIconImages(resolvedPath);

			editor = new PaintEditorWindow(seed.Value.Frames, seed.Value.DelaysMs, NameBox.Text, slot.Role.RegistryName, existingIconImages) { Owner = this };
		}
		else
		{
			CursorCanvasImage? image;
			List<CursorCanvasImage>? existingIconImages = null;

			if (resolvedPath != null)
			{
				image = CursorCanvasService.TryRead(resolvedPath);
				if (image == null)
					return;

				existingIconImages = CursorCanvasService.TryReadAllImages(resolvedPath);
			}
			else
			{
				image = new CursorCanvasImage(32, 32, 0, 0, new byte[32 * 32 * 4]);
			}

			editor = new PaintEditorWindow(image, NameBox.Text, slot.Role.RegistryName, existingIconImages) { Owner = this };
		}

		if (editor.ShowDialog() != true)
			return;

		var isAniResult = editor.ResultFrames is { Count: > 1 };
		var desiredExtension = isAniResult ? AniExtension : CurExtension;
		var canReusePath = slot.SourcePath != null &&
			string.Equals(Path.GetExtension(slot.SourcePath), desiredExtension, StringComparison.OrdinalIgnoreCase);

		string destPath = canReusePath
			? slot.SourcePath!
			: Path.Combine(Path.GetTempPath(), $"cursor-palette-position-{Guid.NewGuid():N}{desiredExtension}");

		if (isAniResult)
		{
			AniCursorWriter.Save(destPath, editor.ResultFrames!, editor.ResultFrameDelaysMs!,
				editor.ResultIconSizes, editor.ResultIconSizeCustomImages,
				editor.ResultIconSizeScaleModeOverrides, editor.ResultIconSizesScaleMode);
		}
		else if (editor.Result != null)
		{
			if (editor.ResultIconSizes is { Count: > 1 } iconSizes)
			{
				var overrides = editor.ResultIconSizeScaleModeOverrides;
				var customImages = editor.ResultIconSizeCustomImages;

				var images = iconSizes
					.Select(size =>
					{
						if (customImages != null && customImages.TryGetValue(size, out var custom))
							return custom;

						return size == editor.Result.Width
							? editor.Result
							: CursorScalerService.ScaleImage(editor.Result, size, size,
								overrides != null && overrides.TryGetValue(size, out var mode) ? mode : editor.ResultIconSizesScaleMode);
					})
					.ToList();

				CursorCanvasService.WriteMultiSize(destPath, images);
			}
			else
			{
				CursorCanvasService.Write(destPath, editor.Result);
			}
		}
		else
		{
			return;
		}

		CursorPreviewService.Invalidate(destPath);

		if (canReusePath)
			UpdateHotspotDot(slot);
		else
			SetSlotSource(slot, destPath);
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

	private static List<CursorCanvasImage>? ReadAniIconImages(string path)
	{
		try
		{
			var bytes = File.ReadAllBytes(path);
			var chunks = AniCursorReader.FindIconChunkRanges(bytes);

			if (chunks.Count == 0)
				return null;

			var (offset, length) = chunks[0];
			var frameBytes = new byte[length];
			Array.Copy(bytes, offset, frameBytes, 0, length);

			return CursorCanvasService.TryReadAllImagesFromBytes(frameBytes);
		}
		catch
		{
			return null;
		}
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
