namespace CursorPalette.Services;

public sealed record CursorCanvasImage(int Width, int Height, int HotspotX, int HotspotY, byte[] Bgra);

public static class CursorCanvasService
{
	private const string CurExtension = ".cur";
	private const string AniExtension = ".ani";
	private const int BytesPerPixel = 4;
	private const int IconDirSize = 6;
	private const int IconDirEntrySize = 16;
	private const int BitmapInfoHeaderSize = 40;
	private const ushort CursorResourceType = 2;
	private const ushort CursorPlanes = 1;
	private const ushort CursorBitCount = 32;
	private const int RowAlignmentBits = 32;
	private const int MaxClassicDimension = 256;

	public static bool IsSupportedFile(string? filePath)
	{
		if (string.IsNullOrWhiteSpace(filePath))
			return false;

		var extension = Path.GetExtension(filePath);

		return string.Equals(extension, CurExtension, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(extension, AniExtension, StringComparison.OrdinalIgnoreCase);
	}

	public static CursorCanvasImage? TryRead(string filePath)
	{
		if (!IsSupportedFile(filePath) || !File.Exists(filePath))
			return null;

		try
		{
			return TryReadFromBytes(File.ReadAllBytes(filePath));
		}
		catch
		{
			return null;
		}
	}

	public static List<CursorCanvasImage>? TryReadAllImages(string filePath)
	{
		if (!IsSupportedFile(filePath) || !File.Exists(filePath))
			return null;

		byte[] bytes;
		try
		{
			bytes = File.ReadAllBytes(filePath);
		}
		catch
		{
			return null;
		}

		return TryReadAllImagesFromBytes(bytes);
	}

	public static List<CursorCanvasImage>? TryReadAllImagesFromBytes(byte[] bytes)
	{
		if (bytes.Length < IconDirSize + IconDirEntrySize)
			return null;

		var type = BitConverter.ToUInt16(bytes, 2);
		if (type != CursorResourceType)
			return null;

		var count = BitConverter.ToUInt16(bytes, 4);
		if (count == 0)
			return null;

		var images = new List<CursorCanvasImage>(count);
		var entryOffset = IconDirSize;

		for (var i = 0; i < count; i++)
		{
			var offset = entryOffset + i * IconDirEntrySize;
			if (offset + IconDirEntrySize > bytes.Length)
				break;

			var hotspotX = BitConverter.ToUInt16(bytes, offset + 4);
			var hotspotY = BitConverter.ToUInt16(bytes, offset + 6);
			var imageOffset = (int)BitConverter.ToUInt32(bytes, offset + 12);

			if (imageOffset + BitmapInfoHeaderSize > bytes.Length)
				continue;

			var width = Math.Abs(BitConverter.ToInt32(bytes, imageOffset + 4));
			var heightRaw = BitConverter.ToInt32(bytes, imageOffset + 8);
			var bitCount = BitConverter.ToUInt16(bytes, imageOffset + 14);
			var actualHeight = Math.Abs(heightRaw) / 2;

			if (width <= 0 || actualHeight <= 0 || width > MaxClassicDimension || actualHeight > MaxClassicDimension)
				continue;

			CursorCanvasImage? image;

			if (bitCount != CursorBitCount)
				image = TryReadPalettedFromBytes(bytes, imageOffset, width, actualHeight, bitCount, hotspotX, hotspotY);
			else
				image = TryRead32BitFromBytes(bytes, imageOffset, width, actualHeight, hotspotX, hotspotY);

			if (image != null)
				images.Add(image);
		}

		return images.Count > 0 ? images : null;
	}

	public static CursorCanvasImage? TryReadFromBytes(byte[] bytes)
	{
		if (bytes.Length < IconDirSize + IconDirEntrySize)
			return null;

		var type = BitConverter.ToUInt16(bytes, 2);
		if (type != CursorResourceType)
			return null;

		var count = BitConverter.ToUInt16(bytes, 4);
		if (count == 0)
			return null;

		var entryOffset = IconDirSize;
		var hotspotX = BitConverter.ToUInt16(bytes, entryOffset + 4);
		var hotspotY = BitConverter.ToUInt16(bytes, entryOffset + 6);
		var imageOffset = (int)BitConverter.ToUInt32(bytes, entryOffset + 12);

		if (imageOffset + BitmapInfoHeaderSize > bytes.Length)
			return null;

		var width = Math.Abs(BitConverter.ToInt32(bytes, imageOffset + 4));
		var heightRaw = BitConverter.ToInt32(bytes, imageOffset + 8);
		var bitCount = BitConverter.ToUInt16(bytes, imageOffset + 14);
		var actualHeight = Math.Abs(heightRaw) / 2;

		if (width <= 0 || actualHeight <= 0 || width > MaxClassicDimension || actualHeight > MaxClassicDimension)
			return null;

		if (bitCount != CursorBitCount)
			return TryReadPalettedFromBytes(bytes, imageOffset, width, actualHeight, bitCount, hotspotX, hotspotY);

		return TryRead32BitFromBytes(bytes, imageOffset, width, actualHeight, hotspotX, hotspotY);
	}

	private static CursorCanvasImage? TryRead32BitFromBytes(
		byte[] bytes, int imageOffset, int width, int actualHeight, int hotspotX, int hotspotY)
	{
		var colorRowStride = width * BytesPerPixel;
		var colorDataOffset = imageOffset + BitmapInfoHeaderSize;

		if (colorDataOffset + colorRowStride * actualHeight > bytes.Length)
			return null;

		var pixels = new byte[colorRowStride * actualHeight];

		for (var y = 0; y < actualHeight; y++)
		{
			var srcY = actualHeight - 1 - y;
			Array.Copy(bytes, colorDataOffset + srcY * colorRowStride, pixels, y * colorRowStride, colorRowStride);
		}

		var hasAlpha = false;

		for (var i = 3; i < pixels.Length; i += BytesPerPixel)
		{
			if (pixels[i] == 0)
				continue;

			hasAlpha = true;
			break;
		}

		if (!hasAlpha)
		{
			var maskRowStride = ((width + RowAlignmentBits - 1) / RowAlignmentBits) * (RowAlignmentBits / 8);
			var maskDataOffset = colorDataOffset + colorRowStride * actualHeight;

			if (maskDataOffset + maskRowStride * actualHeight <= bytes.Length)
			{
				for (var y = 0; y < actualHeight; y++)
				{
					var srcY = actualHeight - 1 - y;
					for (var x = 0; x < width; x++)
					{
						var maskByte = bytes[maskDataOffset + srcY * maskRowStride + x / 8];
						var isTransparent = (maskByte & (0x80 >> (x % 8))) != 0;
						pixels[y * colorRowStride + x * BytesPerPixel + 3] = isTransparent ? (byte)0 : (byte)255;
					}
				}
			}
		}

		return new CursorCanvasImage(width, actualHeight, hotspotX, hotspotY, pixels);
	}

	private static CursorCanvasImage? TryReadPalettedFromBytes(
		byte[] bytes, int imageOffset, int width, int actualHeight, ushort bitCount, int hotspotX, int hotspotY)
	{
		if (bitCount is not (8 or 4 or 24))
			return null;

		var paletteCount = bitCount == 24
			? 0
			: (int)BitConverter.ToUInt32(bytes, imageOffset + 32);

		if (paletteCount == 0 && bitCount != 24)
			paletteCount = 1 << bitCount;

		var paletteOffset = imageOffset + BitmapInfoHeaderSize;

		if (paletteOffset + paletteCount * 4 > bytes.Length)
			return null;

		var palette = new (byte B, byte G, byte R)[paletteCount];

		for (var i = 0; i < paletteCount; i++)
		{
			palette[i] = (
				bytes[paletteOffset + i * 4],
				bytes[paletteOffset + i * 4 + 1],
				bytes[paletteOffset + i * 4 + 2]);
		}

		var xorDataOffset = paletteOffset + paletteCount * 4;
		var xorRowStride = ((width * bitCount + 31) / 32) * 4;

		if (xorDataOffset + xorRowStride * actualHeight > bytes.Length)
			return null;

		var andDataOffset = xorDataOffset + xorRowStride * actualHeight;
		var andRowStride = ((width + 31) / 32) * 4;

		if (andDataOffset + andRowStride * actualHeight > bytes.Length)
			return null;

		var pixels = new byte[width * actualHeight * BytesPerPixel];

		for (var y = 0; y < actualHeight; y++)
		{
			var srcY = actualHeight - 1 - y;
			var xorRowStart = xorDataOffset + srcY * xorRowStride;
			var andRowStart = andDataOffset + srcY * andRowStride;

			for (var x = 0; x < width; x++)
			{
				byte blue, green, red;

				if (bitCount == 8)
				{
					var colorIndex = bytes[xorRowStart + x];
					if (colorIndex >= paletteCount)
						return null;
					(blue, green, red) = palette[colorIndex];
				}
				else if (bitCount == 4)
				{
					var byteIndex = x / 2;
					var nibble = (bytes[xorRowStart + byteIndex] >> (x % 2 == 0 ? 4 : 0)) & 0x0F;
					if (nibble >= paletteCount)
						return null;
					(blue, green, red) = palette[nibble];
				}
				else
				{
					var pixelOffset = xorRowStart + x * 3;
					blue = bytes[pixelOffset];
					green = bytes[pixelOffset + 1];
					red = bytes[pixelOffset + 2];
				}

				var maskByte = bytes[andRowStart + x / 8];
				var isTransparent = (maskByte & (0x80 >> (x % 8))) != 0;
				var alpha = isTransparent ? (byte)0 : (byte)255;

				var destIndex = (y * width + x) * BytesPerPixel;
				pixels[destIndex] = blue;
				pixels[destIndex + 1] = green;
				pixels[destIndex + 2] = red;
				pixels[destIndex + 3] = alpha;
			}
		}

		return new CursorCanvasImage(width, actualHeight, hotspotX, hotspotY, pixels);
	}

	public static void Write(string destinationPath, CursorCanvasImage image)
	{
		using var stream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
		WriteToStream(stream, image);
	}

	public static byte[] BuildBytes(CursorCanvasImage image)
	{
		using var stream = new MemoryStream();
		WriteToStream(stream, image);

		return stream.ToArray();
	}

	public static byte[] BuildMultiSizeBytes(IReadOnlyList<CursorCanvasImage> images)
	{
		using var stream = new MemoryStream();
		WriteMultiSizeToStream(stream, images);

		return stream.ToArray();
	}

	public static void WriteMultiSize(string destinationPath, IReadOnlyList<CursorCanvasImage> images)
	{
		using var stream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
		WriteMultiSizeToStream(stream, images);
	}

	private static void WriteToStream(Stream stream, CursorCanvasImage image) =>
		WriteMultiSizeToStream(stream, [image]);

	private static void WriteMultiSizeToStream(Stream stream, IReadOnlyList<CursorCanvasImage> images)
	{
		var blocks = new byte[images.Count][];
		var offset = IconDirSize + IconDirEntrySize * images.Count;

		using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);

		// ICONDIR
		writer.Write((ushort)0);
		writer.Write(CursorResourceType);
		writer.Write((ushort)images.Count);

		for (var i = 0; i < images.Count; i++)
		{
			var image = images[i];
			var width = Math.Clamp(image.Width, 1, MaxClassicDimension);
			var height = Math.Clamp(image.Height, 1, MaxClassicDimension);
			var hotspotX = Math.Clamp(image.HotspotX, 0, width - 1);
			var hotspotY = Math.Clamp(image.HotspotY, 0, height - 1);

			blocks[i] = BuildImageBlock(image, width, height);

			writer.Write((byte)(width >= MaxClassicDimension ? 0 : width));
			writer.Write((byte)(height >= MaxClassicDimension ? 0 : height));
			writer.Write((byte)0);
			writer.Write((byte)0);
			writer.Write((ushort)hotspotX);
			writer.Write((ushort)hotspotY);
			writer.Write((uint)blocks[i].Length);
			writer.Write((uint)offset);

			offset += blocks[i].Length;
		}

		foreach (var block in blocks)
			writer.Write(block);
	}

	private static byte[] BuildImageBlock(CursorCanvasImage image, int width, int height)
	{
		var colorRowStride = width * BytesPerPixel;
		var maskRowStride = ((width + RowAlignmentBits - 1) / RowAlignmentBits) * (RowAlignmentBits / 8);
		var colorDataSize = colorRowStride * height;
		var maskDataSize = maskRowStride * height;

		using var blockStream = new MemoryStream(BitmapInfoHeaderSize + colorDataSize + maskDataSize);
		using var writer = new BinaryWriter(blockStream);

		// BITMAPINFOHEADER
		writer.Write((uint)BitmapInfoHeaderSize);
		writer.Write(width);
		writer.Write(height * 2);
		writer.Write(CursorPlanes);
		writer.Write(CursorBitCount);
		writer.Write((uint)0);
		writer.Write((uint)(colorDataSize + maskDataSize));
		writer.Write(0);
		writer.Write(0);
		writer.Write((uint)0);
		writer.Write((uint)0);

		// Color data (bottom-up rows)
		for (var y = height - 1; y >= 0; y--)
			writer.Write(image.Bgra, y * colorRowStride, colorRowStride);

		// AND mask (bottom-up rows), transparent pixels get their bit set
		var maskRow = new byte[maskRowStride];

		for (var y = height - 1; y >= 0; y--)
		{
			Array.Clear(maskRow, 0, maskRow.Length);

			for (var x = 0; x < width; x++)
			{
				var alpha = image.Bgra[y * colorRowStride + x * BytesPerPixel + 3];

				if (alpha == 0)
					maskRow[x / 8] |= (byte)(0x80 >> (x % 8));
			}

			writer.Write(maskRow);
		}

		return blockStream.ToArray();
	}
}
