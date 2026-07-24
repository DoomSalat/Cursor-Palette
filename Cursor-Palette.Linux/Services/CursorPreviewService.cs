using Avalonia.Media.Imaging;
using CursorPalette.Services;
using CursorPalette.Models;

namespace CursorPalette.Linux.Services;

public static class CursorPreviewService
{
	private const string AniExtension = ".ani";
	private const string CurExtension = ".cur";

	private static readonly Dictionary<string, Bitmap?> Cache = new(StringComparer.OrdinalIgnoreCase);
	private static readonly Dictionary<string, List<CursorCanvasImage>?> AnimatedCache = new(StringComparer.OrdinalIgnoreCase);

	public static Bitmap? GetPreview(string? filePath)
	{
		if (string.IsNullOrWhiteSpace(filePath))
			return null;

		var expanded = Environment.ExpandEnvironmentVariables(filePath);

		if (Cache.TryGetValue(expanded, out var cached))
			return cached;

		Bitmap? bitmap = null;

		if (File.Exists(expanded))
		{
			if (string.Equals(Path.GetExtension(expanded), CurExtension, StringComparison.OrdinalIgnoreCase))
			{
				var image = CursorCanvasService.TryRead(expanded);
				if (image != null)
				{
					bitmap = CreateBitmap(image);
					Cache[expanded] = bitmap;
					return bitmap;
				}
			}

			if (string.Equals(Path.GetExtension(expanded), AniExtension, StringComparison.OrdinalIgnoreCase))
			{
				var frames = GetAnimatedFrames(expanded);
				if (frames is { Count: > 0 })
				{
					bitmap = CreateBitmap(frames[0]);
					Cache[expanded] = bitmap;
					return bitmap;
				}
			}

			try
			{
				bitmap = new Bitmap(expanded);
			}
			catch
			{
				bitmap = null;
			}
		}

		Cache[expanded] = bitmap;
		return bitmap;
	}

	public static List<CursorCanvasImage>? GetAnimatedFrames(string filePath)
	{
		var expanded = Environment.ExpandEnvironmentVariables(filePath);

		if (AnimatedCache.TryGetValue(expanded, out var cached))
			return cached;

		List<CursorCanvasImage>? result = null;

		if (File.Exists(expanded) && string.Equals(Path.GetExtension(expanded), AniExtension, StringComparison.OrdinalIgnoreCase))
		{
			var animated = AniCursorReader.Read(expanded);
			if (animated != null && animated.Frames.Count > 0)
			{
				var hotspot = CursorHotspotService.Read(expanded);
				var hx = hotspot?.X ?? 0;
				var hy = hotspot?.Y ?? 0;

				result = new List<CursorCanvasImage>();
				for (var i = 0; i < animated.StepFrameIndices.Count; i++)
				{
					var frameIdx = animated.StepFrameIndices[i];
					if (frameIdx < 0 || frameIdx >= animated.Frames.Count)
						continue;

					var frame = animated.Frames[frameIdx];
					result.Add(new CursorCanvasImage(frame.Width, frame.Height, hx, hy, frame.Bgra));
				}
			}
		}

		AnimatedCache[expanded] = result;
		return result;
	}

	public static void Invalidate(string filePath)
	{
		var expanded = Environment.ExpandEnvironmentVariables(filePath);
		Cache.Remove(expanded);
		AnimatedCache.Remove(expanded);
	}

	private static Bitmap CreateBitmap(CursorCanvasImage image)
	{
		var stride = image.Width * 4;
		using var stream = new System.IO.MemoryStream(image.Bgra.Length + 122);
		using var writer = new System.IO.BinaryWriter(stream);

		// BMP header for BGRA
		writer.Write((byte)'B');
		writer.Write((byte)'M');
		writer.Write(54 + image.Bgra.Length);
		writer.Write(0);
		writer.Write(54);

		// DIB header (BITMAPINFOHEADER)
		writer.Write(40);
		writer.Write(image.Width);
		writer.Write(image.Height);
		writer.Write((ushort)1);
		writer.Write((ushort)32);
		writer.Write(0);
		writer.Write(image.Bgra.Length);
		writer.Write(0);
		writer.Write(0);
		writer.Write(0);
		writer.Write(0);

		// Pixel data (BMP is bottom-up, our data is top-down)
		for (var y = image.Height - 1; y >= 0; y--)
		{
			var offset = y * stride;
			writer.Write(image.Bgra, offset, stride);
		}

		stream.Position = 0;
		return new Bitmap(stream);
	}
}
