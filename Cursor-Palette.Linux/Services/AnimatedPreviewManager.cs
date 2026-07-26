using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CursorPalette.Services;

namespace CursorPalette.Linux.Services;

public static class AnimatedPreviewManager
{
	private static readonly Dictionary<Image, AnimationState> _activeAnimations = new();

	public static void TryAttach(Image image, string? filePath)
	{
		Detach(image);

		if (string.IsNullOrWhiteSpace(filePath))
			return;

		var expanded = Environment.ExpandEnvironmentVariables(filePath);

		if (!string.Equals(Path.GetExtension(expanded), ".ani", StringComparison.OrdinalIgnoreCase))
			return;

		if (!File.Exists(expanded))
			return;

		var animated = AniCursorReader.Read(expanded);
		if (animated == null || animated.Frames.Count == 0 || animated.StepFrameIndices.Count == 0)
			return;

		var bitmaps = new List<Bitmap>(animated.Frames.Count);
		foreach (var frame in animated.Frames)
			bitmaps.Add(CreateBitmap(frame));

		var state = new AnimationState(bitmaps, animated.StepFrameIndices, animated.StepDurations);
		_activeAnimations[image] = state;

		image.Source = bitmaps[animated.StepFrameIndices[0]];
		state.Timer = new DispatcherTimer
		{
			Interval = animated.StepDurations[0],
		};

		state.Timer.Tick += (_, _) => AdvanceFrame(image, state);
		state.Timer.Start();
	}

	public static void Detach(Image image)
	{
		if (_activeAnimations.TryGetValue(image, out var state))
		{
			state.Timer?.Stop();
			_activeAnimations.Remove(image);
		}
	}

	public static void DetachAll()
	{
		foreach (var state in _activeAnimations.Values)
			state.Timer?.Stop();

		_activeAnimations.Clear();
	}

	private static void AdvanceFrame(Image image, AnimationState state)
	{
		state.Timer!.Stop();
		state.CurrentStep = (state.CurrentStep + 1) % state.StepFrameIndices.Count;
		var frameIndex = state.StepFrameIndices[state.CurrentStep];
		image.Source = state.Bitmaps[frameIndex];
		state.Timer.Interval = state.StepDurations[state.CurrentStep];
		state.Timer.Start();
	}

	private static Bitmap CreateBitmap(CursorCanvasImage image)
	{
		var stride = image.Width * 4;
		using var stream = new MemoryStream(image.Bgra.Length + 122);
		using var writer = new BinaryWriter(stream);

		writer.Write((byte)'B');
		writer.Write((byte)'M');
		writer.Write(54 + image.Bgra.Length);
		writer.Write(0);
		writer.Write(54);

		writer.Write(40);
		writer.Write(image.Width);
		writer.Write(image.Height);
		writer.Write((ushort)1);
		writer.Write((ushort)32);
		writer.Write(0);
		writer.Write(image.Bgra.Length);
		writer.Write(0);
		writer.Write(0);
		writer.Write(0);
		writer.Write(0);

		for (var row = image.Height - 1; row >= 0; row--)
		{
			var offset = row * stride;
			writer.Write(image.Bgra, offset, stride);
		}

		stream.Position = 0;
		return new Bitmap(stream);
	}

	private sealed class AnimationState
	{
		public List<Bitmap> Bitmaps { get; }
		public IReadOnlyList<int> StepFrameIndices { get; }
		public IReadOnlyList<TimeSpan> StepDurations { get; }
		public int CurrentStep { get; set; }
		public DispatcherTimer? Timer { get; set; }

		public AnimationState(List<Bitmap> bitmaps, IReadOnlyList<int> stepFrameIndices, IReadOnlyList<TimeSpan> stepDurations)
		{
			Bitmaps = bitmaps;
			StepFrameIndices = stepFrameIndices;
			StepDurations = stepDurations;
		}
	}
}
