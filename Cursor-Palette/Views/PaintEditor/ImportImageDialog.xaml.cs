using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CursorPalette.Views;

public partial class ImportImageDialog : Window
{
	private const string PngExtension = ".png";
	private const string JpgExtension = ".jpg";
	private const string JpegExtension = ".jpeg";
	private const string BmpExtension = ".bmp";
	private const string GifExtension = ".gif";
	private const string CurExtension = ".cur";
	private const string AniExtension = ".ani";
	private const string PixelsFormat = "{0} × {1} px";
	private const int SmallImageThreshold = 64;
	private const int BgraBytesPerPixel = 4;
	private const int BitmapDpi = 96;
	private const string AccentButtonStyleKey = "Style.AccentButton";
	private const string ButtonStyleKey = "Style.Button";

	public enum ImportMode { Over, Replace }

	public ImportMode ResultMode { get; private set; } = ImportMode.Over;
	public BitmapSource? Image { get; private set; }

	public ImportImageDialog(BitmapSource? preview)
	{
		InitializeComponent();

		if (preview != null)
		{
			Image = preview;
			PreviewImage.Source = preview;
			ImageInfoText.Text = string.Format(PixelsFormat, preview.PixelWidth, preview.PixelHeight);

			UpdateScalingMode(preview);
		}

		AllowDrop = true;

		PreviewDragEnter += OnDragEnter;
		PreviewDragOver += OnDragOver;
		PreviewDragLeave += OnDragLeave;
		Drop += OnDrop;
	}

	private static bool IsImageFile(string path)
	{
		var extension = System.IO.Path.GetExtension(path);

		return !string.IsNullOrEmpty(extension) && (
			string.Equals(extension, PngExtension, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(extension, JpgExtension, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(extension, JpegExtension, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(extension, BmpExtension, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(extension, GifExtension, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(extension, CurExtension, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(extension, AniExtension, StringComparison.OrdinalIgnoreCase));
	}

	private bool HasImageDrop(DragEventArgs e)
	{
		if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
			return false;

		return paths.Any(p => System.IO.File.Exists(p) && IsImageFile(p));
	}

	private void OnDragEnter(object sender, DragEventArgs e)
	{
		if (HasImageDrop(e))
		{
			e.Effects = DragDropEffects.Copy;
			DropIndicator.Visibility = Visibility.Visible;
		}
		else
		{
			e.Effects = DragDropEffects.None;
		}

		e.Handled = true;
	}

	private void OnDragOver(object sender, DragEventArgs e)
	{
		e.Effects = HasImageDrop(e) ? DragDropEffects.Copy : DragDropEffects.None;
		e.Handled = true;
	}

	private void OnDragLeave(object sender, DragEventArgs e)
	{
		DropIndicator.Visibility = Visibility.Collapsed;
		e.Handled = true;
	}

	private void OnDrop(object sender, DragEventArgs e)
	{
		DropIndicator.Visibility = Visibility.Collapsed;

		if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
			return;

		var path = paths.FirstOrDefault(p => System.IO.File.Exists(p) && IsImageFile(p));

		if (path == null)
			return;

		LoadImage(path);
		e.Handled = true;
	}

	internal void LoadImage(string path)
	{
		try
		{
			BitmapSource? bitmap = null;
			var extension = System.IO.Path.GetExtension(path);

			if (string.Equals(extension, CurExtension, StringComparison.OrdinalIgnoreCase) ||
				string.Equals(extension, AniExtension, StringComparison.OrdinalIgnoreCase))
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

			Image = bitmap;
			PreviewImage.Source = bitmap;
			ImageInfoText.Text = string.Format(PixelsFormat, bitmap.PixelWidth, bitmap.PixelHeight);
			UpdateScalingMode(bitmap);
		}
		catch
		{
		}
	}

	private static BitmapSource? LoadCursorAsBitmap(string filePath)
	{
		var extension = System.IO.Path.GetExtension(filePath);

		if (string.Equals(extension, AniExtension, StringComparison.OrdinalIgnoreCase))
		{
			var frames = Services.AniCursorReader.Read(filePath);
			return frames?.Frames.Count > 0 ? frames.Frames[0] : null;
		}

		var image = Services.CursorCanvasService.TryRead(filePath);

		if (image != null)
		{
			var bmp = new WriteableBitmap(image.Width, image.Height, BitmapDpi, BitmapDpi, PixelFormats.Bgra32, null);
			bmp.WritePixels(new Int32Rect(0, 0, image.Width, image.Height), image.Bgra, image.Width * BgraBytesPerPixel, 0);
			bmp.Freeze();

			return bmp;
		}

		return Services.CursorCanvasService.TryReadAsBitmap(filePath);
	}

	private void UpdateScalingMode(BitmapSource bitmap)
	{
		if (bitmap.PixelWidth <= SmallImageThreshold || bitmap.PixelHeight <= SmallImageThreshold)
			RenderOptions.SetBitmapScalingMode(PreviewImage, BitmapScalingMode.NearestNeighbor);
		else
			RenderOptions.SetBitmapScalingMode(PreviewImage, BitmapScalingMode.HighQuality);
	}

	private bool _isReplaceMode = true;

	private void OnModeButtonClick(object sender, RoutedEventArgs e)
	{
		if (sender == ModeOverButton)
		{
			_isReplaceMode = false;
			ModeOverButton.Style = (Style)FindResource(AccentButtonStyleKey);
			ModeReplaceButton.Style = (Style)FindResource(ButtonStyleKey);
		}
		else
		{
			_isReplaceMode = true;
			ModeReplaceButton.Style = (Style)FindResource(AccentButtonStyleKey);
			ModeOverButton.Style = (Style)FindResource(ButtonStyleKey);
		}
	}

	private void OnOkClick(object sender, RoutedEventArgs e)
	{
		if (Image == null)
			return;

		ResultMode = _isReplaceMode ? ImportMode.Replace : ImportMode.Over;
		DialogResult = true;
	}
}
