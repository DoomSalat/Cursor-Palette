using System.Text;

namespace CursorPalette.Services;

public static class CursorScalerService
{
	private const string CurExtension = ".cur";
	private const string AniExtension = ".ani";
	private const int BytesPerPixel = 4;

	public static Dictionary<string, string> ScaleValues(
		IReadOnlyDictionary<string, string> values, int targetSize)
	{
		var result = new Dictionary<string, string>(values.Count);

		foreach (var (role, path) in values)
		{
			if (string.IsNullOrEmpty(path))
			{
				result[role] = path;
				continue;
			}

			var scaledPath = ScaleToSize(path, targetSize);
			result[role] = scaledPath ?? path;
		}

		return result;
	}

	public static string? ScaleToSize(string sourcePath, int targetSize)
	{
		if (!File.Exists(sourcePath))
			return null;

		var ext = Path.GetExtension(sourcePath).ToLowerInvariant();

		return ext switch
		{
			CurExtension => ScaleCurFile(sourcePath, targetSize),
			AniExtension => ScaleAniFile(sourcePath, targetSize),
			_ => sourcePath,
		};
	}

	private static string? ScaleCurFile(string sourcePath, int targetSize)
	{
		var image = CursorCanvasService.TryRead(sourcePath);
		if (image == null)
			return null;

		if (image.Width == targetSize && image.Height == targetSize)
			return sourcePath;

		var destPath = GetScaledPath(sourcePath, targetSize, CurExtension);
		if (File.Exists(destPath))
			return destPath;

		var scaled = ScaleImage(image, targetSize, targetSize);
		CursorCanvasService.Write(destPath, scaled);

		return destPath;
	}

	private static string? ScaleAniFile(string sourcePath, int targetSize)
	{
		var frames = AniCursorReader.Read(sourcePath);
		if (frames == null || frames.Frames.Count == 0)
			return null;

		if (frames.Frames[0].PixelWidth == targetSize && frames.Frames[0].PixelHeight == targetSize)
			return sourcePath;

		var bytes = File.ReadAllBytes(sourcePath);
		var iconRanges = AniCursorReader.FindIconChunkRanges(bytes);

		var destPath = GetScaledPath(sourcePath, targetSize, AniExtension);
		if (File.Exists(destPath))
			return destPath;

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
				continue;

			var scaled = ScaleImage(image, targetSize, targetSize);
			scaledFrames.Add(scaled);
			delays.Add((int)frames.StepDurations[step].TotalMilliseconds);
		}

		if (scaledFrames.Count == 0)
			return null;

		AniCursorWriter.Save(destPath, scaledFrames, delays);

		return destPath;
	}

	private static CursorCanvasImage ScaleImage(CursorCanvasImage source, int targetWidth, int targetHeight)
	{
		var scaledBgra = ScaleBgraNearestNeighbor(
			source.Bgra, source.Width, source.Height, targetWidth, targetHeight);

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

		for (var dy = 0; dy < dstHeight; dy++)
		{
			int srcY;
			if (dstHeight > 1)
			{
				srcY = dy * (srcHeight - 1) / (dstHeight - 1);
				if (srcY < 0) srcY = 0;
				if (srcY >= srcHeight) srcY = srcHeight - 1;
			}
			else
			{
				srcY = 0;
			}

			for (var dx = 0; dx < dstWidth; dx++)
			{
				int srcX;
				if (dstWidth > 1)
				{
					srcX = dx * (srcWidth - 1) / (dstWidth - 1);
					if (srcX < 0) srcX = 0;
					if (srcX >= srcWidth) srcX = srcWidth - 1;
				}
				else
				{
					srcX = 0;
				}

				var srcIdx = (srcY * srcWidth + srcX) * BytesPerPixel;
				var dstIdx = (dy * dstWidth + dx) * BytesPerPixel;

				dest[dstIdx] = source[srcIdx];
				dest[dstIdx + 1] = source[srcIdx + 1];
				dest[dstIdx + 2] = source[srcIdx + 2];
				dest[dstIdx + 3] = source[srcIdx + 3];
			}
		}

		return dest;
	}

	private static string GetScaledPath(string sourcePath, int targetSize, string extension)
	{
		Directory.CreateDirectory(AppPaths.ScaledCursorsDir);

		var hash = System.Security.Cryptography.SHA256.HashData(
			Encoding.UTF8.GetBytes(sourcePath.ToLowerInvariant()));

		var hashStr = Convert.ToHexString(hash)[..8];
		var fileName = $"{Path.GetFileNameWithoutExtension(sourcePath)}-{targetSize}-{hashStr}{extension}";

		return Path.Combine(AppPaths.ScaledCursorsDir, fileName);
	}

	public static void Cleanup()
	{
		try
		{
			if (Directory.Exists(AppPaths.ScaledCursorsDir))
				Directory.Delete(AppPaths.ScaledCursorsDir, recursive: true);
		}
		catch
		{
		}
	}
}
