using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CursorPalette.Services;
using Microsoft.Win32;

namespace CursorPalette.Views;

public partial class PaintEditorWindow
{
	private const string ImportFileDialogFilter = "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.cur;*.ani)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.cur;*.ani|All files (*.*)|*.*";
	private const string ImportDialogTitle = "Load image";

	private IDisposable? _windowWideDragLeaveWatchdog;

	private void InitWindowWideDragDrop()
	{
		AddHandler(DragEnterEvent, new DragEventHandler(OnPaintWindowDragEnter), true);
		AddHandler(DragLeaveEvent, new DragEventHandler(OnPaintWindowDragLeave), true);
		AddHandler(DropEvent, new DragEventHandler(OnPaintWindowDrop), true);
	}

	private void OnPaintWindowDragEnter(object sender, DragEventArgs e)
	{
		_windowWideDragLeaveWatchdog ??= DropZoneService.StartLeaveWatchdog(this, HideWindowWideDropIndicators);

		ImportDropIndicator.Visibility = HasImportImageDrop(e) ? Visibility.Visible : Visibility.Collapsed;
		BgRefDropIndicator.Visibility = DropZoneService.GetFirstFile(e, IsImageFile) != null ? Visibility.Visible : Visibility.Collapsed;
	}

	private void OnPaintWindowDragLeave(object sender, DragEventArgs e) =>
		DropZoneService.HandleWindowDragLeave(this, HideWindowWideDropIndicators);

	private void OnPaintWindowDrop(object sender, DragEventArgs e) =>
		HideWindowWideDropIndicators();

	private void HideWindowWideDropIndicators()
	{
		ImportDropIndicator.Visibility = Visibility.Collapsed;
		BgRefDropIndicator.Visibility = Visibility.Collapsed;

		_windowWideDragLeaveWatchdog?.Dispose();
		_windowWideDragLeaveWatchdog = null;
	}

	private void OnImportImageClick(object sender, RoutedEventArgs e)
	{
		if (!_ready)
			return;

		var dialog = new OpenFileDialog
		{
			Filter = ImportFileDialogFilter,
			Title = ImportDialogTitle
		};

		if (dialog.ShowDialog() != true)
			return;

		ShowImportDialog(LoadBitmapFromFile(dialog.FileName));
	}

	private static BitmapSource? LoadBitmapFromFile(string path)
	{
		var extension = Path.GetExtension(path);

		if (string.Equals(extension, CurExtension, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(extension, AniExtension, StringComparison.OrdinalIgnoreCase))
		{
			return LoadCursorAsBitmapForImport(path);
		}

		try
		{
			var bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.UriSource = new Uri(path);
			bitmapImage.EndInit();
			bitmapImage.Freeze();

			return bitmapImage;
		}
		catch
		{
			return null;
		}
	}

	private static BitmapSource? LoadCursorAsBitmapForImport(string filePath)
	{
		var extension = Path.GetExtension(filePath);

		if (string.Equals(extension, AniExtension, StringComparison.OrdinalIgnoreCase))
		{
			var frames = AniCursorReader.Read(filePath);
			return frames?.Frames.Count > 0 ? frames.Frames[0] : null;
		}

		var image = CursorCanvasService.TryRead(filePath);

		if (image != null)
		{
			var writeableBitmap = new WriteableBitmap(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null);
			writeableBitmap.WritePixels(new Int32Rect(0, 0, image.Width, image.Height), image.Bgra, image.Width * BytesPerPixel, 0);
			writeableBitmap.Freeze();

			return writeableBitmap;
		}

		return CursorCanvasService.TryReadAsBitmap(filePath);
	}

	private void ShowImportDialog(BitmapSource? bitmap)
	{
		if (bitmap == null)
			return;

		var importDialog = new ImportImageDialog(bitmap) { Owner = this };

		if (importDialog.ShowDialog() != true || importDialog.Image == null)
			return;

		ApplyImportedImage(importDialog.Image, importDialog.ResultMode);
	}

	private void ApplyImportedImage(BitmapSource bitmap, ImportImageDialog.ImportMode mode)
	{
		var imageWidth = bitmap.PixelWidth;
		var imageHeight = bitmap.PixelHeight;

		if (imageWidth <= 0 || imageHeight <= 0)
			return;

		var imageBgra = BitmapToBgra(bitmap, imageWidth, imageHeight);

		if (IsFullyTransparent(imageBgra))
		{
			MessageBox.Show(Loc.Get(LocPaintEmptyWarning), Loc.Get(LocPaintTitle),
				MessageBoxButton.OK, MessageBoxImage.Warning);

			return;
		}

		PushHistory();

		if (mode == ImportImageDialog.ImportMode.Replace)
		{
			_spriteWidth = Math.Clamp(imageWidth, MinCanvasDimension, MaxCanvasDimension);
			_spriteHeight = Math.Clamp(imageHeight, MinCanvasDimension, MaxCanvasDimension);
			_spriteBgra = imageBgra;
			_canvasWidth = _spriteWidth;
			_canvasHeight = _spriteHeight;
			_offsetX = 0;
			_offsetY = 0;
			_hotspotOffsetX = Math.Clamp(_hotspotOffsetX, 0, _spriteWidth - 1);
			_hotspotOffsetY = Math.Clamp(_hotspotOffsetY, 0, _spriteHeight - 1);
		}
		else
		{
			var newCanvasWidth = Math.Max(_canvasWidth, imageWidth);
			var newCanvasHeight = Math.Max(_canvasHeight, imageHeight);
			newCanvasWidth = Math.Clamp(newCanvasWidth, MinCanvasDimension, MaxCanvasDimension);
			newCanvasHeight = Math.Clamp(newCanvasHeight, MinCanvasDimension, MaxCanvasDimension);

			var composed = new byte[newCanvasWidth * newCanvasHeight * BytesPerPixel];
			Blit(composed, newCanvasWidth, newCanvasHeight, _spriteBgra, _spriteWidth, _spriteHeight, _offsetX, _offsetY);
			AlphaComposite(composed, newCanvasWidth, newCanvasHeight, imageBgra, imageWidth, imageHeight, 0, 0);

			_spriteWidth = newCanvasWidth;
			_spriteHeight = newCanvasHeight;
			_spriteBgra = composed;
			_canvasWidth = newCanvasWidth;
			_canvasHeight = newCanvasHeight;
			_offsetX = 0;
			_offsetY = 0;
		}

		_hasLastStrokeEnd = false;
		RenderAll();
	}

	private static byte[] BitmapToBgra(BitmapSource source, int width, int height)
	{
		if (source.Format == PixelFormats.Bgra32)
		{
			var pixels = new byte[width * height * BytesPerPixel];
			source.CopyPixels(pixels, width * BytesPerPixel, 0);
			return pixels;
		}

		var converted = new FormatConvertedBitmap();
		converted.BeginInit();
		converted.Source = source;
		converted.DestinationFormat = PixelFormats.Bgra32;
		converted.EndInit();
		converted.Freeze();

		var result = new byte[width * height * BytesPerPixel];
		converted.CopyPixels(result, width * BytesPerPixel, 0);

		return result;
	}

	private static void AlphaComposite(byte[] destination, int destinationWidth, int destinationHeight, byte[] source, int sourceWidth, int sourceHeight, int offsetX, int offsetY)
	{
		for (var y = 0; y < sourceHeight; y++)
		{
			var destinationY = y + offsetY;

			if (destinationY < 0 || destinationY >= destinationHeight)
				continue;

			for (var x = 0; x < sourceWidth; x++)
			{
				var destinationX = x + offsetX;

				if (destinationX < 0 || destinationX >= destinationWidth)
					continue;

				var sourceIndex = (y * sourceWidth + x) * BytesPerPixel;
				var destinationIndex = (destinationY * destinationWidth + destinationX) * BytesPerPixel;

				var sourceAlpha = source[sourceIndex + 3];

				if (sourceAlpha == 0)
					continue;

				if (sourceAlpha == 255)
				{
					destination[destinationIndex] = source[sourceIndex];
					destination[destinationIndex + 1] = source[sourceIndex + 1];
					destination[destinationIndex + 2] = source[sourceIndex + 2];
					destination[destinationIndex + 3] = 255;
				}
				else
				{
					var sourceAlphaNormalized = sourceAlpha / 255.0;
					var destinationAlpha = destination[destinationIndex + 3] / 255.0;
					var outputAlpha = sourceAlphaNormalized + destinationAlpha * (1 - sourceAlphaNormalized);

					if (outputAlpha <= 0)
						continue;

					destination[destinationIndex] = (byte)((source[sourceIndex] * sourceAlphaNormalized + destination[destinationIndex] * destinationAlpha * (1 - sourceAlphaNormalized)) / outputAlpha);
					destination[destinationIndex + 1] = (byte)((source[sourceIndex + 1] * sourceAlphaNormalized + destination[destinationIndex + 1] * destinationAlpha * (1 - sourceAlphaNormalized)) / outputAlpha);
					destination[destinationIndex + 2] = (byte)((source[sourceIndex + 2] * sourceAlphaNormalized + destination[destinationIndex + 2] * destinationAlpha * (1 - sourceAlphaNormalized)) / outputAlpha);
					destination[destinationIndex + 3] = (byte)(outputAlpha * 255);
				}
			}
		}
	}

	private static bool IsImportImageFile(string path)
	{
		var extension = Path.GetExtension(path);

		return !string.IsNullOrEmpty(extension) && (
			string.Equals(extension, PngExtension, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(extension, JpgExtension, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(extension, JpegExtension, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(extension, BmpExtension, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(extension, GifExtension, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(extension, CurExtension, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(extension, AniExtension, StringComparison.OrdinalIgnoreCase));
	}

	private static bool HasImportImageDrop(DragEventArgs e)
	{
		if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
			return false;

		return paths.Any(p => File.Exists(p) && IsImportImageFile(p));
	}

	private IDisposable? _importDragLeaveWatchdog;

	private void OnImportButtonDragEnter(object sender, DragEventArgs e)
	{
		_importDragLeaveWatchdog ??= DropZoneService.StartLeaveWatchdog(this, HideImportDropIndicator);

		if (HasImportImageDrop(e))
		{
			e.Effects = DragDropEffects.Copy;
			ImportDropIndicator.Visibility = Visibility.Visible;
		}
		else
		{
			e.Effects = DragDropEffects.None;
		}

		e.Handled = true;
	}

	private void OnImportButtonDragOver(object sender, DragEventArgs e)
	{
		e.Effects = HasImportImageDrop(e) ? DragDropEffects.Copy : DragDropEffects.None;
		e.Handled = true;
	}

	private void OnImportButtonDragLeave(object sender, DragEventArgs e)
	{
		DropZoneService.HandleWindowDragLeave(this, HideImportDropIndicator);
		e.Handled = true;
	}

	private void HideImportDropIndicator()
	{
		ImportDropIndicator.Visibility = Visibility.Collapsed;
		_importDragLeaveWatchdog?.Dispose();
		_importDragLeaveWatchdog = null;
	}

	private void OnImportButtonDrop(object sender, DragEventArgs e)
	{
		HideImportDropIndicator();

		if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
			return;

		var path = paths.FirstOrDefault(p => File.Exists(p) && IsImportImageFile(p));

		if (path == null)
			return;

		ShowImportDialog(LoadBitmapFromFile(path));
		e.Handled = true;
	}
}
