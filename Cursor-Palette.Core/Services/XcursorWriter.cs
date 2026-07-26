namespace CursorPalette.Services;

public sealed record XcursorFrame(int Width, int Height, int HotspotX, int HotspotY, byte[] Bgra, int DelayMs);

public static class XcursorWriter
{
	private const uint Magic = 0x72756358; // "Xcur" read as a little-endian uint32
	private const uint FileHeaderSize = 16;
	private const uint FileVersion = 0x1_0000;
	private const uint ImageChunkType = 0xfffd0002;
	private const uint ImageChunkHeaderSize = 36;
	private const uint ImageChunkVersion = 1;
	private const int TocEntrySize = 12;
	private const int BytesPerPixel = 4;
	private const int MaxImageDimension = 4096;
	private const string AniExtension = ".ani";

	public static readonly IReadOnlyDictionary<string, string[]> RoleAliases = new Dictionary<string, string[]>
	{
		["Arrow"] = new[] { "left_ptr", "default", "arrow", "top_left_arrow" },
		["Help"] = new[] { "help", "left_ptr_help", "question_arrow" },
		["AppStarting"] = new[] { "left_ptr_watch", "progress", "half-busy" },
		["Wait"] = new[] { "wait", "watch" },
		["Crosshair"] = new[] { "crosshair", "cross" },
		["IBeam"] = new[] { "text", "xterm", "ibeam" },
		["NWPen"] = new[] { "pencil" },
		["No"] = new[] { "no-drop", "not-allowed", "crossed_circle" },
		["SizeNS"] = new[] { "ns-resize", "sb_v_double_arrow", "size_ver", "v_double_arrow", "n-resize", "s-resize", "row-resize" },
		["SizeWE"] = new[] { "ew-resize", "sb_h_double_arrow", "size_hor", "h_double_arrow", "e-resize", "w-resize", "col-resize" },
		["SizeNWSE"] = new[] { "nwse-resize", "size_fdiag", "fd_double_arrow", "nw-resize", "se-resize" },
		["SizeNESW"] = new[] { "nesw-resize", "size_bdiag", "bd_double_arrow", "ne-resize", "sw-resize" },
		["SizeAll"] = new[] { "move", "fleur", "all-scroll" },
		["UpArrow"] = new[] { "center_ptr", "up_arrow" },
		["Hand"] = new[] { "pointer", "hand2", "hand1", "pointing_hand" },
		["Person"] = new[] { "person" },
		["Pin"] = new[] { "pin" },
	};

	public static readonly IReadOnlyDictionary<string, string> AliasToRole = RoleAliases
		.SelectMany(pair => pair.Value.Select(alias => (Alias: alias, Role: pair.Key)))
		.GroupBy(entry => entry.Alias, StringComparer.OrdinalIgnoreCase)
		.ToDictionary(group => group.Key, group => group.First().Role, StringComparer.OrdinalIgnoreCase);

	public static List<XcursorFrame>? TryParse(byte[] bytes)
	{
		try
		{
			if (bytes.Length < FileHeaderSize || BitConverter.ToUInt32(bytes, 0) != Magic)
				return null;

			var headerSize = (int)BitConverter.ToUInt32(bytes, 4);
			var tocCount = (int)BitConverter.ToUInt32(bytes, 12);

			var imageEntries = new List<(uint Subtype, uint Position)>();

			for (var index = 0; index < tocCount; index++)
			{
				var entryOffset = headerSize + index * TocEntrySize;

				if (entryOffset + TocEntrySize > bytes.Length)
					break;

				var type = BitConverter.ToUInt32(bytes, entryOffset);
				var subtype = BitConverter.ToUInt32(bytes, entryOffset + 4);
				var position = BitConverter.ToUInt32(bytes, entryOffset + 8);

				if (type == ImageChunkType)
					imageEntries.Add((subtype, position));
			}

			if (imageEntries.Count == 0)
				return null;

			var chosenSubtype = imageEntries
				.GroupBy(entry => entry.Subtype)
				.OrderByDescending(group => group.Count())
				.ThenByDescending(group => group.Key)
				.First().Key;

			var frames = new List<XcursorFrame>();

			foreach (var entry in imageEntries.Where(entry => entry.Subtype == chosenSubtype))
			{
				var frame = TryParseImageChunk(bytes, (int)entry.Position);

				if (frame != null)
					frames.Add(frame);
			}

			return frames.Count > 0 ? frames : null;
		}
		catch
		{
			return null;
		}
	}

	private static XcursorFrame? TryParseImageChunk(byte[] bytes, int offset)
	{
		if (offset < 0 || offset + (int)ImageChunkHeaderSize > bytes.Length)
			return null;

		var width = (int)BitConverter.ToUInt32(bytes, offset + 16);
		var height = (int)BitConverter.ToUInt32(bytes, offset + 20);
		var hotspotX = (int)BitConverter.ToUInt32(bytes, offset + 24);
		var hotspotY = (int)BitConverter.ToUInt32(bytes, offset + 28);
		var delayMs = (int)BitConverter.ToUInt32(bytes, offset + 32);

		if (width <= 0 || height <= 0 || width > MaxImageDimension || height > MaxImageDimension)
			return null;

		var pixelOffset = offset + (int)ImageChunkHeaderSize;
		var byteCount = width * height * BytesPerPixel;

		if (pixelOffset + byteCount > bytes.Length)
			return null;

		var bgra = new byte[byteCount];

		for (var pixelIndex = 0; pixelIndex < byteCount; pixelIndex += BytesPerPixel)
		{
			var blue = bytes[pixelOffset + pixelIndex];
			var green = bytes[pixelOffset + pixelIndex + 1];
			var red = bytes[pixelOffset + pixelIndex + 2];
			var alpha = bytes[pixelOffset + pixelIndex + 3];

			if (alpha == 0)
			{
				bgra[pixelIndex] = 0;
				bgra[pixelIndex + 1] = 0;
				bgra[pixelIndex + 2] = 0;
				bgra[pixelIndex + 3] = 0;
				continue;
			}

			bgra[pixelIndex] = (byte)Math.Min(255, blue * 255 / alpha);
			bgra[pixelIndex + 1] = (byte)Math.Min(255, green * 255 / alpha);
			bgra[pixelIndex + 2] = (byte)Math.Min(255, red * 255 / alpha);
			bgra[pixelIndex + 3] = alpha;
		}

		return new XcursorFrame(width, height, hotspotX, hotspotY, bgra, delayMs);
	}

	public static List<XcursorFrame>? LoadFrames(string sourcePath)
	{
		if (string.Equals(Path.GetExtension(sourcePath), AniExtension, StringComparison.OrdinalIgnoreCase))
			return LoadAnimatedFrames(sourcePath);

		var image = CursorCanvasService.TryRead(sourcePath);

		return image == null
			? null
			: new List<XcursorFrame> { new(image.Width, image.Height, image.HotspotX, image.HotspotY, image.Bgra, 0) };
	}

	private static List<XcursorFrame>? LoadAnimatedFrames(string sourcePath)
	{
		var animated = AniCursorReader.Read(sourcePath);

		if (animated == null || animated.Frames.Count == 0 || animated.StepFrameIndices.Count == 0)
			return null;

		var (hotspotX, hotspotY) = ReadFirstFrameHotspot(sourcePath);
		var frames = new List<XcursorFrame>(animated.StepFrameIndices.Count);

		for (var step = 0; step < animated.StepFrameIndices.Count; step++)
		{
			var image = animated.Frames[animated.StepFrameIndices[step]];

			var delayMs = Math.Max(1, (int)Math.Round(animated.StepDurations[step].TotalMilliseconds));
			frames.Add(new XcursorFrame(image.Width, image.Height, hotspotX, hotspotY, image.Bgra, delayMs));
		}

		return frames;
	}

	private static (int X, int Y) ReadFirstFrameHotspot(string sourcePath)
	{
		try
		{
			var bytes = File.ReadAllBytes(sourcePath);
			var ranges = AniCursorReader.FindIconChunkRanges(bytes);

			if (ranges.Count == 0)
				return (0, 0);

			var (offset, length) = ranges[0];
			var chunkBytes = new byte[length];
			Array.Copy(bytes, offset, chunkBytes, 0, length);

			var firstImage = CursorCanvasService.TryReadFromBytes(chunkBytes);

			return firstImage != null ? (firstImage.HotspotX, firstImage.HotspotY) : (0, 0);
		}
		catch
		{
			return (0, 0);
		}
	}

	public static byte[] Build(IReadOnlyList<XcursorFrame> frames)
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream);

		var tocCount = (uint)frames.Count;

		writer.Write(Magic);
		writer.Write(FileHeaderSize);
		writer.Write(FileVersion);
		writer.Write(tocCount);

		var chunkOffsets = new uint[frames.Count];
		var runningOffset = FileHeaderSize + tocCount * TocEntrySize;

		for (var index = 0; index < frames.Count; index++)
		{
			chunkOffsets[index] = runningOffset;
			runningOffset += ImageChunkHeaderSize + (uint)(frames[index].Width * frames[index].Height * BytesPerPixel);
		}

		for (var index = 0; index < frames.Count; index++)
		{
			writer.Write(ImageChunkType);
			writer.Write((uint)Math.Max(frames[index].Width, frames[index].Height));
			writer.Write(chunkOffsets[index]);
		}

		foreach (var frame in frames)
		{
			writer.Write(ImageChunkHeaderSize);
			writer.Write(ImageChunkType);
			writer.Write((uint)Math.Max(frame.Width, frame.Height));
			writer.Write(ImageChunkVersion);
			writer.Write((uint)frame.Width);
			writer.Write((uint)frame.Height);
			writer.Write((uint)Math.Clamp(frame.HotspotX, 0, frame.Width));
			writer.Write((uint)Math.Clamp(frame.HotspotY, 0, frame.Height));
			writer.Write((uint)frame.DelayMs);

			WritePremultipliedPixels(writer, frame);
		}

		return stream.ToArray();
	}

	private static void WritePremultipliedPixels(BinaryWriter writer, XcursorFrame frame)
	{
		var stride = frame.Width * BytesPerPixel;

		for (var y = 0; y < frame.Height; y++)
		{
			for (var x = 0; x < frame.Width; x++)
			{
				var offset = y * stride + x * BytesPerPixel;
				var blue = frame.Bgra[offset];
				var green = frame.Bgra[offset + 1];
				var red = frame.Bgra[offset + 2];
				var alpha = frame.Bgra[offset + 3];

				writer.Write((byte)(blue * alpha / 255));
				writer.Write((byte)(green * alpha / 255));
				writer.Write((byte)(red * alpha / 255));
				writer.Write(alpha);
			}
		}
	}
}
