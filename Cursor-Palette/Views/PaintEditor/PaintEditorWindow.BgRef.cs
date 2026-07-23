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
	private const string FrameIndexFormat = "{0} / {1}";

	private List<BitmapSource> _bgRefFrames = new();
	private int _bgRefFrameIndex;
	private BitmapSource? _bgRefBitmap =>
		_bgRefFrames.Count > 0 ? _bgRefFrames[Math.Clamp(_bgRefFrameIndex, 0, _bgRefFrames.Count - 1)] : null;
	private double _bgRefOpacity = 50;
	private int _bgRefMargin;
	private int _bgRefOffsetX;
	private int _bgRefOffsetY;
	private bool _bgRefReady;
	private string? _bgRefCustomPath;
	private bool _bgRefBilinear;
	private bool _hideMainImage;

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

		_bgRefBilinear = AppState.GetBgRefBilinear();
		BgRefBilinearCheck.IsChecked = _bgRefBilinear;

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
		AppState.SetBgRefBilinear(_bgRefBilinear);
	}

	private static string ExpandSystemPath(string path)
	{
		if (string.IsNullOrEmpty(path))
			return path;

		var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

		return path.Replace(SystemRootVar, systemRoot, StringComparison.OrdinalIgnoreCase);
	}

	private void SetBgRefFrames(IReadOnlyList<BitmapSource> frames)
	{
		_bgRefFrames = new List<BitmapSource>(frames);
		_bgRefFrameIndex = 0;
		BgRefThumbnail.Source = _bgRefBitmap;

		UpdateBgRefFrameNav();
	}

	private void LoadDefaultRefImage()
	{
		if (string.IsNullOrEmpty(_roleName))
		{
			SetBgRefFrames(Array.Empty<BitmapSource>());
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
			SetBgRefFrames(Array.Empty<BitmapSource>());
			_bgRefCustomPath = null;

			UpdateBgRefRender();

			return;
		}

		defaultPath = ExpandSystemPath(defaultPath);

		if (!File.Exists(defaultPath))
		{
			SetBgRefFrames(Array.Empty<BitmapSource>());
			_bgRefCustomPath = null;

			UpdateBgRefRender();

			return;
		}

		SetBgRefFrames(LoadCursorAsBitmapFrames(defaultPath));
		_bgRefCustomPath = null;

		UpdateBgRefRender();
	}

	private static List<BitmapSource> LoadCursorAsBitmapFrames(string filePath)
	{
		var ext = Path.GetExtension(filePath);

		if (string.Equals(ext, AniExtension, StringComparison.OrdinalIgnoreCase))
		{
			var frames = AniCursorReader.Read(filePath);

			return frames != null ? new List<BitmapSource>(frames.Frames) : new List<BitmapSource>();
		}

		var image = CursorCanvasService.TryRead(filePath);

		if (image != null)
		{
			var bitmap = new WriteableBitmap(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null);

			bitmap.WritePixels(new Int32Rect(0, 0, image.Width, image.Height), image.Bgra, image.Width * 4, 0);
			bitmap.Freeze();

			return new List<BitmapSource> { bitmap };
		}

		var fallback = CursorCanvasService.TryReadAsBitmap(filePath);

		return fallback != null ? new List<BitmapSource> { fallback } : new List<BitmapSource>();
	}

	private void LoadCustomRefImage(string path)
	{
		try
		{
			var ext = Path.GetExtension(path);
			List<BitmapSource> frames;

			if (string.Equals(ext, CurExtension, StringComparison.OrdinalIgnoreCase) ||
				string.Equals(ext, AniExtension, StringComparison.OrdinalIgnoreCase))
			{
				frames = LoadCursorAsBitmapFrames(path);
			}
			else
			{
				var bmp = new BitmapImage();
				bmp.BeginInit();
				bmp.CacheOption = BitmapCacheOption.OnLoad;
				bmp.UriSource = new Uri(path);
				bmp.EndInit();
				bmp.Freeze();
				frames = new List<BitmapSource> { bmp };
			}

			if (frames.Count == 0)
				return;

			SetBgRefFrames(frames);
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
		RenderOptions.SetBitmapScalingMode(BgRefImage, _bgRefBilinear ? BitmapScalingMode.HighQuality : BitmapScalingMode.NearestNeighbor);

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

		_bgRefBilinear = false;
		BgRefBilinearCheck.IsChecked = _bgRefBilinear;

		UpdateBgRefRender();
	}

	private void OnBgRefBilinearClick(object sender, RoutedEventArgs e)
	{
		if (!_bgRefReady)
			return;

		_bgRefBilinear = BgRefBilinearCheck.IsChecked == true;

		UpdateBgRefRender();
	}

	private void OnToolBgRefClick(object sender, RoutedEventArgs e) =>
		SetTool(AppState.PaintEditorToolBgRef);

	private void OnHideMainImageClick(object sender, RoutedEventArgs e)
	{
		_hideMainImage = HideMainImageCheck.IsChecked == true;
		PreviewImage.Visibility = _hideMainImage ? Visibility.Hidden : Visibility.Visible;
	}

	private void UpdateBgRefManualControlsVisibility() =>
		BgRefFrameNavPanel.Visibility = _refManualMode && _bgRefFrames.Count > 1
			? Visibility.Visible
			: Visibility.Collapsed;

	private void UpdateBgRefFrameNav()
	{
		UpdateBgRefManualControlsVisibility();

		BgRefFrameIndexText.Text = string.Format(
			CultureInfo.InvariantCulture, FrameIndexFormat, _bgRefFrameIndex + 1, Math.Max(_bgRefFrames.Count, 1));

		BgRefFramePrevButton.IsEnabled = _bgRefFrameIndex > 0;
		BgRefFrameNextButton.IsEnabled = _bgRefFrameIndex < _bgRefFrames.Count - 1;
	}

	private void OnBgRefFramePrevClick(object sender, RoutedEventArgs e)
	{
		if (_bgRefFrameIndex <= 0)
			return;

		_bgRefFrameIndex--;
		BgRefThumbnail.Source = _bgRefBitmap;
		UpdateBgRefFrameNav();
		UpdateBgRefRender();
	}

	private void OnBgRefFrameNextClick(object sender, RoutedEventArgs e)
	{
		if (_bgRefFrameIndex >= _bgRefFrames.Count - 1)
			return;

		_bgRefFrameIndex++;
		BgRefThumbnail.Source = _bgRefBitmap;
		UpdateBgRefFrameNav();
		UpdateBgRefRender();
	}

	private void OnBgRefFrameResetClick(object sender, RoutedEventArgs e)
	{
		_bgRefFrameIndex = 0;
		BgRefThumbnail.Source = _bgRefBitmap;
		UpdateBgRefFrameNav();
		UpdateBgRefRender();
	}

	private void SyncRefFrameToTimeline()
	{
		if (_refManualMode || _bgRefFrames.Count <= 1 || _timelineFrames.Count <= 1)
			return;

		var ratio = _timelineFrames.Count > 1
			? (double)_activeFrameIndex / (_timelineFrames.Count - 1)
			: 0;

		var targetIndex = (int)Math.Round(ratio * (_bgRefFrames.Count - 1));

		if (targetIndex == _bgRefFrameIndex)
			return;

		_bgRefFrameIndex = Math.Clamp(targetIndex, 0, _bgRefFrames.Count - 1);
		BgRefThumbnail.Source = _bgRefBitmap;
		UpdateBgRefFrameNav();
		UpdateBgRefRender();
	}

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
