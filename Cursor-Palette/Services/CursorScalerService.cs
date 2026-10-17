using System.Text;
using CursorPalette.Models;

namespace CursorPalette.Services;

public static class CursorScalerService
{
	private const string CurExtension = ".cur";
	private const string AniExtension = ".ani";
	private const int BytesPerPixel = 4;
	private const int MaxCursorDimension = 256;

	public static Dictionary<string, string> ScaleValues(
		IReadOnlyDictionary<string, string> values, int targetSize, ScaleMode scaleMode = ScaleMode.AreaWeighted)
	{
		var result = new Dictionary<string, string>(values.Count);
		var targetSizes = GetDpiTargetSizes(targetSize);

		foreach (var (role, path) in values)
		{
			if (string.IsNullOrEmpty(path))
			{
				result[role] = path;
				continue;
			}

			var scaledPath = ScaleToSizes(path, targetSizes, scaleMode);
			result[role] = scaledPath ?? path;
		}

		return result;
	}

	private static List<int> GetDpiTargetSizes(int targetSize) =>
		DisplayDpiService.GetDistinctMonitorScales()
			.Select(scale => Math.Clamp((int)Math.Round(targetSize * scale), 1, MaxCursorDimension))
			.Prepend(targetSize)
			.Distinct()
			.Order()
			.ToList();

	private static string? ScaleToSizes(string sourcePath, IReadOnlyList<int> targetSizes, ScaleMode scaleMode)
	{
		if (!File.Exists(sourcePath))
			return null;

		var ext = Path.GetExtension(sourcePath).ToLowerInvariant();

		return ext switch
		{
			CurExtension => ScaleCurFile(sourcePath, targetSizes, scaleMode),
			AniExtension => ScaleAniFile(sourcePath, targetSizes, scaleMode),
			_ => sourcePath,
		};
	}

	private static string? ScaleCurFile(string sourcePath, IReadOnlyList<int> targetSizes, ScaleMode scaleMode)
	{
		var sourceImages = CursorCanvasService.TryReadAllImages(sourcePath);
		if (sourceImages == null && CursorCanvasService.TryRead(sourcePath) is { } singleImage)
			sourceImages = [singleImage];

		if (sourceImages == null)
			return null;

		if (ContainsAllSizes(sourceImages, targetSizes))
			return sourcePath;

		var destPath = GetScaledPath(sourcePath, targetSizes, CurExtension, scaleMode);

		if (File.Exists(destPath))
			return destPath;

		CursorCanvasService.WriteMultiSize(destPath, ScaleToAllSizes(sourceImages, targetSizes, scaleMode));

		return destPath;
	}

	private static string? ScaleAniFile(string sourcePath, IReadOnlyList<int> targetSizes, ScaleMode scaleMode)
	{
		var frames = AniCursorReader.Read(sourcePath);
		if (frames == null || frames.Frames.Count == 0)
			return null;

		var destPath = GetScaledPath(sourcePath, targetSizes, AniExtension, scaleMode);

		if (File.Exists(destPath))
			return destPath;

		var bytes = File.ReadAllBytes(sourcePath);
		var iconRanges = AniCursorReader.FindIconChunkRanges(bytes);

		var sourceFrames = new List<List<CursorCanvasImage>>(frames.StepFrameIndices.Count);
		var delays = new List<int>(frames.StepFrameIndices.Count);

		for (var step = 0; step < frames.StepFrameIndices.Count; step++)
		{
			var frameIndex = frames.StepFrameIndices[step];
			if (frameIndex >= iconRanges.Count)
				continue;

			var (offset, length) = iconRanges[frameIndex];
			var frameBytes = new byte[length];
			Array.Copy(bytes, offset, frameBytes, 0, length);

			var frameImages = CursorCanvasService.TryReadAllImagesFromBytes(frameBytes);
			if (frameImages == null && ReadFrameViaTempFile(frameBytes) is { } fallbackImage)
				frameImages = [fallbackImage];

			if (frameImages == null)
				continue;

			sourceFrames.Add(frameImages);
			delays.Add((int)frames.StepDurations[step].TotalMilliseconds);
		}

		if (sourceFrames.Count == 0)
			return null;

		if (sourceFrames.All(frameImages => ContainsAllSizes(frameImages, targetSizes)))
			return sourcePath;

		var scaledFrames = sourceFrames
			.Select(frameImages => ScaleToAllSizes(frameImages, targetSizes, scaleMode))
			.ToList();
		AniCursorWriter.SaveMultiSizeFrames(destPath, scaledFrames, delays);

		return destPath;
	}

	private static CursorCanvasImage? ReadFrameViaTempFile(byte[] frameBytes)
	{
		var tempPath = Path.Combine(Path.GetTempPath(), $"cp-scale-{Guid.NewGuid():N}.cur");

		try
		{
			File.WriteAllBytes(tempPath, frameBytes);
			return CursorCanvasService.TryRead(tempPath);
		}
		catch
		{
			return null;
		}
		finally
		{
			try { File.Delete(tempPath); } catch { }
		}
	}

	private static bool ContainsAllSizes(IReadOnlyList<CursorCanvasImage> images, IReadOnlyList<int> sizes) =>
		sizes.All(size => images.Any(image => image.Width == size && image.Height == size));

	private static List<CursorCanvasImage> ScaleToAllSizes(
		IReadOnlyList<CursorCanvasImage> sourceImages, IReadOnlyList<int> targetSizes, ScaleMode scaleMode) =>
		targetSizes
			.Select(size =>
			{
				var source = PickBestSource(sourceImages, size, scaleMode);
				return source.Width == size && source.Height == size ? source : ScaleImage(source, size, size, scaleMode);
			})
			.ToList();

	private static CursorCanvasImage PickBestSource(IReadOnlyList<CursorCanvasImage> sourceImages, int size, ScaleMode scaleMode)
	{
		var larger = sourceImages
			.Where(image => image.Width >= size && image.Height >= size)
			.MinBy(image => image.Width);

		if (larger != null)
			return larger;

		if (scaleMode == ScaleMode.NearestNeighbor)
		{
			var evenDivisor = sourceImages
				.Where(image => size % image.Width == 0 && size % image.Height == 0)
				.MaxBy(image => image.Width);

			if (evenDivisor != null)
				return evenDivisor;
		}

		return sourceImages.MaxBy(image => image.Width)!;
	}

	public static CursorCanvasImage ScaleImage(CursorCanvasImage source, int targetWidth, int targetHeight, ScaleMode scaleMode)
	{
		var scaledBgra = scaleMode == ScaleMode.NearestNeighbor
			? ScaleBgraNearestNeighbor(source.Bgra, source.Width, source.Height, targetWidth, targetHeight)
			: ScaleBgraAreaWeighted(source.Bgra, source.Width, source.Height, targetWidth, targetHeight);

		var scaledHotspotX = source.Width > 0
			? Math.Clamp((int)Math.Round((double)source.HotspotX * targetWidth / source.Width), 0, targetWidth - 1)
			: 0;
		var scaledHotspotY = source.Height > 0
			? Math.Clamp((int)Math.Round((double)source.HotspotY * targetHeight / source.Height), 0, targetHeight - 1)
			: 0;

		return new CursorCanvasImage(targetWidth, targetHeight, scaledHotspotX, scaledHotspotY, scaledBgra);
	}

	private static byte[] ScaleBgraNearestNeighbor(
		byte[] source, int srcWidth, int srcHeight, int dstWidth, int dstHeight)
	{
		var dest = new byte[dstWidth * dstHeight * BytesPerPixel];

		for (var destY = 0; destY < dstHeight; destY++)
		{
			var sourceY = Math.Min(destY * srcHeight / dstHeight, srcHeight - 1);

			for (var destX = 0; destX < dstWidth; destX++)
			{
				var sourceX = Math.Min(destX * srcWidth / dstWidth, srcWidth - 1);
				var sourceIndex = (sourceY * srcWidth + sourceX) * BytesPerPixel;
				var destIndex = (destY * dstWidth + destX) * BytesPerPixel;

				dest[destIndex] = source[sourceIndex];
				dest[destIndex + 1] = source[sourceIndex + 1];
				dest[destIndex + 2] = source[sourceIndex + 2];
				dest[destIndex + 3] = source[sourceIndex + 3];
			}
		}

		return dest;
	}

	private static byte[] ScaleBgraAreaWeighted(
		byte[] source, int srcWidth, int srcHeight, int dstWidth, int dstHeight)
	{
		var dest = new byte[dstWidth * dstHeight * BytesPerPixel];

		var scaleX = (double)srcWidth / dstWidth;
		var scaleY = (double)srcHeight / dstHeight;

		for (var dy = 0; dy < dstHeight; dy++)
		{
			var srcY0 = dy * scaleY;
			var srcY1 = srcY0 + scaleY;
			var yStart = Math.Max(0, (int)Math.Floor(srcY0));
			var yEnd = Math.Min(srcHeight - 1, (int)Math.Ceiling(srcY1) - 1);
			if (yEnd < yStart) yEnd = yStart;

			for (var dx = 0; dx < dstWidth; dx++)
			{
				var srcX0 = dx * scaleX;
				var srcX1 = srcX0 + scaleX;
				var xStart = Math.Max(0, (int)Math.Floor(srcX0));
				var xEnd = Math.Min(srcWidth - 1, (int)Math.Ceiling(srcX1) - 1);
				if (xEnd < xStart) xEnd = xStart;

				var sumBlue = 0.0;
				var sumGreen = 0.0;
				var sumRed = 0.0;
				var sumAlpha = 0.0;
				var totalWeight = 0.0;

				for (var sy = yStart; sy <= yEnd; sy++)
				{
					var overlapY = Math.Min(sy + 1, srcY1) - Math.Max(sy, srcY0);
					if (overlapY <= 0)
						continue;

					for (var sx = xStart; sx <= xEnd; sx++)
					{
						var overlapX = Math.Min(sx + 1, srcX1) - Math.Max(sx, srcX0);
						if (overlapX <= 0)
							continue;

						var weight = overlapX * overlapY;
						var sourceIndex = (sy * srcWidth + sx) * BytesPerPixel;

						var alpha = source[sourceIndex + 3] / 255.0;
						var alphaWeight = weight * alpha;

						sumBlue += source[sourceIndex] * alphaWeight;
						sumGreen += source[sourceIndex + 1] * alphaWeight;
						sumRed += source[sourceIndex + 2] * alphaWeight;
						sumAlpha += weight * alpha;
						totalWeight += weight;
					}
				}

				var destIndex = (dy * dstWidth + dx) * BytesPerPixel;

				if (sumAlpha > 0)
				{
					dest[destIndex] = (byte)Math.Clamp(Math.Round(sumBlue / sumAlpha), 0, 255);
					dest[destIndex + 1] = (byte)Math.Clamp(Math.Round(sumGreen / sumAlpha), 0, 255);
					dest[destIndex + 2] = (byte)Math.Clamp(Math.Round(sumRed / sumAlpha), 0, 255);
				}

				dest[destIndex + 3] = totalWeight > 0
					? (byte)Math.Clamp(Math.Round(sumAlpha / totalWeight * 255.0), 0, 255)
					: (byte)0;
			}
		}

		return dest;
	}

	private const string ScaleAlgorithmVersion = "v4";

	private static string GetScaledPath(string sourcePath, IReadOnlyList<int> targetSizes, string extension, ScaleMode scaleMode)
	{
		Directory.CreateDirectory(AppPaths.ScaledCursorsDir);

		var modeSuffix = scaleMode == ScaleMode.NearestNeighbor ? "-nn" : "-aw";
		var stamp = File.GetLastWriteTimeUtc(sourcePath).Ticks;
		var hash = System.Security.Cryptography.SHA256.HashData(
			Encoding.UTF8.GetBytes($"{ScaleAlgorithmVersion}{modeSuffix}{sourcePath.ToLowerInvariant()}|{stamp}|{string.Join('-', targetSizes)}"));

		var hashStr = Convert.ToHexString(hash)[..8];
		var fileName = $"{Path.GetFileNameWithoutExtension(sourcePath)}-{targetSizes[0]}-{hashStr}{extension}";

		return Path.Combine(AppPaths.ScaledCursorsDir, fileName);
	}
}
