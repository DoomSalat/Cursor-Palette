using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CursorPalette.Services;
using CursorPalette.Models;

namespace CursorPalette.Linux.Services;

public static class CursorPreviewService
{
	private const string AniExtension = ".ani";
	private const string CurExtension = ".cur";
	private const double Dpi = 96.0;

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
				var hotspotX = hotspot?.X ?? 0;
				var hotspotY = hotspot?.Y ?? 0;

				result = new List<CursorCanvasImage>();
				for (var index = 0; index < animated.StepFrameIndices.Count; index++)
				{
					var frameIndex = animated.StepFrameIndices[index];
					if (frameIndex < 0 || frameIndex >= animated.Frames.Count)
						continue;

					var frame = animated.Frames[frameIndex];
					result.Add(new CursorCanvasImage(frame.Width, frame.Height, hotspotX, hotspotY, frame.Bgra));
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
		var bitmap = new WriteableBitmap(
			new PixelSize(image.Width, image.Height),
			new Vector(Dpi, Dpi),
			PixelFormat.Bgra8888,
			AlphaFormat.Unpremul);

		using var locked = bitmap.Lock();
		Marshal.Copy(image.Bgra, 0, locked.Address, image.Bgra.Length);

		return bitmap;
	}
}
