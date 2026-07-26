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
			Title = "Load image",
			AllowMultiple = false,
			FileTypeFilter = new[]
			{
				new FilePickerFileType("Image files") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.cur", "*.ani" } },
			},
		});

		if (files.Count == 0)
			return;

		var path = files[0].Path.LocalPath;
		await using var stream = File.OpenRead(path);
		var bitmap = WriteableBitmap.Decode(stream);

		ApplyImportedImage(bitmap);
	}

	private void ApplyImportedImage(WriteableBitmap bitmap)
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

		_spriteWidth = Math.Clamp(imageWidth, MinCanvasDimension, MaxCanvasDimension);
		_spriteHeight = Math.Clamp(imageHeight, MinCanvasDimension, MaxCanvasDimension);
		_spriteBgra = imageBgra;
		_canvasWidth = _spriteWidth;
		_canvasHeight = _spriteHeight;
		_offsetX = 0;
		_offsetY = 0;
		_hotspotOffsetX = Math.Clamp(_hotspotOffsetX, 0, _spriteWidth - 1);
		_hotspotOffsetY = Math.Clamp(_hotspotOffsetY, 0, _spriteHeight - 1);
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

	private void OnCanvasSizeClick(object? sender, RoutedEventArgs e)
	{
		if (!_ready)
			return;
		PushHistory();

		var newWidth = Math.Clamp(_canvasWidth, MinCanvasDimension, MaxCanvasDimension);
		var newHeight = Math.Clamp(_canvasHeight, MinCanvasDimension, MaxCanvasDimension);
		_canvasWidth = newWidth;
		_canvasHeight = newHeight;
		ClampOffset();
		RenderAll();
	}
}
