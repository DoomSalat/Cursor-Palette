using System.Text;
using CursorPalette.Models;

namespace CursorPalette.Services;

public static class CursorScalerService
{
	private const string CurExtension = ".cur";
	private const string AniExtension = ".ani";
	private const int BytesPerPixel = 4;

	public static Dictionary<string, string> ScaleValues(
		IReadOnlyDictionary<string, string> values, int targetSize, ScaleMode scaleMode = ScaleMode.AreaWeighted)
	{
		var result = new Dictionary<string, string>(values.Count);

		foreach (var (role, path) in values)
		{
			if (string.IsNullOrEmpty(path))
			{
				result[role] = path;
				continue;
			}

			var scaledPath = ScaleToSize(path, targetSize, scaleMode);
			result[role] = scaledPath ?? path;
		}

		return result;
	}

	public static string? ScaleToSize(string sourcePath, int targetSize, ScaleMode scaleMode = ScaleMode.AreaWeighted)
	{
		if (!File.Exists(sourcePath))
			return null;

		var ext = Path.GetExtension(sourcePath).ToLowerInvariant();

		return ext switch
		{
			CurExtension => ScaleCurFile(sourcePath, targetSize, scaleMode),
			AniExtension => ScaleAniFile(sourcePath, targetSize, scaleMode),
			_ => sourcePath,
		};
	}

	private static string? ScaleCurFile(string sourcePath, int targetSize, ScaleMode scaleMode)
	{
		var image = CursorCanvasService.TryRead(sourcePath);
		if (image == null)
			return null;

		if (image.Width == targetSize && image.Height == targetSize)
			return sourcePath;

		var destPath = GetScaledPath(sourcePath, targetSize, CurExtension, scaleMode);

		var scaled = ScaleImage(image, targetSize, targetSize, scaleMode);
		CursorCanvasService.Write(destPath, scaled);

		return destPath;
	}

	private static string? ScaleAniFile(string sourcePath, int targetSize, ScaleMode scaleMode)
	{
		var frames = AniCursorReader.Read(sourcePath);
		if (frames == null || frames.Frames.Count == 0)
			return null;

		if (frames.Frames[0].Width == targetSize && frames.Frames[0].Height == targetSize)
			return sourcePath;

		var bytes = File.ReadAllBytes(sourcePath);
		var iconRanges = AniCursorReader.FindIconChunkRanges(bytes);

		var destPath = GetScaledPath(sourcePath, targetSize, AniExtension, scaleMode);

		var scaledFrames = new List<CursorCanvasImage>(frames.StepFrameIndices.Count);
		var delays = new List<int>(frames.StepFrameIndices.Count);

		for (var step = 0; step < frames.StepFrameIndices.Count; step++)
		{
			var frameIdx = frames.StepFrameIndices[step];
			if (frameIdx >= iconRanges.Count)
				continue;

			var (offset, length) = iconRanges[frameIdx];
			var frameBytes = new byte[length];
			Array.Copy(bytes, offset, frameBytes, 0, length);

			var image = CursorCanvasService.TryReadFromBytes(frameBytes);
			if (image == null)
			{
				var tempPath = Path.Combine(Path.GetTempPath(), $"cp-scale-{Guid.NewGuid():N}.cur");
				try
				{
					File.WriteAllBytes(tempPath, frameBytes);
					image = CursorCanvasService.TryRead(tempPath);
				}
				catch
				{
				}
				finally
				{
					try { File.Delete(tempPath); } catch { }
				}
				if (image == null)
					continue;
			}

			var scaled = ScaleImage(image, targetSize, targetSize, scaleMode);
			scaledFrames.Add(scaled);
			delays.Add((int)frames.StepDurations[step].TotalMilliseconds);
		}

		if (scaledFrames.Count == 0)
			return null;

		AniCursorWriter.Save(destPath, scaledFrames, delays);

		return destPath;
	}

	private static CursorCanvasImage ScaleImage(CursorCanvasImage source, int targetWidth, int targetHeight, ScaleMode scaleMode)
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

				var sumB = 0.0;
				var sumG = 0.0;
				var sumR = 0.0;
				var sumA = 0.0;
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
						var srcIdx = (sy * srcWidth + sx) * BytesPerPixel;

						var alpha = source[srcIdx + 3] / 255.0;
						var alphaWeight = weight * alpha;

						sumB += source[srcIdx] * alphaWeight;
						sumG += source[srcIdx + 1] * alphaWeight;
						sumR += source[srcIdx + 2] * alphaWeight;
						sumA += weight * alpha;
						totalWeight += weight;
					}
				}

				var dstIdx = (dy * dstWidth + dx) * BytesPerPixel;

				if (sumA > 0)
				{
					dest[dstIdx] = (byte)Math.Clamp(Math.Round(sumB / sumA), 0, 255);
					dest[dstIdx + 1] = (byte)Math.Clamp(Math.Round(sumG / sumA), 0, 255);
					dest[dstIdx + 2] = (byte)Math.Clamp(Math.Round(sumR / sumA), 0, 255);
				}

				dest[dstIdx + 3] = totalWeight > 0
					? (byte)Math.Clamp(Math.Round(sumA / totalWeight * 255.0), 0, 255)
					: (byte)0;
			}
		}

		return dest;
	}

	private const string ScaleAlgorithmVersion = "v4";

	private static string GetScaledPath(string sourcePath, int targetSize, string extension, ScaleMode scaleMode)
	{
		Directory.CreateDirectory(PathProvider.Current.ScaledCursorsDir);

		var modeSuffix = scaleMode == ScaleMode.NearestNeighbor ? "-nn" : "-aw";
		var hash = System.Security.Cryptography.SHA256.HashData(
			Encoding.UTF8.GetBytes(ScaleAlgorithmVersion + modeSuffix + sourcePath.ToLowerInvariant()));

		var hashStr = Convert.ToHexString(hash)[..8];
		var fileName = $"{Path.GetFileNameWithoutExtension(sourcePath)}-{targetSize}-{hashStr}{extension}";

		return Path.Combine(PathProvider.Current.ScaledCursorsDir, fileName);
	}

	public static void Cleanup()
	{
		try
		{
			if (Directory.Exists(PathProvider.Current.ScaledCursorsDir))
				Directory.Delete(PathProvider.Current.ScaledCursorsDir, recursive: true);
		}
		catch
		{
		}
	}
}
