namespace CursorPalette.Services;

public sealed record CursorHotspot(int X, int Y, int Width, int Height);

public static class CursorHotspotService
{
	private const int WidthOffset = 6;
	private const int HeightOffset = 7;
	private const int XHotspotOffset = 10;
	private const int YHotspotOffset = 12;
	private const string AniExtension = ".ani";
	private const int CursorInfoHeaderSize = 16;
	private const int ImplicitCursorDimension = 256;

	public static CursorHotspot? Read(string filePath)
	{
		try
		{
			var bytes = File.ReadAllBytes(filePath);
			var blobOffset = FindFirstBlobOffset(bytes, filePath);

			return blobOffset is int offset && bytes.Length >= offset + CursorInfoHeaderSize
				? ReadAt(bytes, offset)
				: null;
		}
		catch
		{
			return null;
		}
	}

	public static void WriteWithHotspot(string sourcePath, string destinationPath, int x, int y)
	{
		var bytes = File.ReadAllBytes(sourcePath);

		if (IsAniFile(sourcePath))
		{
			foreach (var (offset, _) in AniCursorReader.FindIconChunkRanges(bytes))
				WriteAt(bytes, offset, x, y);
		}
		else
		{
			WriteAt(bytes, 0, x, y);
		}

		File.WriteAllBytes(destinationPath, bytes);
	}

	private static int? FindFirstBlobOffset(byte[] bytes, string filePath)
	{
		if (!IsAniFile(filePath))
			return 0;

		var chunks = AniCursorReader.FindIconChunkRanges(bytes);

		return chunks.Count > 0 ? chunks[0].Offset : null;
	}

	private static CursorHotspot ReadAt(byte[] bytes, int blobOffset)
	{
		var width = bytes[blobOffset + WidthOffset];
		var height = bytes[blobOffset + HeightOffset];
		var x = BitConverter.ToUInt16(bytes, blobOffset + XHotspotOffset);
		var y = BitConverter.ToUInt16(bytes, blobOffset + YHotspotOffset);

		return new CursorHotspot(x, y, width == 0 ? ImplicitCursorDimension : width, height == 0 ? ImplicitCursorDimension : height);
	}

	private static void WriteAt(byte[] bytes, int blobOffset, int x, int y)
	{
		BitConverter.GetBytes((ushort)x).CopyTo(bytes, blobOffset + XHotspotOffset);
		BitConverter.GetBytes((ushort)y).CopyTo(bytes, blobOffset + YHotspotOffset);
	}

	private static bool IsAniFile(string path) =>
		string.Equals(Path.GetExtension(path), AniExtension, StringComparison.OrdinalIgnoreCase);
}
