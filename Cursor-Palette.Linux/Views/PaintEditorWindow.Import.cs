using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Media.Imaging;
using CursorPalette.Services;
using System.Runtime.InteropServices;

namespace CursorPalette.Linux.Views;

public partial class PaintEditorWindow
{
	private async void OnImportClick(object? sender, RoutedEventArgs e)
	{
		if (!_ready)
			return;

		var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
		{
			Title = Loc.Get("S.ImportImage.Title"),
			AllowMultiple = false,
			FileTypeFilter = new[]
			{
				new FilePickerFileType("Image files") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.cur", "*.ani" } },
			},
		});

		if (files.Count == 0)
			return;

		var path = files[0].Path.LocalPath;

		if (TryImportAnimatedGif(path))
			return;

		WriteableBitmap? bitmap;

		var ext = Path.GetExtension(path);
		if (ext.Equals(".cur", StringComparison.OrdinalIgnoreCase) ||
			ext.Equals(".ani", StringComparison.OrdinalIgnoreCase))
		{
			bitmap = LoadCursorAsBitmapForImport(path);
		}
		else
		{
			await using var stream = File.OpenRead(path);
			bitmap = WriteableBitmap.Decode(stream);
		}

		if (bitmap == null)
			return;

		var importDialog = new ImportImageDialog(bitmap);
		await importDialog.ShowDialog(this);

		if (importDialog.Image == null)
			return;

		ApplyImportedImage(importDialog.Image, importDialog.ResultMode);
	}

	private static WriteableBitmap? LoadCursorAsBitmapForImport(string filePath)
	{
		var ext = Path.GetExtension(filePath);

		if (ext.Equals(".ani", StringComparison.OrdinalIgnoreCase))
		{
			var frames = AniCursorReader.Read(filePath);
			if (frames == null || frames.Frames.Count == 0)
				return null;

			return CursorCanvasImageToBitmap(frames.Frames[0]);
		}

		var image = CursorCanvasService.TryRead(filePath);
		if (image != null)
			return CursorCanvasImageToBitmap(image);

		return null;
	}

	private static WriteableBitmap CursorCanvasImageToBitmap(CursorCanvasImage image)
	{
		var bmp = new WriteableBitmap(
			new PixelSize(image.Width, image.Height),
			new Vector(96, 96),
			Avalonia.Platform.PixelFormat.Bgra8888,
			Avalonia.Platform.AlphaFormat.Unpremul);
		using var locked = bmp.Lock();
		Marshal.Copy(image.Bgra, 0, locked.Address, image.Bgra.Length);
		return bmp;
	}

	private void ApplyImportedImage(WriteableBitmap bitmap, ImportImageDialog.ImportMode mode)
	{
		var imageWidth = bitmap.PixelSize.Width;
		var imageHeight = bitmap.PixelSize.Height;

		if (imageWidth <= 0 || imageHeight <= 0)
			return;

		var imageBgra = new byte[imageWidth * imageHeight * BytesPerPixel];
		using var lockedBitmap = bitmap.Lock();
		Marshal.Copy(lockedBitmap.Address, imageBgra, 0, imageBgra.Length);

		if (IsFullyTransparent(imageBgra))
			return;

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
			var newCanvasWidth = Math.Clamp(Math.Max(_canvasWidth, imageWidth), MinCanvasDimension, MaxCanvasDimension);
			var newCanvasHeight = Math.Clamp(Math.Max(_canvasHeight, imageHeight), MinCanvasDimension, MaxCanvasDimension);

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

	private async void OnExportPngClick(object? sender, RoutedEventArgs e)
	{
		var pixels = Compose();
		var bitmap = new WriteableBitmap(
			new PixelSize(_canvasWidth, _canvasHeight),
			new Vector(Dpi, Dpi),
			Avalonia.Platform.PixelFormat.Bgra8888,
			Avalonia.Platform.AlphaFormat.Unpremul);
		using var lockedBitmap = bitmap.Lock();
		Marshal.Copy(pixels, 0, lockedBitmap.Address, pixels.Length);

		var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
		{
			Title = "Export PNG",
			DefaultExtension = "png",
			FileTypeChoices = new[] { new FilePickerFileType("PNG") { Patterns = new[] { "*.png" } } },
		});

		if (file == null)
			return;

		await using var stream = await file.OpenWriteAsync();
		bitmap.Save(stream);
	}

	private async void OnExportGifClick(object? sender, RoutedEventArgs e)
	{
		if (!IsAnimated)
			return;

		_timelineFrames[_activeFrameIndex] = CaptureCurrentAsFrame();

		var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
		{
			Title = "Export GIF",
			DefaultExtension = "gif",
			FileTypeChoices = new[] { new FilePickerFileType("GIF") { Patterns = new[] { "*.gif" } } },
		});

		if (file == null)
			return;

		var frameBitmaps = new List<WriteableBitmap>();
		var frameDelays = new List<int>();

		foreach (var frame in _timelineFrames)
		{
			var buffer = new byte[_canvasWidth * _canvasHeight * BytesPerPixel];
			Blit(buffer, _canvasWidth, _canvasHeight, frame.SpriteBgra, frame.SpriteWidth, frame.SpriteHeight, frame.OffsetX, frame.OffsetY);

			var bitmap = new WriteableBitmap(
				new PixelSize(_canvasWidth, _canvasHeight),
				new Vector(Dpi, Dpi),
				Avalonia.Platform.PixelFormat.Bgra8888,
				Avalonia.Platform.AlphaFormat.Unpremul);
			using var lockedBitmap = bitmap.Lock();
			Marshal.Copy(buffer, 0, lockedBitmap.Address, buffer.Length);
			frameBitmaps.Add(bitmap);
			frameDelays.Add(frame.DurationMs);
		}

		await using var stream = await file.OpenWriteAsync();
		AnimatedGifWriter.Save(stream, frameBitmaps, frameDelays);
	}

	private async void OnCanvasSizeClick(object? sender, RoutedEventArgs e)
	{
		if (!_ready)
			return;

		var dialog = new CanvasSizeDialog(_canvasWidth, _canvasHeight);
		await dialog.ShowDialog(this);

		if (dialog.ResultWidth == _canvasWidth && dialog.ResultHeight == _canvasHeight
			&& dialog.ResultAnchorX == 1 && dialog.ResultAnchorY == 1)
			return;

		ApplyCanvasSize(dialog.ResultWidth, dialog.ResultHeight, dialog.ResultAnchorX, dialog.ResultAnchorY);
	}

	private void ApplyCanvasSize(int width, int height, int anchorX, int anchorY)
	{
		PushHistory();

		width = Math.Clamp(width, MinCanvasDimension, MaxCanvasDimension);
		height = Math.Clamp(height, MinCanvasDimension, MaxCanvasDimension);

		var leftGrowthX = (int)((width - _canvasWidth) * anchorX / 2.0);
		var topGrowthY = (int)((height - _canvasHeight) * anchorY / 2.0);
		var newOffsetX = _offsetX + leftGrowthX;
		var newOffsetY = _offsetY + topGrowthY;

		_canvasWidth = width;
		_canvasHeight = height;

		var (minX, maxX) = HorizontalRange();
		var (minY, maxY) = VerticalRange();
		_offsetX = Math.Clamp(newOffsetX, minX, maxX);
		_offsetY = Math.Clamp(newOffsetY, minY, maxY);

		_panTransform.X -= leftGrowthX * _zoom;
		_panTransform.Y -= topGrowthY * _zoom;

		RenderAll();
	}
}
