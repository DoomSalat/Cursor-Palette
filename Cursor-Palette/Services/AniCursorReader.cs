using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace CursorPalette.Services;

public sealed record AnimatedCursorFrames(
	IReadOnlyList<BitmapSource> Frames,
	IReadOnlyList<int> StepFrameIndices,
	IReadOnlyList<TimeSpan> StepDurations);

public static class AniCursorReader
{
	private const string User32Dll = "user32.dll";
	private const uint FallbackJiffiesPerFrame = 6;
	private const int RiffHeaderSize = 12;
	private const int AnimationHeaderMinSize = 36;
	private const string IconChunkFourCc = "icon";
	private const string AnihChunkFourCc = "anih";
	private const string RateChunkFourCc = "rate";
	private const string SeqChunkFourCc = "seq ";
	private const string ListChunkFourCc = "LIST";
	private const double JiffiesPerSecond = 60.0;
	private const int AnihFrameCountOffset = 4;
	private const int AnihStepCountOffset = 8;
	private const int AnihJiffiesPerFrameOffset = 28;
	private const int FourCcSize = 4;
	private const int ChunkHeaderSize = 8;
	private const int ListTypeSize = 4;
	private const int UInt32Size = 4;
	private const int WordAlignment = 2;
	private const string TempFilePrefix = "cursor-palette-frame-";
	private const string TempFileExtension = ".cur";
	private const string RiffSignature = "RIFF";
	private const string AconTypeSignature = "ACON";
	private const int AconTypeOffset = 8;

	[DllImport(User32Dll, CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr LoadCursorFromFile(string lpFileName);

	[DllImport(User32Dll, SetLastError = true)]
	private static extern bool DestroyCursor(IntPtr hCursor);

	public static AnimatedCursorFrames? Read(string path)
	{
		try
		{
			return Parse(File.ReadAllBytes(path));
		}
		catch
		{
			return null;
		}
	}

	public static List<(int Offset, int Length)> FindIconChunkRanges(byte[] bytes)
	{
		var chunks = new List<(int Offset, int Length)>();

		if (!IsRiffAconFile(bytes))
			return chunks;

		WalkChunks(bytes, RiffHeaderSize, bytes.Length, (fourCc, offset, length) =>
		{
			if (fourCc == IconChunkFourCc)
				chunks.Add((offset, length));
		});

		return chunks;
	}

	private static AnimatedCursorFrames? Parse(byte[] bytes)
	{
		if (!IsRiffAconFile(bytes))
			return null;

		var header = new AnimationHeader();
		var iconChunks = new List<(int Offset, int Length)>();

		WalkChunks(bytes, RiffHeaderSize, bytes.Length, (fourCc, offset, length) =>
			CollectChunk(bytes, fourCc, offset, length, header, iconChunks));

		if (iconChunks.Count == 0)
			return null;

		var frames = DecodeFrames(bytes, iconChunks, header.FrameCount);
		if (frames.Count == 0)
			return null;

		return BuildSchedule(frames, header);
	}

	private static bool IsRiffAconFile(byte[] bytes) =>
		bytes.Length >= RiffHeaderSize &&
		bytes[0] == RiffSignature[0] && bytes[1] == RiffSignature[1] &&
		bytes[2] == RiffSignature[2] && bytes[3] == RiffSignature[3] &&
		bytes[AconTypeOffset] == AconTypeSignature[0] && bytes[AconTypeOffset + 1] == AconTypeSignature[1] &&
		bytes[AconTypeOffset + 2] == AconTypeSignature[2] && bytes[AconTypeOffset + 3] == AconTypeSignature[3];

	private sealed class AnimationHeader
	{
		public uint FrameCount;
		public uint StepCount;
		public uint JiffiesPerFrame = FallbackJiffiesPerFrame;
		public uint[]? StepDurationsJiffies;
		public uint[]? StepFrameOrder;
	}

	private static void CollectChunk(
		byte[] bytes, string fourCc, int offset, int length,
		AnimationHeader header, List<(int Offset, int Length)> iconChunks)
	{
		switch (fourCc)
		{
			case AnihChunkFourCc:
				ReadAnimationHeader(bytes, offset, length, header);
				break;
			case RateChunkFourCc:
				header.StepDurationsJiffies = ReadUInt32Array(bytes, offset, length);
				break;
			case SeqChunkFourCc:
				header.StepFrameOrder = ReadUInt32Array(bytes, offset, length);
				break;
			case IconChunkFourCc:
				iconChunks.Add((offset, length));
				break;
		}
	}

	private static void ReadAnimationHeader(byte[] bytes, int offset, int length, AnimationHeader header)
	{
		if (length < AnimationHeaderMinSize)
			return;

		header.FrameCount = ReadUInt32(bytes, offset + AnihFrameCountOffset);
		header.StepCount = ReadUInt32(bytes, offset + AnihStepCountOffset);
		header.JiffiesPerFrame = ReadUInt32(bytes, offset + AnihJiffiesPerFrameOffset);

		if (header.JiffiesPerFrame == 0)
			header.JiffiesPerFrame = FallbackJiffiesPerFrame;
	}

	private static List<BitmapSource> DecodeFrames(
		byte[] bytes, List<(int Offset, int Length)> iconChunks, uint declaredFrameCount)
	{
		var frameCount = declaredFrameCount > 0
			? Math.Min((int)declaredFrameCount, iconChunks.Count)
			: iconChunks.Count;

		var frames = new List<BitmapSource>(frameCount);

		for (var i = 0; i < frameCount; i++)
		{
			var (offset, length) = iconChunks[i];
			var frame = DecodeFrameViaTempCursorFile(bytes, offset, length);

			if (frame == null)
				break;

			frames.Add(frame);
		}

		return frames;
	}

	private static AnimatedCursorFrames BuildSchedule(List<BitmapSource> frames, AnimationHeader header)
	{
		var steps = header.StepCount > 0 ? (int)header.StepCount : frames.Count;
		var frameIndices = new List<int>(steps);
		var durations = new List<TimeSpan>(steps);

		for (var step = 0; step < steps; step++)
		{
			frameIndices.Add(ResolveStepFrameIndex(header, step, frames.Count));
			durations.Add(ResolveStepDuration(header, step));
		}

		return new AnimatedCursorFrames(frames, frameIndices, durations);
	}

	private static int ResolveStepFrameIndex(AnimationHeader header, int step, int frameCount)
	{
		var index = header.StepFrameOrder != null && step < header.StepFrameOrder.Length
			? (int)header.StepFrameOrder[step]
			: step % frameCount;

		return Math.Clamp(index, 0, frameCount - 1);
	}

	private static TimeSpan ResolveStepDuration(AnimationHeader header, int step)
	{
		var jiffies = header.StepDurationsJiffies != null &&
			step < header.StepDurationsJiffies.Length &&
			header.StepDurationsJiffies[step] > 0
				? header.StepDurationsJiffies[step]
				: header.JiffiesPerFrame;

		return TimeSpan.FromSeconds(jiffies / JiffiesPerSecond);
	}

	private static BitmapSource? DecodeFrameViaTempCursorFile(byte[] bytes, int offset, int length)
	{
		var tempPath = Path.Combine(Path.GetTempPath(), $"{TempFilePrefix}{Guid.NewGuid():N}{TempFileExtension}");

		try
		{
			using (var stream = File.Create(tempPath))
				stream.Write(bytes, offset, length);

			return LoadCursorAsFrozenBitmap(tempPath);
		}
		catch
		{
			return null;
		}
		finally
		{
			TryDeleteFile(tempPath);
		}
	}

	private static BitmapSource? LoadCursorAsFrozenBitmap(string cursorFilePath)
	{
		var handle = LoadCursorFromFile(cursorFilePath);
		if (handle == IntPtr.Zero)
			return null;

		try
		{
			var image = Imaging.CreateBitmapSourceFromHIcon(handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
			image.Freeze();
			return image;
		}
		finally
		{
			DestroyCursor(handle);
		}
	}

	private static void TryDeleteFile(string path)
	{
		try
		{
			File.Delete(path);
		}
		catch
		{
		}
	}

	private static void WalkChunks(byte[] bytes, int start, int end, Action<string, int, int> onChunk)
	{
		var pos = start;

		while (pos + ChunkHeaderSize <= end)
		{
			var fourCc = System.Text.Encoding.ASCII.GetString(bytes, pos, FourCcSize);
			var size = (int)ReadUInt32(bytes, pos + FourCcSize);
			var dataOffset = pos + ChunkHeaderSize;

			if (dataOffset + size > end || size < 0)
				break;

			if (fourCc == ListChunkFourCc && size >= ListTypeSize)
				WalkChunks(bytes, dataOffset + ListTypeSize, dataOffset + size, onChunk);
			else
				onChunk(fourCc, dataOffset, size);

			pos = dataOffset + size + (size % WordAlignment);
		}
	}

	private static uint ReadUInt32(byte[] bytes, int offset) =>
		BitConverter.ToUInt32(bytes, offset);

	private static uint[] ReadUInt32Array(byte[] bytes, int offset, int length)
	{
		var count = length / UInt32Size;
		var result = new uint[count];

		for (var i = 0; i < count; i++)
			result[i] = ReadUInt32(bytes, offset + i * UInt32Size);

		return result;
	}
}
