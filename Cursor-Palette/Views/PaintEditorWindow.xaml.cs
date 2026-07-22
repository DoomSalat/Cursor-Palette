using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class PaintEditorWindow : Window
{
	private const double MaxDisplaySize = 320;
	private const double MaxScaleFactor = 10;
	private const int MinCanvasDimension = 1;
	private const int MaxCanvasDimension = 256;
	private const int BytesPerPixel = 4;
	private const double UiZoomStep = 0.1;
	private const string CoordsFormat = "{0} × {1} px   X: {2}   Y: {3}";
	private const string StyleAccentButton = "Style.AccentButton";
	private const string StyleButton = "Style.Button";

	private const string LocInfoTitle = "S.Info.Title";
	private const string LocInfoPosition = "S.Info.Position";

	private readonly int _spriteWidth;
	private readonly int _spriteHeight;
	private readonly byte[] _spriteBgra;
	private readonly int _hotspotOffsetX;
	private readonly int _hotspotOffsetY;

	private int _canvasWidth;
	private int _canvasHeight;
	private int _offsetX;
	private int _offsetY;
	private double _scale;
	private bool _ready;

	public CursorCanvasImage? Result { get; private set; }

	public PaintEditorWindow(CursorCanvasImage source)
	{
		InitializeComponent();

		Width = AppState.GetPaintEditorWidth();
		Height = AppState.GetPaintEditorHeight();

		var uiScale = AppState.GetEditorUiScale();
		UiScaleTransform.ScaleX = uiScale;
		UiScaleTransform.ScaleY = uiScale;
		UiZoomText.Text = $"{(int)Math.Round(uiScale * 100)}%";

		var bounds = FindOpaqueBounds(source);

		_spriteWidth = bounds.Width;
		_spriteHeight = bounds.Height;
		_spriteBgra = ExtractRegion(source.Bgra, source.Width, bounds);
		_hotspotOffsetX = Math.Clamp(source.HotspotX - bounds.X, 0, _spriteWidth - 1);
		_hotspotOffsetY = Math.Clamp(source.HotspotY - bounds.Y, 0, _spriteHeight - 1);

		_canvasWidth = source.Width;
		_canvasHeight = source.Height;
		_offsetX = bounds.X;
		_offsetY = bounds.Y;

		CanvasWidthBox.Text = _canvasWidth.ToString(CultureInfo.InvariantCulture);
		CanvasHeightBox.Text = _canvasHeight.ToString(CultureInfo.InvariantCulture);

		var showBounds = AppState.GetShowSpriteBounds();
		ShowSpriteBoundsCheck.IsChecked = showBounds;
		SpriteBoundsRect.Visibility = showBounds ? Visibility.Visible : Visibility.Collapsed;

		_ready = true;

		RenderAll();
	}

	private readonly record struct PixelRect(int X, int Y, int Width, int Height);

	private static PixelRect FindOpaqueBounds(CursorCanvasImage source)
	{
		var minX = int.MaxValue;
		var minY = int.MaxValue;
		var maxX = int.MinValue;
		var maxY = int.MinValue;
		var stride = source.Width * BytesPerPixel;

		for (var y = 0; y < source.Height; y++)
		{
			for (var x = 0; x < source.Width; x++)
			{
				if (source.Bgra[y * stride + x * BytesPerPixel + 3] == 0)
					continue;

				if (x < minX) minX = x;
				if (x > maxX) maxX = x;
				if (y < minY) minY = y;
				if (y > maxY) maxY = y;
			}
		}

		return maxX < minX
			? new PixelRect(0, 0, source.Width, source.Height)
			: new PixelRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
	}

	private static byte[] ExtractRegion(byte[] source, int sourceWidth, PixelRect region)
	{
		var stride = sourceWidth * BytesPerPixel;
		var regionStride = region.Width * BytesPerPixel;
		var result = new byte[regionStride * region.Height];

		for (var y = 0; y < region.Height; y++)
			Array.Copy(source, (region.Y + y) * stride + region.X * BytesPerPixel, result, y * regionStride, regionStride);

		return result;
	}

	private static void Blit(byte[] dest, int destWidth, int destHeight, byte[] src, int srcWidth, int srcHeight, int offsetX, int offsetY)
	{
		for (var y = 0; y < srcHeight; y++)
		{
			var destY = y + offsetY;

			if (destY < 0 || destY >= destHeight)
				continue;

			for (var x = 0; x < srcWidth; x++)
			{
				var destX = x + offsetX;

				if (destX < 0 || destX >= destWidth)
					continue;

				var srcIndex = (y * srcWidth + x) * BytesPerPixel;
				var destIndex = (destY * destWidth + destX) * BytesPerPixel;

				dest[destIndex] = src[srcIndex];
				dest[destIndex + 1] = src[srcIndex + 1];
				dest[destIndex + 2] = src[srcIndex + 2];
				dest[destIndex + 3] = src[srcIndex + 3];
			}
		}
	}

	private byte[] Compose()
	{
		var buffer = new byte[_canvasWidth * _canvasHeight * BytesPerPixel];
		Blit(buffer, _canvasWidth, _canvasHeight, _spriteBgra, _spriteWidth, _spriteHeight, _offsetX, _offsetY);

		return buffer;
	}

	private static double ComputeScale(int width, int height)
	{
		var factor = Math.Min(MaxScaleFactor, Math.Min(MaxDisplaySize / width, MaxDisplaySize / height));

		return Math.Max(1, factor);
	}

	private (int Min, int Max) HorizontalRange() =>
		(Math.Min(0, _canvasWidth - _spriteWidth), Math.Max(0, _canvasWidth - _spriteWidth));

	private (int Min, int Max) VerticalRange() =>
		(Math.Min(0, _canvasHeight - _spriteHeight), Math.Max(0, _canvasHeight - _spriteHeight));

	private void RenderAll()
	{
		_scale = ComputeScale(_canvasWidth, _canvasHeight);

		var displayWidth = _canvasWidth * _scale;
		var displayHeight = _canvasHeight * _scale;

		PreviewCanvas.Width = displayWidth;
		PreviewCanvas.Height = displayHeight;
		PreviewImage.Width = displayWidth;
		PreviewImage.Height = displayHeight;
		RenderOptions.SetBitmapScalingMode(PreviewImage, BitmapScalingMode.NearestNeighbor);

		var bitmap = new WriteableBitmap(_canvasWidth, _canvasHeight, 96, 96, PixelFormats.Bgra32, null);
		var pixels = Compose();
		bitmap.WritePixels(new Int32Rect(0, 0, _canvasWidth, _canvasHeight), pixels, _canvasWidth * BytesPerPixel, 0);
		PreviewImage.Source = bitmap;

		UpdateSpriteBoundsRect();
		UpdateCoordsText();
		UpdateMoveButtonsEnabled();
		UpdateSnapHighlight();
	}

	private void UpdateCoordsText() =>
		CoordsText.Text = string.Format(CoordsFormat, _canvasWidth, _canvasHeight, _offsetX, _offsetY);

	private void UpdateMoveButtonsEnabled()
	{
		var (minX, maxX) = HorizontalRange();
		var (minY, maxY) = VerticalRange();

		MoveLeftButton.IsEnabled = _offsetX > minX;
		MoveRightButton.IsEnabled = _offsetX < maxX;
		MoveUpButton.IsEnabled = _offsetY > minY;
		MoveDownButton.IsEnabled = _offsetY < maxY;
	}

	private void UpdateSnapHighlight()
	{
		var (minX, maxX) = HorizontalRange();
		var (minY, maxY) = VerticalRange();

		foreach (var child in SnapGrid.Children)
		{
			if (child is not Button button)
				continue;

			var (fractionX, fractionY) = ParseFraction(button);
			var targetX = SnapOffset(fractionX, minX, maxX);
			var targetY = SnapOffset(fractionY, minY, maxY);
			var isCurrent = targetX == _offsetX && targetY == _offsetY;

			button.Style = (Style)Application.Current.Resources[isCurrent ? StyleAccentButton : StyleButton];
		}
	}

	private static int SnapOffset(double fraction, int min, int max) =>
		fraction == 0 ? min : fraction == 1 ? max : (min + max) / 2;

	private static (double X, double Y) ParseFraction(Button button)
	{
		var parts = ((string)button.Tag).Split(',');

		return (
			double.Parse(parts[0], CultureInfo.InvariantCulture),
			double.Parse(parts[1], CultureInfo.InvariantCulture));
	}

	private void OnInfoButtonClick(object sender, RoutedEventArgs e)
	{
		new InfoHelpWindow(Loc.Get(LocInfoTitle), Loc.Get(LocInfoPosition)) { Owner = this }.ShowDialog();
	}

	private void OnMoveLeftClick(object sender, RoutedEventArgs e)
	{
		var (min, _) = HorizontalRange();
		_offsetX = Math.Max(min, _offsetX - 1);
		RenderAll();
	}

	private void OnMoveRightClick(object sender, RoutedEventArgs e)
	{
		var (_, max) = HorizontalRange();
		_offsetX = Math.Min(max, _offsetX + 1);
		RenderAll();
	}

	private void OnMoveUpClick(object sender, RoutedEventArgs e)
	{
		var (min, _) = VerticalRange();
		_offsetY = Math.Max(min, _offsetY - 1);
		RenderAll();
	}

	private void OnMoveDownClick(object sender, RoutedEventArgs e)
	{
		var (_, max) = VerticalRange();
		_offsetY = Math.Min(max, _offsetY + 1);
		RenderAll();
	}

	private void OnSnapClick(object sender, RoutedEventArgs e)
	{
		var (fractionX, fractionY) = ParseFraction((Button)sender);
		var (minX, maxX) = HorizontalRange();
		var (minY, maxY) = VerticalRange();

		_offsetX = SnapOffset(fractionX, minX, maxX);
		_offsetY = SnapOffset(fractionY, minY, maxY);

		RenderAll();
	}

	private void OnApplyCanvasSizeClick(object sender, RoutedEventArgs e)
	{
		if (!_ready)
			return;

		var width = ParseDimension(CanvasWidthBox.Text, _canvasWidth);
		var height = ParseDimension(CanvasHeightBox.Text, _canvasHeight);

		_canvasWidth = width;
		_canvasHeight = height;

		var (minX, maxX) = HorizontalRange();
		var (minY, maxY) = VerticalRange();
		_offsetX = Math.Clamp(_offsetX, minX, maxX);
		_offsetY = Math.Clamp(_offsetY, minY, maxY);

		CanvasWidthBox.Text = _canvasWidth.ToString(CultureInfo.InvariantCulture);
		CanvasHeightBox.Text = _canvasHeight.ToString(CultureInfo.InvariantCulture);

		RenderAll();
	}

	private static int ParseDimension(string text, int fallback) =>
		int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
			? Math.Clamp(value, MinCanvasDimension, MaxCanvasDimension)
			: fallback;

	private void UpdateSpriteBoundsRect()
	{
		if (SpriteBoundsRect.Visibility != Visibility.Visible)
			return;

		var x = _offsetX * _scale;
		var y = _offsetY * _scale;
		var w = _spriteWidth * _scale;
		var h = _spriteHeight * _scale;

		Canvas.SetLeft(SpriteBoundsRect, x);
		Canvas.SetTop(SpriteBoundsRect, y);
		SpriteBoundsRect.Width = w;
		SpriteBoundsRect.Height = h;
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

	private void OnUiZoomOutClick(object sender, RoutedEventArgs e) => AdjustUiZoom(-UiZoomStep);
	private void OnUiZoomInClick(object sender, RoutedEventArgs e) => AdjustUiZoom(UiZoomStep);

	private void AdjustUiZoom(double delta)
	{
		var scale = Math.Clamp(Math.Round(AppState.GetEditorUiScale() + delta, 2), AppState.EditorUiScaleMin, AppState.EditorUiScaleMax);
		UiScaleTransform.ScaleX = scale;
		UiScaleTransform.ScaleY = scale;
		UiZoomText.Text = $"{(int)Math.Round(scale * 100)}%";
		AppState.SetEditorUiScale(scale);
	}

	private void OnExportPngClick(object sender, RoutedEventArgs e)
	{
		var pixels = Compose();
		var bitmap = new WriteableBitmap(_canvasWidth, _canvasHeight, 96, 96, PixelFormats.Bgra32, null);
		bitmap.WritePixels(new Int32Rect(0, 0, _canvasWidth, _canvasHeight), pixels, _canvasWidth * BytesPerPixel, 0);

		Directory.CreateDirectory(AppPaths.DownloadsDir);

		var fileName = $"cursor_{_canvasWidth}x{_canvasHeight}.png";
		var destPath = Path.Combine(AppPaths.DownloadsDir, fileName);
		var attempt = 1;

		while (File.Exists(destPath))
			destPath = Path.Combine(AppPaths.DownloadsDir, $"cursor_{_canvasWidth}x{_canvasHeight} ({attempt++}).png");

		var encoder = new PngBitmapEncoder();
		encoder.Frames.Add(BitmapFrame.Create(bitmap));

		using var stream = File.Create(destPath);
		encoder.Save(stream);

		Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{destPath}\"") { UseShellExecute = true });
	}

	protected override void OnClosed(EventArgs e)
	{
		AppState.SetPaintEditorSize(Width, Height);
		base.OnClosed(e);
	}
}
