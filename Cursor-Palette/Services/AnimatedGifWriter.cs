using System.Numerics;
using System.Text;
using System.Windows.Media.Imaging;

namespace CursorPalette.Services;

public static class AnimatedGifWriter
{
	private const byte ExtensionIntroducer = 0x21;
	private const byte GraphicControlLabel = 0xF9;
	private const byte Trailer = 0x3B;
	private const int HeaderSize = 6;
	private const int LsdSize = 7;
	private const int ImageDescriptorSize = 10;
	private const int MinDelayCentiseconds = 2;
	private const string GifHeader = "GIF89a";
	private const string NetscapeApplicationId = "NETSCAPE2.0";

	public static void Save(string path, IReadOnlyList<BitmapSource> frames, IReadOnlyList<int> frameDelaysMs)
	{
		if (frames.Count == 0)
			return;

		var frameChunks = new List<byte[]>(frames.Count);
		var canvasWidth = 0;
		var canvasHeight = 0;

		for (var i = 0; i < frames.Count; i++)
		{
			var encoded = EncodeSingleFrame(frames[i]);
			var (width, height, chunk) = ExtractFrameChunk(encoded);

			if (i == 0)
			{
				canvasWidth = width;
				canvasHeight = height;
			}

			var delayCentiseconds = Math.Max(MinDelayCentiseconds, frameDelaysMs[i] / 10);
			PatchDelay(chunk, delayCentiseconds);

			frameChunks.Add(chunk);
		}

		using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
		using var writer = new BinaryWriter(stream);

		writer.Write(Encoding.ASCII.GetBytes(GifHeader));
		writer.Write((ushort)canvasWidth);
		writer.Write((ushort)canvasHeight);
		writer.Write((byte)0x00);
		writer.Write((byte)0x00);
		writer.Write((byte)0x00);

		WriteLoopExtension(writer);

		foreach (var chunk in frameChunks)
			writer.Write(chunk);

		writer.Write(Trailer);
	}

	private static void WriteLoopExtension(BinaryWriter writer)
	{
		writer.Write(ExtensionIntroducer);
		writer.Write((byte)0xFF);
		writer.Write((byte)0x0B);
		writer.Write(Encoding.ASCII.GetBytes(NetscapeApplicationId));
		writer.Write((byte)0x03);
		writer.Write((byte)0x01);
		writer.Write((ushort)0x0000);
		writer.Write((byte)0x00);
	}

	private static byte[] EncodeSingleFrame(BitmapSource frame)
	{
		var encoder = new GifBitmapEncoder();
		encoder.Frames.Add(BitmapFrame.Create(frame));

		using var memoryStream = new MemoryStream();
		encoder.Save(memoryStream);

		return memoryStream.ToArray();
	}

	private static int SkipSubBlocks(byte[] bytes, int pos)
	{
		while (true)
		{
			var blockSize = bytes[pos];
			pos++;

			if (blockSize == 0)
				return pos;

			pos += blockSize;
		}
	}

	private static (int Width, int Height, byte[] Chunk) ExtractFrameChunk(byte[] bytes)
	{
		var width = BitConverter.ToUInt16(bytes, HeaderSize);
		var height = BitConverter.ToUInt16(bytes, HeaderSize + 2);
		var lsdPacked = bytes[HeaderSize + 4];
		var pos = HeaderSize + LsdSize;

		var globalColorTable = Array.Empty<byte>();

		if ((lsdPacked & 0x80) != 0)
		{
			var gctLen = 3 * (1 << ((lsdPacked & 0x07) + 1));
			globalColorTable = bytes[pos..(pos + gctLen)];
			pos += gctLen;
		}

		var extensionsStart = pos;

		while (bytes[pos] == ExtensionIntroducer)
		{
			pos += 2;
			pos = SkipSubBlocks(bytes, pos);
		}

		byte[]? gceBytes = null;
		var extensionsPos = extensionsStart;

		while (extensionsPos < pos)
		{
			var blockStart = extensionsPos;
			var label = bytes[blockStart + 1];
			var blockEnd = SkipSubBlocks(bytes, blockStart + 2);

			if (label == GraphicControlLabel)
				gceBytes = bytes[blockStart..blockEnd];

			extensionsPos = blockEnd;
		}

		gceBytes ??= new byte[] { ExtensionIntroducer, GraphicControlLabel, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00 };

		var descriptorStart = pos;
		var originalPacked = bytes[descriptorStart + 9];
		pos += ImageDescriptorSize;

		byte[] colorTable;

		if ((originalPacked & 0x80) != 0)
		{
			var lctLen = 3 * (1 << ((originalPacked & 0x07) + 1));
			colorTable = bytes[pos..(pos + lctLen)];
			pos += lctLen;
		}
		else
		{
			colorTable = globalColorTable;
		}

		var lzwStart = pos;
		pos += 1;
		pos = SkipSubBlocks(bytes, pos);
		var imageDataBytes = bytes[lzwStart..pos];

		var colorCount = Math.Max(colorTable.Length / 3, 2);
		var sizeExp = Math.Max(BitOperations.Log2((uint)colorCount) - 1, 0);
		var descriptorBytes = bytes[descriptorStart..(descriptorStart + ImageDescriptorSize)].ToArray();
		descriptorBytes[9] = (byte)(0x80 | (originalPacked & 0x60) | sizeExp);

		using var output = new MemoryStream();
		output.Write(gceBytes);
		output.Write(descriptorBytes);
		output.Write(colorTable);
		output.Write(imageDataBytes);

		return (width, height, output.ToArray());
	}

	private static void PatchDelay(byte[] chunk, int delayCentiseconds)
	{
		if (chunk.Length < 8 || chunk[0] != ExtensionIntroducer || chunk[1] != GraphicControlLabel)
			return;

		chunk[4] = (byte)(delayCentiseconds & 0xFF);
		chunk[5] = (byte)((delayCentiseconds >> 8) & 0xFF);
	}
}
