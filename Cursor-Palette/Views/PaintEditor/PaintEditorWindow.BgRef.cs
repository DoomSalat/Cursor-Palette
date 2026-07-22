using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CursorPalette.Services;
using Microsoft.Win32;

namespace CursorPalette.Views;

public partial class PaintEditorWindow
{
	private const string CurExtension = ".cur";
	private const string AniExtension = ".ani";
	private const string PngExtension = ".png";
	private const string JpgExtension = ".jpg";
	private const string JpegExtension = ".jpeg";
	private const string BmpExtension = ".bmp";
	private const string GifExtension = ".gif";
	private const string SystemRootVar = "%SystemRoot%";
	private const string PngFileDialogFilter = "PNG files (*.png)|*.png|All files (*.*)|*.*";
	private const string LoadRefDialogTitle = "Load reference image";
	private const double DefaultBgRefOpacity = 50;
	private const int DefaultBgRefMargin = 0;
	private const int DefaultBgRefOffsetX = 0;
	private const int DefaultBgRefOffsetY = 0;

	private BitmapSource? _bgRefBitmap;
	private double _bgRefOpacity = 50;
	private int _bgRefMargin;
	private int _bgRefOffsetX;
	private int _bgRefOffsetY;
	private bool _bgRefReady;
	private string? _bgRefCustomPath;

	private void InitBgRef()
	{
		_bgRefOpacity = AppState.GetBgRefOpacity();
		_bgRefMargin = AppState.GetBgRefMargin();
		var (offsetX, offsetY) = AppState.GetBgRefOffset();
		_bgRefOffsetX = offsetX;
		_bgRefOffsetY = offsetY;

		BgRefOpacitySlider.Value = _bgRefOpacity;
		BgRefMarginSlider.Value = _bgRefMargin;
		BgRefOffsetXBox.Text = _bgRefOffsetX.ToString(CultureInfo.InvariantCulture);
		BgRefOffsetYBox.Text = _bgRefOffsetY.ToString(CultureInfo.InvariantCulture);

		LoadDefaultRefImage();
		_bgRefReady = true;

		var customPath = AppState.GetBgRefCustomPath();
		if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
			LoadCustomRefImage(customPath);

		DropZoneService.Attach(
			BgRefDropBorder,
			BgRefDropIndicator,
			e => DropZoneService.GetFirstFile(e, IsImageFile) != null,
			files =>
			{
				var path = files.FirstOrDefault(IsImageFile);
				if (path != null)
					LoadCustomRefImage(path);
			});
	}

	private void SaveBgRefSettings()
	{
		AppState.SetBgRefOpacity(_bgRefOpacity);
		AppState.SetBgRefMargin(_bgRefMargin);
		AppState.SetBgRefOffset(_bgRefOffsetX, _bgRefOffsetY);
		AppState.SetBgRefCustomPath(_bgRefCustomPath);
	}

	private static string ExpandSystemPath(string path)
	{
		if (string.IsNullOrEmpty(path))
			return path;

		var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

		return path.Replace(SystemRootVar, systemRoot, StringComparison.OrdinalIgnoreCase);
	}

	private void LoadDefaultRefImage()
	{
		if (string.IsNullOrEmpty(_roleName))
		{
			_bgRefBitmap = null;
			BgRefThumbnail.Source = null;
			_bgRefCustomPath = null;

			UpdateBgRefRender();

			return;
		}

		var systemDefaults = RegistryCursorService.GetWindowsDefaultValues();
		var defaultPath = systemDefaults.GetValueOrDefault(_roleName);

		if (string.IsNullOrWhiteSpace(defaultPath))
			defaultPath = PlaceholderCursorDefaults.GetPath(_roleName);

		if (string.IsNullOrWhiteSpace(defaultPath))
		{
			_bgRefBitmap = null;
			BgRefThumbnail.Source = null;
			_bgRefCustomPath = null;

			UpdateBgRefRender();

			return;
		}

		defaultPath = ExpandSystemPath(defaultPath);

		if (!File.Exists(defaultPath))
		{
			_bgRefBitmap = null;
			BgRefThumbnail.Source = null;
			_bgRefCustomPath = null;

			UpdateBgRefRender();

			return;
		}

		_bgRefBitmap = LoadCursorAsBitmap(defaultPath);
		BgRefThumbnail.Source = _bgRefBitmap;
		_bgRefCustomPath = null;

		UpdateBgRefRender();
	}

	private static BitmapSource? LoadCursorAsBitmap(string filePath)
	{
		var ext = Path.GetExtension(filePath);

		if (string.Equals(ext, AniExtension, StringComparison.OrdinalIgnoreCase))
		{
			var frames = AniCursorReader.Read(filePath);

			return frames?.Frames.Count > 0 ? frames.Frames[0] : null;
		}

		var image = CursorCanvasService.TryRead(filePath);

		if (image != null)
		{
			var bitmap = new WriteableBitmap(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null);

			bitmap.WritePixels(new Int32Rect(0, 0, image.Width, image.Height), image.Bgra, image.Width * 4, 0);
			bitmap.Freeze();

			return bitmap;
		}

		return CursorCanvasService.TryReadAsBitmap(filePath);
	}

	private void LoadCustomRefImage(string path)
	{
		try
		{
			var ext = Path.GetExtension(path);
			BitmapSource? bitmap;

			if (string.Equals(ext, CurExtension, StringComparison.OrdinalIgnoreCase) ||
				string.Equals(ext, AniExtension, StringComparison.OrdinalIgnoreCase))
			{
				bitmap = LoadCursorAsBitmap(path);
			}
			else
			{
				var bmp = new BitmapImage();
				bmp.BeginInit();
				bmp.CacheOption = BitmapCacheOption.OnLoad;
				bmp.UriSource = new Uri(path);
				bmp.EndInit();
				bmp.Freeze();
				bitmap = bmp;
			}

			if (bitmap == null)
				return;

			_bgRefBitmap = bitmap;
			BgRefThumbnail.Source = bitmap;
			_bgRefCustomPath = path;

			UpdateBgRefRender();
		}
		catch
		{
		}
	}

	internal void UpdateBgRefRender()
	{
		if (_bgRefBitmap == null)
		{
			BgRefImage.Visibility = Visibility.Collapsed;
			return;
		}

		BgRefImage.Visibility = Visibility.Visible;

		BgRefImage.Source = _bgRefBitmap;
		RenderOptions.SetBitmapScalingMode(BgRefImage, BitmapScalingMode.HighQuality);

		var refW = (double)_bgRefBitmap.PixelWidth;
		var refH = (double)_bgRefBitmap.PixelHeight;

		if (refW <= 0 || refH <= 0)
		{
			BgRefImage.Visibility = Visibility.Collapsed;
			return;
		}

		var availW = Math.Max(0, _canvasWidth - 2 * _bgRefMargin);
		var availH = Math.Max(0, _canvasHeight - 2 * _bgRefMargin);

		double displayW, displayH;

		if (refW <= availW && refH <= availH)
		{
			displayW = refW;
			displayH = refH;
		}
		else
		{
			var scale = Math.Min(availW / refW, availH / refH);
			displayW = refW * scale;
			displayH = refH * scale;
		}

		var posX = (_canvasWidth - displayW) / 2.0 + _bgRefOffsetX;
		var posY = (_canvasHeight - displayH) / 2.0 + _bgRefOffsetY;

		Canvas.SetLeft(BgRefImage, posX);
		Canvas.SetTop(BgRefImage, posY);
		BgRefImage.Width = displayW;
		BgRefImage.Height = displayH;
		BgRefImage.Opacity = _bgRefOpacity / 100.0;

		var clipX = Math.Max(0, -posX);
		var clipY = Math.Max(0, -posY);
		var clipW = Math.Min(displayW - clipX, _canvasWidth - Math.Max(0, posX));
		var clipH = Math.Min(displayH - clipY, _canvasHeight - Math.Max(0, posY));

		if (clipW <= 0 || clipH <= 0)
		{
			BgRefImage.Visibility = Visibility.Collapsed;
			return;
		}

		BgRefImage.Clip = new RectangleGeometry(new Rect(clipX, clipY, clipW, clipH));
	}

	private void OnBgRefOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_bgRefReady)
			return;

		_bgRefOpacity = e.NewValue;

		UpdateBgRefRender();
	}

	private void OnBgRefMarginChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_bgRefReady)
			return;

		_bgRefMargin = (int)Math.Round(e.NewValue);

		UpdateBgRefRender();
	}

	private void OnBgRefOffsetXChanged(object sender, TextChangedEventArgs e)
	{
		if (!_bgRefReady)
			return;

		if (int.TryParse(BgRefOffsetXBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
		{
			_bgRefOffsetX = value;

			UpdateBgRefRender();
		}
	}

	private void OnBgRefOffsetYChanged(object sender, TextChangedEventArgs e)
	{
		if (!_bgRefReady)
			return;

		if (int.TryParse(BgRefOffsetYBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
		{
			_bgRefOffsetY = value;

			UpdateBgRefRender();
		}
	}

	private void OnBgRefLoadClick(object sender, RoutedEventArgs e)
	{
		var dialog = new OpenFileDialog
		{
			Filter = PngFileDialogFilter,
			Title = LoadRefDialogTitle
		};

		if (dialog.ShowDialog() == true)
			LoadCustomRefImage(dialog.FileName);
	}

	private void OnBgRefResetClick(object sender, RoutedEventArgs e)
	{
		LoadDefaultRefImage();
	}

	private void OnBgRefResetSettingsClick(object sender, RoutedEventArgs e)
	{
		_bgRefOpacity = DefaultBgRefOpacity;
		_bgRefMargin = DefaultBgRefMargin;
		_bgRefOffsetX = DefaultBgRefOffsetX;
		_bgRefOffsetY = DefaultBgRefOffsetY;

		BgRefOpacitySlider.Value = _bgRefOpacity;
		BgRefMarginSlider.Value = _bgRefMargin;
		BgRefOffsetXBox.Text = _bgRefOffsetX.ToString(CultureInfo.InvariantCulture);
		BgRefOffsetYBox.Text = _bgRefOffsetY.ToString(CultureInfo.InvariantCulture);

		UpdateBgRefRender();
	}

	private void OnToolBgRefClick(object sender, RoutedEventArgs e) =>
		SetTool(AppState.PaintEditorToolBgRef);

	private static bool IsImageFile(string path)
	{
		var ext = Path.GetExtension(path);

		return string.Equals(ext, PngExtension, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(ext, JpgExtension, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(ext, JpegExtension, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(ext, BmpExtension, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(ext, GifExtension, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(ext, CurExtension, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(ext, AniExtension, StringComparison.OrdinalIgnoreCase);
	}
}
