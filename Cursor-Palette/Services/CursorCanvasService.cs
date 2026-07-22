using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CursorPalette.Services;

public sealed record CursorCanvasImage(int Width, int Height, int HotspotX, int HotspotY, byte[] Bgra);

public static class CursorCanvasService
{
	private const string User32Dll = "user32.dll";
	private const string CurExtension = ".cur";
	private const int BytesPerPixel = 4;
	private const int IconDirSize = 6;
	private const int IconDirEntrySize = 16;
	private const int BitmapInfoHeaderSize = 40;
	private const ushort CursorResourceType = 2;
	private const ushort CursorPlanes = 1;
	private const ushort CursorBitCount = 32;
	private const int RowAlignmentBits = 32;
	private const int MaxClassicDimension = 256;

	[DllImport(User32Dll, CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr LoadCursorFromFile(string lpFileName);

	[DllImport(User32Dll, SetLastError = true)]
	private static extern bool DestroyCursor(IntPtr cursorHandle);

	public static bool IsSupportedFile(string? filePath) =>
		!string.IsNullOrWhiteSpace(filePath) &&
		string.Equals(Path.GetExtension(filePath), CurExtension, StringComparison.OrdinalIgnoreCase);

	public static CursorCanvasImage? TryRead(string filePath)
	{
		if (!IsSupportedFile(filePath) || !File.Exists(filePath))
			return null;

		var hotspot = CursorHotspotService.Read(filePath);

		if (hotspot == null)
			return null;

		var handle = LoadCursorFromFile(filePath);

		if (handle == IntPtr.Zero)
			return null;

		try
		{
			BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(
				handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

			if (source.Format != PixelFormats.Bgra32)
				source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

			var width = source.PixelWidth;
			var height = source.PixelHeight;
			var stride = width * BytesPerPixel;
			var pixels = new byte[stride * height];
			source.CopyPixels(pixels, stride, 0);

			return new CursorCanvasImage(width, height, hotspot.X, hotspot.Y, pixels);
		}
		catch
		{
			return null;
		}
		finally
		{
			DestroyCursor(handle);
		}
	}

	public static void Write(string destinationPath, CursorCanvasImage image)
	{
		var width = Math.Clamp(image.Width, 1, MaxClassicDimension);
		var height = Math.Clamp(image.Height, 1, MaxClassicDimension);
		var hotspotX = Math.Clamp(image.HotspotX, 0, width - 1);
		var hotspotY = Math.Clamp(image.HotspotY, 0, height - 1);

		var colorRowStride = width * BytesPerPixel;
		var maskRowStride = ((width + RowAlignmentBits - 1) / RowAlignmentBits) * (RowAlignmentBits / 8);
		var colorDataSize = colorRowStride * height;
		var maskDataSize = maskRowStride * height;
		var imageDataSize = BitmapInfoHeaderSize + colorDataSize + maskDataSize;
		var imageOffset = IconDirSize + IconDirEntrySize;

		using var stream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
		using var writer = new BinaryWriter(stream);

		// ICONDIR
		writer.Write((ushort)0);
		writer.Write(CursorResourceType);
		writer.Write((ushort)1);

		// ICONDIRENTRY
		writer.Write((byte)(width >= MaxClassicDimension ? 0 : width));
		writer.Write((byte)(height >= MaxClassicDimension ? 0 : height));
		writer.Write((byte)0);
		writer.Write((byte)0);
		writer.Write((ushort)hotspotX);
		writer.Write((ushort)hotspotY);
		writer.Write((uint)imageDataSize);
		writer.Write((uint)imageOffset);

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
	}
}
