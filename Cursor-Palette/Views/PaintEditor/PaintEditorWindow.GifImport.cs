using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CursorPalette.Views;

public partial class PaintEditorWindow
{
	private const int DefaultGifFrameDelayMs = 100;
	private const int DefaultGifFrameDelayCentiseconds = 10;
	private const int CentisecondsToMs = 10;

	private sealed record GifRawFrame(BitmapSource Bitmap, int Left, int Top, int DelayMs, int Disposal);

	private bool TryImportAnimatedGif(string path)
	{
		if (!string.Equals(Path.GetExtension(path), GifExtension, StringComparison.OrdinalIgnoreCase))
			return false;

		var decoded = DecodeGifRawFrames(path);

		if (decoded == null || decoded.Value.Frames.Count <= 1)
			return false;

		var (width, height, rawFrames) = decoded.Value;
		var composed = ComposeGifFrames(width, height, rawFrames);

		ApplyImportedGifFrames(width, height, composed);

		return true;
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

	private void ApplyImportedGifFrames(int width, int height, List<(BitmapSource Bitmap, int DelayMs)> frames)
	{
		var limited = frames.Count > MaxTimelineFrames ? frames.GetRange(0, MaxTimelineFrames) : frames;

		if (limited.Count == 0)
			return;

		var clampedWidth = Math.Clamp(width, MinCanvasDimension, MaxCanvasDimension);
		var clampedHeight = Math.Clamp(height, MinCanvasDimension, MaxCanvasDimension);

		_timelineFrames.Clear();

		foreach (var (bitmap, delayMs) in limited)
		{
			var bgra = BitmapToBgra(bitmap, width, height);
			var durationMs = Math.Clamp(delayMs, MinFrameDurationMs, MaxFrameDurationMs);

			_timelineFrames.Add(new TimelineFrame(bgra, clampedWidth, clampedHeight, 0, 0, durationMs));
		}

		_canvasWidth = clampedWidth;
		_canvasHeight = clampedHeight;
		_activeFrameIndex = 0;

		ApplyFrame(_timelineFrames[0]);
		RebuildFrameStrip();
		UpdateTimelineButtons();
		SyncRefFrameToTimeline();
	}
}
