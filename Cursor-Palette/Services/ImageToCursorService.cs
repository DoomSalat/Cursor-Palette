using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CursorPalette.Services;

public static class ImageToCursorService
{
	private const string CurExtension = ".cur";
	private const string AniExtension = ".ani";
	private const string PngExtension = ".png";
	private const string JpgExtension = ".jpg";
	private const string JpegExtension = ".jpeg";
	private const string BmpExtension = ".bmp";
	private const string GifExtension = ".gif";

	private const int BytesPerPixel = 4;
	private const int MaxDimension = 256;
	private const int MaxFrames = 60;
	private const int DefaultGifFrameDelayMs = 100;
	private const int DefaultGifFrameDelayCentiseconds = 10;
	private const int CentisecondsToMs = 10;
	private const int MinFrameDurationMs = 20;
	private const int MaxFrameDurationMs = 10000;

	private const string TempFilePrefix = "cursor-palette-convert-";

	private sealed record GifRawFrame(BitmapSource Bitmap, int Left, int Top, int DelayMs, int Disposal);

	public static bool IsImageFile(string path)
	{
		var ext = Path.GetExtension(path);

		return !string.IsNullOrEmpty(ext) && (
			string.Equals(ext, PngExtension, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(ext, JpgExtension, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(ext, JpegExtension, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(ext, BmpExtension, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(ext, GifExtension, StringComparison.OrdinalIgnoreCase));
	}

	public static bool IsCursorFile(string path)
	{
		var ext = Path.GetExtension(path);

		return !string.IsNullOrEmpty(ext) && (
			string.Equals(ext, CurExtension, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(ext, AniExtension, StringComparison.OrdinalIgnoreCase));
	}

	public static bool IsConvertibleFile(string path)
	{
		var ext = Path.GetExtension(path);

		return !string.IsNullOrEmpty(ext) && (
			IsImageFile(path) ||
			IsCursorFile(path));
	}

	public static string? ConvertToCursorTempFile(string path)
	{
		var ext = Path.GetExtension(path).ToLowerInvariant();

		if (ext is CurExtension or AniExtension)
			return path;

		if (ext == GifExtension)
		{
			var aniPath = TryConvertAnimatedGif(path);
			if (aniPath != null)
				return aniPath;
		}

		return TryConvertStaticImage(path);
	}

	public static bool IsFullyTransparent(string path)
	{
		var ext = Path.GetExtension(path).ToLowerInvariant();

		if (ext == CurExtension)
		{
			var image = CursorCanvasService.TryRead(path);
			if (image == null)
				return false;

			for (var i = 3; i < image.Bgra.Length; i += BytesPerPixel)
			{
				if (image.Bgra[i] != 0)
					return false;
			}

			return true;
		}

		if (ext == AniExtension)
		{
			var frames = AniCursorReader.Read(path);
			if (frames == null || frames.Frames.Count == 0)
				return false;

			foreach (var frame in frames.Frames)
			{
				if (IsBitmapVisible(frame))
					return false;
			}

			return true;
		}

		if (ext == GifExtension)
		{
			var decoded = DecodeGifRawFrames(path);
			if (decoded != null && decoded.Value.Frames.Count > 1)
			{
				var composed = ComposeGifFrames(decoded.Value.Width, decoded.Value.Height, decoded.Value.Frames);
				foreach (var (bitmap, _) in composed)
				{
					if (IsBitmapVisible(bitmap))
						return false;
				}

				return true;
			}
		}

		if (IsImageFile(path))
		{
			var bitmap = LoadBitmap(path);
			if (bitmap == null)
				return false;

			return !IsBitmapVisible(bitmap);
		}

		return false;
	}

	private static string? TryConvertStaticImage(string path)
	{
		var bitmap = LoadBitmap(path);
		if (bitmap == null)
			return null;

		var width = Math.Clamp(bitmap.PixelWidth, 1, MaxDimension);
		var height = Math.Clamp(bitmap.PixelHeight, 1, MaxDimension);
		var bgra = BitmapToBgra(bitmap, bitmap.PixelWidth, bitmap.PixelHeight);

		if (bitmap.PixelWidth != width || bitmap.PixelHeight != height)
			bgra = CropBgra(bgra, bitmap.PixelWidth, bitmap.PixelHeight, width, height);

		var image = new CursorCanvasImage(width, height, 0, 0, bgra);
		var tempPath = Path.Combine(Path.GetTempPath(), $"{TempFilePrefix}{Guid.NewGuid():N}{CurExtension}");
		CursorCanvasService.Write(tempPath, image);

		return tempPath;
	}

	private static string? TryConvertAnimatedGif(string path)
	{
		var decoded = DecodeGifRawFrames(path);
		if (decoded == null || decoded.Value.Frames.Count <= 1)
			return null;

		var (width, height, rawFrames) = decoded.Value;
		var composed = ComposeGifFrames(width, height, rawFrames);

		var clampedWidth = Math.Clamp(width, 1, MaxDimension);
		var clampedHeight = Math.Clamp(height, 1, MaxDimension);

		var frames = new List<CursorCanvasImage>(composed.Count);
		var delays = new List<int>(composed.Count);

		foreach (var (bitmap, delayMs) in composed)
		{
			var bgra = BitmapToBgra(bitmap, width, height);

			if (width != clampedWidth || height != clampedHeight)
				bgra = CropBgra(bgra, width, height, clampedWidth, clampedHeight);

			var duration = Math.Clamp(delayMs, MinFrameDurationMs, MaxFrameDurationMs);
			frames.Add(new CursorCanvasImage(clampedWidth, clampedHeight, 0, 0, bgra));
			delays.Add(duration);
		}

		var tempPath = Path.Combine(Path.GetTempPath(), $"{TempFilePrefix}{Guid.NewGuid():N}{AniExtension}");
		AniCursorWriter.Save(tempPath, frames, delays);

		return tempPath;
	}

	private static BitmapSource? LoadBitmap(string path)
	{
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

	private static byte[] CropBgra(byte[] source, int srcWidth, int srcHeight, int dstWidth, int dstHeight)
	{
		var destination = new byte[dstWidth * dstHeight * BytesPerPixel];

		for (var y = 0; y < dstHeight; y++)
		{
			for (var x = 0; x < dstWidth; x++)
			{
				var sourceIndex = (y * srcWidth + x) * BytesPerPixel;
				var destinationIndex = (y * dstWidth + x) * BytesPerPixel;
				destination[destinationIndex] = source[sourceIndex];
				destination[destinationIndex + 1] = source[sourceIndex + 1];
				destination[destinationIndex + 2] = source[sourceIndex + 2];
				destination[destinationIndex + 3] = source[sourceIndex + 3];
			}
		}

		return destination;
	}

	private static bool IsBitmapVisible(BitmapSource bitmap)
	{
		var width = bitmap.PixelWidth;
		var height = bitmap.PixelHeight;

		if (width == 0 || height == 0)
			return false;

		var stride = width * BytesPerPixel;
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

		for (var i = 3; i < pixels.Length; i += BytesPerPixel)
		{
			if (pixels[i] != 0)
				return true;
		}

		return false;
	}

	private static (int Width, int Height, List<GifRawFrame> Frames)? DecodeGifRawFrames(string path)
	{
		try
		{
			using var stream = File.OpenRead(path);
			var decoder = new GifBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

			if (decoder.Frames.Count == 0)
				return null;

			var rawFrames = new List<GifRawFrame>(decoder.Frames.Count);
			var canvasWidth = 0;
			var canvasHeight = 0;

			for (var i = 0; i < decoder.Frames.Count; i++)
			{
				var frame = decoder.Frames[i];
				var metadata = frame.Metadata as BitmapMetadata;

				var left = 0;
				var top = 0;
				var delayCentiseconds = DefaultGifFrameDelayCentiseconds;
				var disposal = 0;

				if (metadata != null)
				{
					TryGetQueryInt(metadata, "/imgdesc/Left", out left);
					TryGetQueryInt(metadata, "/imgdesc/Top", out top);
					TryGetQueryInt(metadata, "/grctlext/Delay", out delayCentiseconds);
					TryGetQueryInt(metadata, "/grctlext/Disposal", out disposal);

					if (canvasWidth == 0)
					{
						TryGetQueryInt(metadata, "/logscrdesc/Width", out canvasWidth);
						TryGetQueryInt(metadata, "/logscrdesc/Height", out canvasHeight);
					}
				}

				var bitmap = frame.Format == PixelFormats.Bgra32
					? (BitmapSource)frame
					: new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);

				if (bitmap.CanFreeze)
					bitmap.Freeze();

				var delayMs = delayCentiseconds > 0 ? delayCentiseconds * CentisecondsToMs : DefaultGifFrameDelayMs;

				rawFrames.Add(new GifRawFrame(bitmap, left, top, delayMs, disposal));

				canvasWidth = Math.Max(canvasWidth, left + frame.PixelWidth);
				canvasHeight = Math.Max(canvasHeight, top + frame.PixelHeight);
			}

			return (canvasWidth, canvasHeight, rawFrames);
		}
		catch
		{
			return null;
		}
	}

	private static List<(BitmapSource Bitmap, int DelayMs)> ComposeGifFrames(int width, int height, List<GifRawFrame> rawFrames)
	{
		var canvas = new byte[width * height * BytesPerPixel];
		var result = new List<(BitmapSource Bitmap, int DelayMs)>(rawFrames.Count);

		foreach (var raw in rawFrames)
		{
			var restoreSnapshot = raw.Disposal == 3 ? (byte[])canvas.Clone() : null;

			var frameBgra = BitmapToBgra(raw.Bitmap, raw.Bitmap.PixelWidth, raw.Bitmap.PixelHeight);
			AlphaComposite(canvas, width, height, frameBgra, raw.Bitmap.PixelWidth, raw.Bitmap.PixelHeight, raw.Left, raw.Top);

			var composedCopy = (byte[])canvas.Clone();
			var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
			bitmap.WritePixels(new Int32Rect(0, 0, width, height), composedCopy, width * BytesPerPixel, 0);
			bitmap.Freeze();

			result.Add((bitmap, raw.DelayMs));

			if (raw.Disposal == 2)
				ClearRect(canvas, width, height, raw.Left, raw.Top, raw.Bitmap.PixelWidth, raw.Bitmap.PixelHeight);
			else if (raw.Disposal == 3 && restoreSnapshot != null)
				canvas = restoreSnapshot;
		}

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

	private static void ClearRect(byte[] buffer, int width, int height, int x, int y, int rectWidth, int rectHeight)
	{
		for (var yy = Math.Max(y, 0); yy < y + rectHeight && yy < height; yy++)
		{
			for (var xx = Math.Max(x, 0); xx < x + rectWidth && xx < width; xx++)
			{
				var index = (yy * width + xx) * BytesPerPixel;
				buffer[index] = 0;
				buffer[index + 1] = 0;
				buffer[index + 2] = 0;
				buffer[index + 3] = 0;
			}
		}
	}

	private static bool TryGetQueryInt(BitmapMetadata metadata, string query, out int value)
	{
		value = 0;

		try
		{
			if (metadata.ContainsQuery(query) && metadata.GetQuery(query) is object raw)
			{
				value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
				return true;
			}
		}
		catch
		{
		}

		return false;
	}
}
