using System.Text;
using CursorPalette.Models;

namespace CursorPalette.Services;

public static class AniCursorWriter
{
	private const string RiffFourCc = "RIFF";
	private const string AconFourCc = "ACON";
	private const string AnihFourCc = "anih";
	private const string RateFourCc = "rate";
	private const string ListFourCc = "LIST";
	private const string FramFourCc = "fram";
	private const string IconFourCc = "icon";
	private const int FourCcSize = 4;
	private const int ChunkHeaderSize = 8;
	private const int AnihDataSize = 36;
	private const uint IconFlag = 0x1;
	private const double JiffiesPerSecond = 60.0;
	private const double MsPerJiffy = 1000.0 / JiffiesPerSecond;

	public static void Save(string destinationPath, IReadOnlyList<CursorCanvasImage> frames, IReadOnlyList<int> frameDelaysMs)
	{
		Save(destinationPath, frames, frameDelaysMs, null, null, null, ScaleMode.AreaWeighted);
	}

	public static void Save(
		string destinationPath,
		IReadOnlyList<CursorCanvasImage> frames,
		IReadOnlyList<int> frameDelaysMs,
		IReadOnlyList<int>? iconSizes,
		IReadOnlyDictionary<int, CursorCanvasImage>? iconSizeCustomImages,
		IReadOnlyDictionary<int, ScaleMode>? iconSizeScaleModeOverrides,
		ScaleMode iconSizesScaleMode)
	{
		if (frames.Count == 0)
			return;

		var iconChunks = new List<byte[]>(frames.Count);

		foreach (var frame in frames)
		{
			if (iconSizes is { Count: > 1 })
			{
				var images = iconSizes
					.Select(size =>
					{
						if (iconSizeCustomImages != null && iconSizeCustomImages.TryGetValue(size, out var custom))
							return custom;

						return size == frame.Width
							? frame
							: CursorScalerService.ScaleImage(frame, size, size,
									iconSizeScaleModeOverrides != null && iconSizeScaleModeOverrides.TryGetValue(size, out var mode) ? mode : iconSizesScaleMode);
					})
					.ToList();

				iconChunks.Add(CursorCanvasService.BuildMultiSizeBytes(images));
			}
			else
			{
				iconChunks.Add(CursorCanvasService.BuildBytes(frame));
			}
		}

		var rateJiffies = new uint[frames.Count];

		for (var i = 0; i < frames.Count; i++)
		{
			var delayMs = i < frameDelaysMs.Count ? frameDelaysMs[i] : (int)(MsPerJiffy * 6);
			rateJiffies[i] = (uint)Math.Max(1, (int)Math.Round(delayMs / MsPerJiffy));
		}

		using var stream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
		using var writer = new BinaryWriter(stream);

		writer.Write(Encoding.ASCII.GetBytes(RiffFourCc));
		var sizePosition = stream.Position;
		writer.Write((uint)0);
		writer.Write(Encoding.ASCII.GetBytes(AconFourCc));

		WriteAnihChunk(writer, frames.Count, rateJiffies[0]);
		WriteRateChunk(writer, rateJiffies);
		WriteFramList(writer, iconChunks);

		var endPosition = stream.Position;
		var riffSize = (uint)(endPosition - sizePosition - FourCcSize);
		stream.Position = sizePosition;
		writer.Write(riffSize);
	}

	private static void WriteChunkHeader(BinaryWriter writer, string fourCc, int dataSize)
	{
		writer.Write(Encoding.ASCII.GetBytes(fourCc));
		writer.Write((uint)dataSize);
	}

	private static void WritePadding(BinaryWriter writer, int dataSize)
	{
		if (dataSize % 2 != 0)
			writer.Write((byte)0);
	}

	private static void WriteAnihChunk(BinaryWriter writer, int frameCount, uint defaultJiffies)
	{
		WriteChunkHeader(writer, AnihFourCc, AnihDataSize);

		writer.Write((uint)AnihDataSize); // cbSizeOf
		writer.Write((uint)frameCount);   // nFrames
		writer.Write((uint)frameCount);   // nSteps
		writer.Write((uint)0);            // iWidth (ignored, frames are full icons)
		writer.Write((uint)0);            // iHeight
		writer.Write((uint)0);            // iBitCount
		writer.Write((uint)0);            // iPlanes
		writer.Write(defaultJiffies);     // iDisplayRate
		writer.Write(IconFlag);           // bfAttributes
	}

	private static void WriteRateChunk(BinaryWriter writer, uint[] rateJiffies)
	{
		var dataSize = rateJiffies.Length * 4;
		WriteChunkHeader(writer, RateFourCc, dataSize);

		foreach (var jiffies in rateJiffies)
			writer.Write(jiffies);

		WritePadding(writer, dataSize);
	}

	private static void WriteFramList(BinaryWriter writer, List<byte[]> iconChunks)
	{
		var innerSize = FourCcSize;

		foreach (var bytes in iconChunks)
			innerSize += ChunkHeaderSize + bytes.Length + (bytes.Length % 2 != 0 ? 1 : 0);

		WriteChunkHeader(writer, ListFourCc, innerSize);
		writer.Write(Encoding.ASCII.GetBytes(FramFourCc));

		foreach (var bytes in iconChunks)
		{
			WriteChunkHeader(writer, IconFourCc, bytes.Length);
			writer.Write(bytes);
			WritePadding(writer, bytes.Length);
		}
	}
}
