using Avalonia;
using Avalonia.Media.Imaging;
using CursorPalette.Services;
using System.Runtime.InteropServices;

namespace CursorPalette.Linux.Views;

public partial class PaintEditorWindow
{
	private const string GifExtension = ".gif";
	private const int DefaultGifFrameDelayMs = 100;
	private const int CentisecondsToMs = 10;

	private sealed record GifRawFrame(byte[] Bgra, int Width, int Height, int Left, int Top, int DelayMs, int Disposal);

	private bool TryImportAnimatedGif(string path)
	{
		if (!string.Equals(Path.GetExtension(path), GifExtension, StringComparison.OrdinalIgnoreCase))
			return false;

		var decoded = DecodeGifRawFrames(path);
		if (decoded == null || decoded.Value.Frames.Count <= 1)
			return false;

		var (width, height, rawFrames) = decoded.Value;
		var composed = ComposeGifFrames(width, height, rawFrames);
		ApplyImportedGifFrames(width, height, composed);
		return true;
	}

	private static (int Width, int Height, List<GifRawFrame> Frames)? DecodeGifRawFrames(string path)
	{
		try
		{
			var data = File.ReadAllBytes(path);
			return DecodeGif(data);
		}
		catch
		{
			return null;
		}
	}

	private static (int Width, int Height, List<GifRawFrame> Frames)? DecodeGif(byte[] data)
	{
		if (data.Length < 13)
			return null;
		if (data[0] != 'G' || data[1] != 'I' || data[2] != 'F')
			return null;

		var canvasWidth = data[6] | (data[7] << 8);
		var canvasHeight = data[8] | (data[9] << 8);
		var packed = data[10];
		var hasGlobalTable = (packed & 0x80) != 0;
		var globalTableSize = 1 << ((packed & 0x07) + 1);

		var pos = 13;
		byte[]? globalPalette = null;

		if (hasGlobalTable)
		{
			globalPalette = new byte[globalTableSize * 3];
			Array.Copy(data, pos, globalPalette, 0, globalPalette.Length);
			pos += globalPalette.Length;
		}

		var frames = new List<GifRawFrame>();
		var pendingDelayCs = 0;
		var pendingDisposal = 0;
		var transparentIndex = -1;

		while (pos < data.Length)
		{
			var blockType = data[pos++];

			if (blockType == 0x3B)
				break;

			if (blockType == 0x21)
			{
				var label = data[pos++];
				if (label == 0xF9)
				{
					var blockSize = data[pos++];
					var flags = data[pos + 1];
					pendingDelayCs = data[pos + 2] | (data[pos + 3] << 8);
					transparentIndex = (flags & 0x01) != 0 ? data[pos + 4] : -1;
					pendingDisposal = (flags >> 2) & 0x07;
					pos += blockSize + 1;

					while (pos < data.Length && data[pos] != 0)
						pos += data[pos] + 1;
					pos++;
					continue;
				}
				else
				{
					while (pos < data.Length && data[pos] != 0)
						pos += data[pos] + 1;
					pos++;
					continue;
				}
			}

			if (blockType == 0x2C)
			{
				if (pos + 9 > data.Length)
					break;

				var left = data[pos] | (data[pos + 1] << 8);
				var top = data[pos + 2] | (data[pos + 3] << 8);
				var frameWidth = data[pos + 4] | (data[pos + 5] << 8);
				var frameHeight = data[pos + 6] | (data[pos + 7] << 8);
				var framePacked = data[pos + 8];
				pos += 9;

				var frameHasLocalTable = (framePacked & 0x80) != 0;
				var localTableSize = 1 << ((framePacked & 0x07) + 1);
				var frameTransparent = (framePacked & 0x01) != 0 ? transparentIndex : -1;

				byte[] palette;
				if (frameHasLocalTable)
				{
					palette = new byte[localTableSize * 3];
					Array.Copy(data, pos, palette, 0, palette.Length);
					pos += palette.Length;
				}
				else
				{
					palette = globalPalette ?? new byte[256 * 3];
				}

				var lzwMinCodeSize = data[pos++];
				var compressedData = ReadSubBlocks(data, ref pos);
				var indices = LzwDecode(compressedData, lzwMinCodeSize, frameWidth * frameHeight);

				var bgra = IndicesToBgra(indices, frameWidth, frameHeight, palette, frameTransparent);

				var delayMs = pendingDelayCs > 0
					? pendingDelayCs * CentisecondsToMs
					: DefaultGifFrameDelayMs;

				frames.Add(new GifRawFrame(bgra, frameWidth, frameHeight, left, top, delayMs, pendingDisposal));

				pendingDelayCs = 0;
				pendingDisposal = 0;
				transparentIndex = -1;
				continue;
			}

			break;
		}

		if (frames.Count == 0)
			return null;

		canvasWidth = Math.Max(canvasWidth, frames.Max(f => f.Left + f.Width));
		canvasHeight = Math.Max(canvasHeight, frames.Max(f => f.Top + f.Height));

		return (canvasWidth, canvasHeight, frames);
	}

	private static byte[] ReadSubBlocks(byte[] data, ref int pos)
	{
		var result = new List<byte>();
		while (pos < data.Length && data[pos] != 0)
		{
			var len = data[pos++];
			if (pos + len > data.Length)
				break;
			result.AddRange(data.Skip(pos).Take(len));
			pos += len;
		}
		pos++;
		return result.ToArray();
	}

	private static byte[] LzwDecode(byte[] data, int minCodeSize, int expectedPixels)
	{
		var clearCode = 1 << minCodeSize;
		var endCode = clearCode + 1;
		var codeSize = minCodeSize + 1;
		var dict = new List<byte[]>();
		var result = new byte[expectedPixels];
		var resultPos = 0;
		var bitPos = 0;
		var prevCode = -1;

		ResetDict();
		void ResetDict()
		{
			dict.Clear();
			for (var i = 0; i < clearCode; i++)
				dict.Add(new byte[] { (byte)i });
			dict.Add(Array.Empty<byte>());
			dict.Add(Array.Empty<byte>());
			codeSize = minCodeSize + 1;
		}

		while (resultPos < expectedPixels)
		{
			var code = ReadBits(data, ref bitPos, codeSize);
			if (code == clearCode)
			{
				ResetDict();
				prevCode = -1;
				continue;
			}
			if (code == endCode)
				break;

			byte[] entry;
			if (code < dict.Count)
			{
				entry = dict[code];
			}
			else if (code == dict.Count && prevCode >= 0)
			{
				entry = new byte[dict[prevCode].Length + 1];
				Array.Copy(dict[prevCode], entry, dict[prevCode].Length);
				entry[^1] = dict[prevCode][0];
			}
			else
			{
				break;
			}

			Array.Copy(entry, 0, result, resultPos, Math.Min(entry.Length, expectedPixels - resultPos));
			resultPos += entry.Length;

			if (prevCode >= 0 && dict.Count < 4096)
			{
				var newEntry = new byte[dict[prevCode].Length + 1];
				Array.Copy(dict[prevCode], newEntry, dict[prevCode].Length);
				newEntry[^1] = entry[0];
				dict.Add(newEntry);
				if (dict.Count == (1 << codeSize) && codeSize < 12)
					codeSize++;
			}

			prevCode = code;
		}

		return result;
	}

	private static int ReadBits(byte[] data, ref int bitPos, int bitCount)
	{
		var result = 0;
		for (var i = 0; i < bitCount; i++)
		{
			var byteIndex = bitPos / 8;
			if (byteIndex >= data.Length)
				return 0;
			var bitIndex = bitPos % 8;
			if ((data[byteIndex] & (1 << bitIndex)) != 0)
				result |= 1 << i;
			bitPos++;
		}
		return result;
	}

	private static byte[] IndicesToBgra(byte[] indices, int width, int height, byte[] palette, int transparentIndex)
	{
		var bgra = new byte[width * height * BytesPerPixel];
		for (var i = 0; i < indices.Length && i < width * height; i++)
		{
			var idx = indices[i];
			var offset = i * BytesPerPixel;
			if (idx == transparentIndex)
			{
				bgra[offset] = 0;
				bgra[offset + 1] = 0;
				bgra[offset + 2] = 0;
				bgra[offset + 3] = 0;
			}
			else
			{
				var palOffset = idx * 3;
				if (palOffset + 2 < palette.Length)
				{
					bgra[offset] = palette[palOffset + 2];
					bgra[offset + 1] = palette[palOffset + 1];
					bgra[offset + 2] = palette[palOffset];
					bgra[offset + 3] = 255;
				}
			}
		}
		return bgra;
	}

	private static List<(byte[] Bgra, int DelayMs)> ComposeGifFrames(int width, int height, List<GifRawFrame> rawFrames)
	{
		var canvas = new byte[width * height * BytesPerPixel];
		var result = new List<(byte[] Bgra, int DelayMs)>(rawFrames.Count);

		foreach (var raw in rawFrames)
		{
			var restoreSnapshot = raw.Disposal == 3 ? (byte[])canvas.Clone() : null;

			AlphaComposite(canvas, width, height, raw.Bgra, raw.Width, raw.Height, raw.Left, raw.Top);

			result.Add(((byte[])canvas.Clone(), raw.DelayMs));

			if (raw.Disposal == 2)
				ClearRect(canvas, width, height, raw.Left, raw.Top, raw.Width, raw.Height);
			else if (raw.Disposal == 3 && restoreSnapshot != null)
				canvas = restoreSnapshot;
		}

		return result;
	}

	private void ApplyImportedGifFrames(int width, int height, List<(byte[] Bgra, int DelayMs)> frames)
	{
		var limited = frames.Count > MaxTimelineFrames ? frames.GetRange(0, MaxTimelineFrames) : frames;
		if (limited.Count == 0)
			return;

		var clampedWidth = Math.Clamp(width, MinCanvasDimension, MaxCanvasDimension);
		var clampedHeight = Math.Clamp(height, MinCanvasDimension, MaxCanvasDimension);

		_timelineFrames.Clear();

		foreach (var (bgra, delayMs) in limited)
		{
			var durationMs = Math.Clamp(delayMs, MinFrameDurationMs, MaxFrameDurationMs);
			_timelineFrames.Add(new TimelineFrame(bgra, clampedWidth, clampedHeight, 0, 0, durationMs));
		}

		_canvasWidth = clampedWidth;
		_canvasHeight = clampedHeight;
		_activeFrameIndex = 0;

		ApplyFrame(_timelineFrames[0]);
		RebuildFrameStrip();
		UpdateTimelineButtons();
		_exportGifButton.IsVisible = true;
		_timelinePanel.IsVisible = true;
		RenderAll();
	}
}
