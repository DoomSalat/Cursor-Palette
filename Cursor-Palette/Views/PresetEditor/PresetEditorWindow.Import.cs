using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
		var cursorFiles = Directory.EnumerateFiles(folder, "*.*", searchOption)
			.Where(IsCursorFile)
			.ToList();

		if (cursorFiles.Count == 0)
		{
			MessageBox.Show(Loc.Get(LocEditorNoCursorInFolder), Title,
				MessageBoxButton.OK, MessageBoxImage.Information);
			return;
		}

		var matched = 0;
		var emptySkipped = 0;

		foreach (var file in cursorFiles)
		{
			var role = CursorRoles.MatchByFileName(file);
			if (role == null)
				continue;

			var slot = _slots.First(slot => slot.Role.RegistryName == role.RegistryName);
			matched++;

			if (slot.IsLocked)
				continue;

			if (IsCursorFullyTransparent(file))
			{
				emptySkipped++;
				continue;
			}

			SetSlotSource(slot, file);
		}

		if (emptySkipped > 0)
		{
			MessageBox.Show(Loc.Format(LocEditorEmptySkipped, emptySkipped), Title,
				MessageBoxButton.OK, MessageBoxImage.Information);
		}

		if (matched == 0)
		{
			MessageBox.Show(Loc.Format(LocEditorNoMatchInFolder, cursorFiles.Count), Title,
				MessageBoxButton.OK, MessageBoxImage.Information);
		}
	}

	private static bool IsCursorFile(string path)
	{
		var extension = Path.GetExtension(path).ToLowerInvariant();

		return extension is CurExtension or AniExtension;
	}

	private static bool IsCursorFullyTransparent(string path)
	{
		var extension = Path.GetExtension(path).ToLowerInvariant();

		if (extension == CurExtension)
		{
			var image = CursorCanvasService.TryRead(path);
			if (image == null)
				return false;

			for (var i = 3; i < image.Bgra.Length; i += 4)
			{
				if (image.Bgra[i] != 0)
					return false;
			}

			return true;
		}

		if (extension == AniExtension)
		{
			var frames = AniCursorReader.Read(path);
			if (frames == null || frames.Frames.Count == 0)
				return false;

			foreach (var frame in frames.Frames)
			{
				if (IsBitmapSourceVisible(frame))
					return false;
			}

			return true;
		}

		return false;
	}

	private static bool IsBitmapSourceVisible(BitmapSource bitmap)
	{
		var width = bitmap.PixelWidth;
		var height = bitmap.PixelHeight;

		if (width == 0 || height == 0)
			return false;

		var stride = width * 4;
		var pixels = new byte[stride * height];

		if (bitmap.Format == PixelFormats.Bgra32)
		{
			bitmap.CopyPixels(pixels, stride, 0);
		}
		else
		{
			var converted = new FormatConvertedBitmap();
			converted.BeginInit();
			converted.Source = bitmap;
			converted.DestinationFormat = PixelFormats.Bgra32;
			converted.EndInit();
			converted.Freeze();
			converted.CopyPixels(pixels, stride, 0);
		}

		for (var i = 3; i < pixels.Length; i += 4)
		{
			if (pixels[i] != 0)
				return true;
		}

		return false;
	}
}
