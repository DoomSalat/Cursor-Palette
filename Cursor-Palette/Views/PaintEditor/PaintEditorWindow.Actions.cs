using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class PaintEditorWindow
{
	private void OnInfoButtonClick(object sender, RoutedEventArgs e)
	{
		new InfoHelpWindow(Loc.Get(LocInfoTitle), Services.HelpTextService.Get("Paint")) { Owner = this }.ShowDialog();
	}

	private void OnCanvasSizeButtonClick(object sender, RoutedEventArgs e)
	{
		if (!_ready)
			return;

		RestoreIconSizesPreview();

		var dialog = new CanvasSizeDialog(_canvasWidth, _canvasHeight) { Owner = this };

		if (dialog.ShowDialog() == true)
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

		CanvasPanTransform.X -= leftGrowthX * _zoom;
		CanvasPanTransform.Y -= topGrowthY * _zoom;

		RenderAll();
	}

	private void OnShowSpriteBoundsChanged(object sender, RoutedEventArgs e)
	{
		var value = ShowSpriteBoundsCheck.IsChecked == true;
		SpriteBoundsRect.Visibility = value ? Visibility.Visible : Visibility.Collapsed;

		AppState.SetShowSpriteBounds(value);

		UpdateSpriteBoundsRect();
	}

	private void OnSaveClick(object sender, RoutedEventArgs e)
	{
		RestoreIconSizesPreview();

		var pixels = Compose();

		if (IsFullyTransparent(pixels))
		{
			MessageBox.Show(Loc.Get(LocPaintEmptyWarning), Loc.Get(LocPaintTitle),
				MessageBoxButton.OK, MessageBoxImage.Warning);

			return;
		}

		var hotspotX = Math.Clamp(_offsetX + _hotspotOffsetX, 0, _canvasWidth - 1);
		var hotspotY = Math.Clamp(_offsetY + _hotspotOffsetY, 0, _canvasHeight - 1);

		Result = new CursorCanvasImage(_canvasWidth, _canvasHeight, hotspotX, hotspotY, pixels);
		CaptureIconSizesResult();

		if (IsAnimated)
		{
			_timelineFrames[_activeFrameIndex] = CaptureCurrentAsFrame();

			var resultFrames = new List<CursorCanvasImage>(_timelineFrames.Count);
			var resultDelays = new List<int>(_timelineFrames.Count);

			foreach (var frame in _timelineFrames)
			{
				var buffer = new byte[_canvasWidth * _canvasHeight * BytesPerPixel];
				Blit(buffer, _canvasWidth, _canvasHeight, frame.SpriteBgra, frame.SpriteWidth, frame.SpriteHeight, frame.OffsetX, frame.OffsetY);

				var frameHotspotX = Math.Clamp(frame.OffsetX + _hotspotOffsetX, 0, _canvasWidth - 1);
				var frameHotspotY = Math.Clamp(frame.OffsetY + _hotspotOffsetY, 0, _canvasHeight - 1);

				resultFrames.Add(new CursorCanvasImage(_canvasWidth, _canvasHeight, frameHotspotX, frameHotspotY, buffer));
				resultDelays.Add(frame.DurationMs);
			}

			ResultFrames = resultFrames;
			ResultFrameDelaysMs = resultDelays;
		}

		DialogResult = true;
	}

	private void OnExportPngClick(object sender, RoutedEventArgs e)
	{
		var pixels = Compose();
		var bitmap = new WriteableBitmap(_canvasWidth, _canvasHeight, 96, 96, PixelFormats.Bgra32, null);
		bitmap.WritePixels(new Int32Rect(0, 0, _canvasWidth, _canvasHeight), pixels, _canvasWidth * BytesPerPixel, 0);

		Directory.CreateDirectory(AppPaths.DownloadsDir);

		var baseName = ExportFileNaming.Build(_presetName, _roleName, "cursor", _canvasWidth, _canvasHeight);
		var destPath = Path.Combine(AppPaths.DownloadsDir, $"{baseName}.png");
		var attempt = 1;

		while (File.Exists(destPath))
			destPath = Path.Combine(AppPaths.DownloadsDir, $"{baseName} ({attempt++}).png");

		var encoder = new PngBitmapEncoder();
		encoder.Frames.Add(BitmapFrame.Create(bitmap));

		using var stream = File.Create(destPath);
		encoder.Save(stream);

		if (AppState.GetOpenFolderAfterDownload())
			ExplorerService.RevealFile(destPath);
	}

	private void OnExportGifClick(object sender, RoutedEventArgs e)
	{
		if (!IsAnimated)
			return;

		_timelineFrames[_activeFrameIndex] = CaptureCurrentAsFrame();

		var frameBitmaps = new List<BitmapSource>(_timelineFrames.Count);
		var frameDelays = new List<int>(_timelineFrames.Count);

		foreach (var frame in _timelineFrames)
		{
			var buffer = new byte[_canvasWidth * _canvasHeight * BytesPerPixel];
			Blit(buffer, _canvasWidth, _canvasHeight, frame.SpriteBgra, frame.SpriteWidth, frame.SpriteHeight, frame.OffsetX, frame.OffsetY);

			var bitmap = new WriteableBitmap(_canvasWidth, _canvasHeight, 96, 96, PixelFormats.Bgra32, null);
			bitmap.WritePixels(new Int32Rect(0, 0, _canvasWidth, _canvasHeight), buffer, _canvasWidth * BytesPerPixel, 0);
			bitmap.Freeze();

			frameBitmaps.Add(bitmap);
			frameDelays.Add(frame.DurationMs);
		}

		Directory.CreateDirectory(AppPaths.DownloadsDir);

		var baseName = ExportFileNaming.Build(_presetName, _roleName, "cursor", _canvasWidth, _canvasHeight);
		var destPath = Path.Combine(AppPaths.DownloadsDir, $"{baseName}.gif");
		var attempt = 1;

		while (File.Exists(destPath))
			destPath = Path.Combine(AppPaths.DownloadsDir, $"{baseName} ({attempt++}).gif");

		AnimatedGifWriter.Save(destPath, frameBitmaps, frameDelays);

		if (AppState.GetOpenFolderAfterDownload())
			ExplorerService.RevealFile(destPath);
	}
}
