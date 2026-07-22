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
		new InfoHelpWindow(Loc.Get(LocInfoTitle), Loc.Get(LocInfoPaint)) { Owner = this }.ShowDialog();
	}

	private void OnCanvasSizeButtonClick(object sender, RoutedEventArgs e)
	{
		if (!_ready)
			return;

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
		var pixels = Compose();
		var hotspotX = Math.Clamp(_offsetX + _hotspotOffsetX, 0, _canvasWidth - 1);
		var hotspotY = Math.Clamp(_offsetY + _hotspotOffsetY, 0, _canvasHeight - 1);

		Result = new CursorCanvasImage(_canvasWidth, _canvasHeight, hotspotX, hotspotY, pixels);
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
}
