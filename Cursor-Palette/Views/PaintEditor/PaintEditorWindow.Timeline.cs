using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class PaintEditorWindow
{
	private const int MaxTimelineFrames = 60;
	private const int MinFrameDurationMs = 17;
	private const int MaxFrameDurationMs = 2000;
	private const int DefaultFrameDurationMs = 100;
	private const string FrameButtonContentFormat = "{0}";

	private sealed record TimelineFrame(
		byte[] SpriteBgra, int SpriteWidth, int SpriteHeight, int OffsetX, int OffsetY, int DurationMs);

	private readonly List<TimelineFrame> _timelineFrames = new();
	private int _activeFrameIndex;
	private int _activeFrameDurationMs = DefaultFrameDurationMs;
	private bool _isPlayingTimeline;
	private bool _refManualMode;
	private DispatcherTimer? _playbackTimer;

	private bool IsAnimated => _timelineFrames.Count > 1;

	private void InitTimeline()
	{
		_timelineFrames.Clear();
		_timelineFrames.Add(CaptureCurrentAsFrame());
		_activeFrameIndex = 0;

		RebuildFrameStrip();
		UpdateTimelineButtons();
	}

	private void InitTimelineFromFrames(IReadOnlyList<CursorCanvasImage> frames, IReadOnlyList<int> frameDelaysMs)
	{
		_timelineFrames.Clear();

		var limit = Math.Min(frames.Count, MaxTimelineFrames);

		for (var i = 0; i < limit; i++)
		{
			var bounds = FindOpaqueBounds(frames[i]);
			var spriteBgra = ExtractRegion(frames[i].Bgra, frames[i].Width, bounds);
			var durationMs = i < frameDelaysMs.Count
				? Math.Clamp(frameDelaysMs[i], MinFrameDurationMs, MaxFrameDurationMs)
				: DefaultFrameDurationMs;

			_timelineFrames.Add(new TimelineFrame(spriteBgra, bounds.Width, bounds.Height, bounds.X, bounds.Y, durationMs));
		}

		_activeFrameIndex = 0;
		ApplyFrame(_timelineFrames[0]);

		RebuildFrameStrip();
		UpdateTimelineButtons();
	}

	private TimelineFrame CaptureCurrentAsFrame() =>
		new((byte[])_spriteBgra.Clone(), _spriteWidth, _spriteHeight, _offsetX, _offsetY, _activeFrameDurationMs);

	private void ApplyFrame(TimelineFrame frame)
	{
		_spriteBgra = (byte[])frame.SpriteBgra.Clone();
		_spriteWidth = frame.SpriteWidth;
		_spriteHeight = frame.SpriteHeight;
		_offsetX = frame.OffsetX;
		_offsetY = frame.OffsetY;
		_activeFrameDurationMs = frame.DurationMs;

		ClearHistory();
		UpdateUndoRedoButtons();
		_hasLastStrokeEnd = false;

		RenderAll();
		UpdateFrameDurationBox();
	}

	private void SwitchToFrame(int index)
	{
		if (index < 0 || index >= _timelineFrames.Count || index == _activeFrameIndex)
			return;

		_timelineFrames[_activeFrameIndex] = CaptureCurrentAsFrame();
		_activeFrameIndex = index;
		ApplyFrame(_timelineFrames[_activeFrameIndex]);

		RebuildFrameStrip();
		SyncRefFrameToTimeline();
	}

	private void OnAddFrameClick(object sender, RoutedEventArgs e)
	{
		if (_timelineFrames.Count >= MaxTimelineFrames)
			return;

		_timelineFrames[_activeFrameIndex] = CaptureCurrentAsFrame();

		var duplicate = _timelineFrames[_activeFrameIndex] with { };
		_timelineFrames.Insert(_activeFrameIndex + 1, duplicate);
		_activeFrameIndex++;

		ApplyFrame(_timelineFrames[_activeFrameIndex]);
		RebuildFrameStrip();
		UpdateTimelineButtons();
		SyncRefFrameToTimeline();
	}

	private void OnRemoveFrameClick(object sender, RoutedEventArgs e)
	{
		if (_timelineFrames.Count <= 1)
			return;

		_timelineFrames.RemoveAt(_activeFrameIndex);
		_activeFrameIndex = Math.Clamp(_activeFrameIndex, 0, _timelineFrames.Count - 1);

		ApplyFrame(_timelineFrames[_activeFrameIndex]);
		RebuildFrameStrip();
		UpdateTimelineButtons();
		SyncRefFrameToTimeline();
	}

	private void RebuildFrameStrip()
	{
		TimelineFramesPanel.Children.Clear();

		for (var i = 0; i < _timelineFrames.Count; i++)
		{
			var index = i;
			var button = new Button
			{
				Content = string.Format(CultureInfo.InvariantCulture, FrameButtonContentFormat, i + 1),
				Style = (Style)Application.Current.Resources[
					i == _activeFrameIndex ? StyleAccentButton : StyleButton],
				Padding = new Thickness(10, 4, 10, 4),
				Margin = new Thickness(0, 0, 4, 0),
				MinWidth = 32,
				IsEnabled = !_isPlayingTimeline,
			};

			button.Click += (_, _) => SwitchToFrame(index);
			TimelineFramesPanel.Children.Add(button);
		}
	}

	private void UpdateTimelineButtons()
	{
		AddFrameButton.IsEnabled = !_isPlayingTimeline && _timelineFrames.Count < MaxTimelineFrames;
		RemoveFrameButton.IsEnabled = !_isPlayingTimeline && _timelineFrames.Count > 1;
		PlayStopButton.IsEnabled = _timelineFrames.Count > 1;
		FrameDurationBox.IsEnabled = !_isPlayingTimeline;
		ManualRefCheck.IsEnabled = !_isPlayingTimeline;
		ExportGifButton.Visibility = IsAnimated ? Visibility.Visible : Visibility.Collapsed;
	}

	private void UpdateFrameDurationBox() =>
		FrameDurationBox.Text = _activeFrameDurationMs.ToString(CultureInfo.InvariantCulture);

	private void OnFrameDurationChanged(object sender, TextChangedEventArgs e)
	{
		if (!_ready)
			return;

		if (!int.TryParse(FrameDurationBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
			return;

		_activeFrameDurationMs = Math.Clamp(value, MinFrameDurationMs, MaxFrameDurationMs);
		_timelineFrames[_activeFrameIndex] = _timelineFrames[_activeFrameIndex] with { DurationMs = _activeFrameDurationMs };
	}

	private void OnPlayStopClick(object sender, RoutedEventArgs e)
	{
		if (_isPlayingTimeline)
			StopTimelinePlayback();
		else
			StartTimelinePlayback();
	}

	private void StartTimelinePlayback()
	{
		if (_timelineFrames.Count <= 1 || _isPlayingTimeline)
			return;

		_timelineFrames[_activeFrameIndex] = CaptureCurrentAsFrame();
		_isPlayingTimeline = true;
		PlayStopButton.Content = "⏹";
		PlayStopButton.Style = (Style)Resources["Style.PlayStopButton.Playing"];
		UpdateTimelineButtons();
		RebuildFrameStrip();

		_playbackTimer = new DispatcherTimer();
		_playbackTimer.Tick += OnPlaybackTick;
		SchedulePlaybackTick();
	}

	private void SchedulePlaybackTick()
	{
		if (_playbackTimer == null)
			return;

		_playbackTimer.Stop();
		_playbackTimer.Interval = TimeSpan.FromMilliseconds(_timelineFrames[_activeFrameIndex].DurationMs);
		_playbackTimer.Start();
	}

	private void OnPlaybackTick(object? sender, EventArgs e)
	{
		var nextIndex = (_activeFrameIndex + 1) % _timelineFrames.Count;

		_activeFrameIndex = nextIndex;
		ApplyFrame(_timelineFrames[_activeFrameIndex]);
		RebuildFrameStrip();
		SyncRefFrameToTimeline();
		SchedulePlaybackTick();
	}

	private void StopTimelinePlayback()
	{
		if (!_isPlayingTimeline)
			return;

		_playbackTimer?.Stop();
		_playbackTimer = null;
		_isPlayingTimeline = false;
		PlayStopButton.Content = "▶";
		PlayStopButton.Style = (Style)Application.Current.Resources[StyleButton];

		UpdateTimelineButtons();
		RebuildFrameStrip();
	}

	private void OnManualRefChanged(object sender, RoutedEventArgs e)
	{
		_refManualMode = ManualRefCheck.IsChecked == true;

		UpdateBgRefManualControlsVisibility();

		if (!_refManualMode)
			SyncRefFrameToTimeline();
	}
}
