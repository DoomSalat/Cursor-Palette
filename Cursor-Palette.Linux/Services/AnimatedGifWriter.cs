using Avalonia.Media.Imaging;
using System.IO;
using System.Numerics;
using System.Text;

namespace CursorPalette.Services;

public static class AnimatedGifWriter
{
	private const byte ExtensionIntroducer = 0x21;
	private const byte GraphicControlLabel = 0xF9;
	private const byte Trailer = 0x3B;
	private const byte ImageSeparator = 0x2C;
	private const string GifHeader = "GIF89a";
	private const string NetscapeApplicationId = "NETSCAPE2.0";
	private const int MinDelayCentiseconds = 2;
	private const int MaxColors = 256;
	private const int BgraBytesPerPixel = 4;
	private const int MaxLzwCode = 4096;
	private const int MaxLzwCodeSize = 12;
	private const byte ApplicationExtensionLabel = 0xFF;
	private const byte BlockSizeLabel = 0x0B;
	private const byte SubBlockSize = 0x03;
	private const byte SubBlockId = 0x01;
	private const byte BlockTerminator = 0x00;
	private const byte GifLogicalScreenPackedField = 0xF7;
	private const byte GifBackgroundColorIndex = 0x00;
	private const byte GifPixelAspectRatio = 0x00;
	private const byte GraphicControlBlockSize = 0x04;
	private const byte GraphicControlPackedField = 0x09;
	private const byte LocalImagePackedFieldFlag = 0x80;
	private const ushort LoopCountZero = 0x0000;

	public static void Save(Stream stream, IReadOnlyList<WriteableBitmap> frames, IReadOnlyList<int> frameDelaysMs)
	{
		if (frames.Count == 0)
			return;

		var canvasWidth = frames[0].PixelSize.Width;
		var canvasHeight = frames[0].PixelSize.Height;

		using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

		writer.Write(Encoding.ASCII.GetBytes(GifHeader));
		writer.Write((ushort)canvasWidth);
		writer.Write((ushort)canvasHeight);
		writer.Write(GifLogicalScreenPackedField);
		writer.Write(GifBackgroundColorIndex);
		writer.Write(GifPixelAspectRatio);

		WriteLoopExtension(writer);

		for (var index = 0; index < frames.Count; index++)
		{
			var delayCentiseconds = Math.Max(MinDelayCentiseconds, frameDelaysMs[index] / 10);
			WriteFrame(writer, frames[index], delayCentiseconds);
		}

		writer.Write(Trailer);
	}

	private static void WriteLoopExtension(BinaryWriter writer)
	{
		writer.Write(ExtensionIntroducer);
		writer.Write(ApplicationExtensionLabel);
		writer.Write(BlockSizeLabel);
		writer.Write(Encoding.ASCII.GetBytes(NetscapeApplicationId));
		writer.Write(SubBlockSize);
		writer.Write(SubBlockId);
		writer.Write(LoopCountZero);
		writer.Write(BlockTerminator);
	}

	private static void WriteFrame(BinaryWriter writer, WriteableBitmap bitmap, int delayCentiseconds)
	{
		var width = bitmap.PixelSize.Width;
		var height = bitmap.PixelSize.Height;
		var pixels = new byte[width * height * BgraBytesPerPixel];
		using var lockedBitmap = bitmap.Lock();
		System.Runtime.InteropServices.Marshal.Copy(lockedBitmap.Address, pixels, 0, pixels.Length);

		var palette = BuildPalette(pixels, width, height);
		var colorTable = palette.Table;
		var indices = palette.Indices;
		var colorCount = colorTable.Length / 3;
		var sizeExponent = Math.Max(BitOperations.Log2((uint)colorCount) - 1, 0);

		writer.Write(ExtensionIntroducer);
		writer.Write(GraphicControlLabel);
		writer.Write(GraphicControlBlockSize);
		writer.Write(GraphicControlPackedField);
		writer.Write((ushort)delayCentiseconds);
		writer.Write(BlockTerminator);
		writer.Write(BlockTerminator);

		writer.Write(ImageSeparator);
		writer.Write((ushort)0);
		writer.Write((ushort)0);
		writer.Write((ushort)width);
		writer.Write((ushort)height);
		writer.Write((byte)(LocalImagePackedFieldFlag | sizeExponent));

		writer.Write(colorTable);

		writer.Write((byte)(sizeExponent + 1));
		var lzwData = LzwEncode(indices, sizeExponent + 1);
		writer.Write(lzwData);
		writer.Write(BlockTerminator);
	}

	private static (byte[] Table, byte[] Indices) BuildPalette(byte[] bgra, int width, int height)
	{
		var colorMap = new Dictionary<uint, int>();
		var colorList = new List<byte>();
		var indices = new byte[width * height];

		for (var index = 0; index < width * height; index++)
		{
			var blue = bgra[index * BgraBytesPerPixel];
			var green = bgra[index * BgraBytesPerPixel + 1];
			var red = bgra[index * BgraBytesPerPixel + 2];
			var alpha = bgra[index * BgraBytesPerPixel + 3];
			var packed = (uint)((alpha << 24) | (red << 16) | (green << 8) | blue);

			if (!colorMap.TryGetValue(packed, out var colorIndex))
			{
				colorIndex = colorMap.Count;
				if (colorIndex >= MaxColors)
				{
					colorIndex = FindNearestColor(colorList, red, green, blue);
				}
				else
				{
					colorMap[packed] = colorIndex;
					colorList.Add(red);
					colorList.Add(green);
					colorList.Add(blue);
				}
			}

			indices[index] = (byte)colorIndex;
		}

		var tableSize = Math.Max(2, NextPowerOfTwo(colorMap.Count));
		while (tableSize < 2) tableSize *= 2;
		var table = new byte[tableSize * 3];
		for (var index = 0; index < colorList.Count && index < tableSize * 3; index++)
			table[index] = colorList[index];

		return (table, indices);
	}

	private static int FindNearestColor(List<byte> colorList, byte red, byte green, byte blue)
	{
		var bestIndex = 0;
		var bestDistance = int.MaxValue;
		for (var index = 0; index < colorList.Count / 3; index++)
		{
			var deltaRed = red - colorList[index * 3];
			var deltaGreen = green - colorList[index * 3 + 1];
			var deltaBlue = blue - colorList[index * 3 + 2];
			var distance = deltaRed * deltaRed + deltaGreen * deltaGreen + deltaBlue * deltaBlue;
			if (distance < bestDistance)
			{
				bestDistance = distance;
				bestIndex = index;
			}
		}
		return bestIndex;
	}

	private static int NextPowerOfTwo(int value)
	{
		var power = 1;
		while (power < value) power <<= 1;
		return power;
	}

	private static byte[] LzwEncode(byte[] indices, int minCodeSize)
	{
		var clearCode = 1 << minCodeSize;
		var endCode = clearCode + 1;
		var nextCode = endCode + 1;
		var codeSize = minCodeSize + 1;
		var dictionary = new Dictionary<string, int>();
		var output = new List<bool>();
		var maxCode = 1 << codeSize;

		for (var index = 0; index < clearCode; index++)
			dictionary[index.ToString()] = index;

		EmitCode(output, clearCode, codeSize);

		var currentString = indices[0].ToString();

		for (var index = 1; index < indices.Length; index++)
		{
			var combinedKey = currentString + "," + indices[index];
			if (dictionary.ContainsKey(combinedKey))
			{
				currentString = combinedKey;
			}
			else
			{
				EmitCode(output, dictionary[currentString], codeSize);
				if (nextCode < MaxLzwCode)
				{
					dictionary[combinedKey] = nextCode;
					nextCode++;
					if (nextCode > maxCode && codeSize < MaxLzwCodeSize)
					{
						codeSize++;
						maxCode = 1 << codeSize;
					}
				}
				else
				{
					EmitCode(output, clearCode, codeSize);
					dictionary.Clear();
					for (var resetIndex = 0; resetIndex < clearCode; resetIndex++)
						dictionary[resetIndex.ToString()] = resetIndex;
					nextCode = endCode + 1;
					codeSize = minCodeSize + 1;
					maxCode = 1 << codeSize;
				}
				currentString = indices[index].ToString();
			}
		}

		EmitCode(output, dictionary[currentString], codeSize);
		EmitCode(output, endCode, codeSize);

		return BitsToBytes(output);
	}

	private static void EmitCode(List<bool> output, int code, int codeSize)
	{
		for (var index = 0; index < codeSize; index++)
			output.Add(((code >> index) & 1) == 1);
	}

	private static byte[] BitsToBytes(List<bool> bits)
	{
		var byteCount = (bits.Count + 7) / 8;
		var bytes = new byte[byteCount];
		for (var index = 0; index < bits.Count; index++)
		{
			if (bits[index])
				bytes[index / 8] |= (byte)(1 << (index % 8));
		}
		return bytes;
	}
}
