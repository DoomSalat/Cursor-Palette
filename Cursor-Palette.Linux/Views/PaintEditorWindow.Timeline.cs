using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CursorPalette.Services;
using System.Runtime.InteropServices;

namespace CursorPalette.Linux.Views;

public partial class PaintEditorWindow
{
	private const int MaxTimelineFrames = 60;
	private const int MinFrameDurationMs = 20;
	private const int MaxFrameDurationMs = 10000;
	private const int DefaultFrameDurationMs = 100;

	private const int ThumbnailSize = 32;
	private const int ThumbnailMargin = 2;
	private const int ActiveFrameBorderThickness = 2;
	private const int InactiveFrameBorderThickness = 1;
	private const double CanvasToolPanelLabelFontSize = 11;
	private const double ApplyButtonPaddingHorizontal = 12;
	private const double ApplyButtonPaddingVertical = 4;

	private sealed record TimelineFrame(byte[] SpriteBgra, int SpriteWidth, int SpriteHeight, int OffsetX, int OffsetY, int DurationMs);

	private readonly List<TimelineFrame> _timelineFrames = new();
	private int _activeFrameIndex;
	private bool _isPlaying;
	private DispatcherTimer? _playbackTimer;

	private bool IsAnimated => _timelineFrames.Count > 1;

	private void BuildBrushToolPanel()
	{
		_brushToolPanel.Children.Add(new TextBlock { Text = "Brush", FontWeight = FontWeight.SemiBold });
		_brushToolPanel.Children.Add(_colorWheel);
		_brushToolPanel.Children.Add(new TextBlock
		{
			Text = "Click and drag to paint.\nShift+click for line drawing.",
			FontSize = CanvasToolPanelLabelFontSize,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0, ToolPanelSpacing, 0, 0),
		});
	}

	private void BuildEraserToolPanel()
	{
		_eraserToolPanel.Children.Add(new TextBlock { Text = "Eraser", FontWeight = FontWeight.SemiBold });
		_eraserToolPanel.Children.Add(new TextBlock
		{
			Text = "Click and drag to erase.\nShift+click for line erasing.",
			FontSize = CanvasToolPanelLabelFontSize,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0, ToolPanelSpacing, 0, 0),
		});
	}

	private void BuildFillToolPanel()
	{
		_fillToolPanel.Children.Add(new TextBlock { Text = "Fill", FontWeight = FontWeight.SemiBold });
		_fillToolPanel.Children.Add(_colorWheel);
		_fillToolPanel.Children.Add(new TextBlock
		{
			Text = "Click to flood fill an area.",
			FontSize = CanvasToolPanelLabelFontSize,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0, ToolPanelSpacing, 0, 0),
		});
	}

	private void BuildCanvasToolPanel()
	{
		_canvasToolPanel.Children.Add(new TextBlock { Text = "Canvas", FontWeight = FontWeight.SemiBold });
		_canvasToolPanel.Children.Add(new TextBlock
		{
			Text = "Drag the edges or corners to resize the canvas.\nClick Apply to confirm or switch tools to cancel.",
			FontSize = CanvasToolPanelLabelFontSize,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0, ToolPanelSpacing, 0, 0),
		});
		var applyButton = new Button { Content = "Apply", Padding = new Thickness(ApplyButtonPaddingHorizontal, ApplyButtonPaddingVertical, ApplyButtonPaddingHorizontal, ApplyButtonPaddingVertical), Margin = new Thickness(0, ToolPanelSpacing, 0, 0) };
		applyButton.Click += OnCanvasResizeApply;
		_canvasToolPanel.Children.Add(applyButton);
	}

	private void OnCanvasResizeApply(object? sender, RoutedEventArgs e)
	{
		if (!_hasCanvasResizeSnapshot)
			return;
		PushHistory();
		_hasCanvasResizeSnapshot = false;
		SetTool(AppState.PaintEditorToolMove);
	}

	private void UpdateResizeOverlay()
	{
		if (!_resizeOverlay.IsVisible)
			return;

		var inverseZoom = 1.0 / _zoom;

		Avalonia.Controls.Canvas.SetLeft(_canvasBorderRect, 0);
		Avalonia.Controls.Canvas.SetTop(_canvasBorderRect, 0);
		_canvasBorderRect.Width = _canvasWidth;
		_canvasBorderRect.Height = _canvasHeight;
		_canvasBorderRect.StrokeThickness = BorderStrokePx * inverseZoom;
	}

	private bool _isResizeDragging;
	private Point _resizeDragStartScreen;
	private double _resizeDragStartCanvasX;
	private double _resizeDragStartCanvasY;
	private int _resizeEdgeFlags;

	private const int ResizeEdgeLeft = 1;
	private const int ResizeEdgeRight = 2;
	private const int ResizeEdgeTop = 4;
	private const int ResizeEdgeBottom = 8;
	private const double ResizeEdgeThreshold = 0.15;

	private void StartResizeDrag(Point screenPos)
	{
		_isResizeDragging = true;
		_resizeOriginalWidth = _canvasWidth;
		_resizeOriginalHeight = _canvasHeight;
		_resizeOriginalOffsetX = _offsetX;
		_resizeOriginalOffsetY = _offsetY;
		_resizeOriginalPanX = _panTransform.X;
		_resizeOriginalPanY = _panTransform.Y;
		_resizeAccumulatorX = 0;
		_resizeAccumulatorY = 0;
		_resizeDragStartScreen = screenPos;

		var canvasX = (screenPos.X - _panTransform.X) / _zoom;
		var canvasY = (screenPos.Y - _panTransform.Y) / _zoom;
		_resizeDragStartCanvasX = canvasX;
		_resizeDragStartCanvasY = canvasY;

		var relX = canvasX / _canvasWidth;
		var relY = canvasY / _canvasHeight;

		_resizeEdgeFlags = 0;
		if (relX < ResizeEdgeThreshold)
			_resizeEdgeFlags |= ResizeEdgeLeft;
		else if (relX > 1 - ResizeEdgeThreshold)
			_resizeEdgeFlags |= ResizeEdgeRight;
		if (relY < ResizeEdgeThreshold)
			_resizeEdgeFlags |= ResizeEdgeTop;
		else if (relY > 1 - ResizeEdgeThreshold)
			_resizeEdgeFlags |= ResizeEdgeBottom;

		if (_resizeEdgeFlags == 0)
			_resizeEdgeFlags = ResizeEdgeRight | ResizeEdgeBottom;
	}

	private void UpdateResizeDrag(Point screenPos)
	{
		var canvasX = (screenPos.X - _panTransform.X) / _zoom;
		var canvasY = (screenPos.Y - _panTransform.Y) / _zoom;
		var deltaX = canvasX - _resizeDragStartCanvasX;
		var deltaY = canvasY - _resizeDragStartCanvasY;

		var newWidth = _resizeOriginalWidth;
		var newHeight = _resizeOriginalHeight;
		var growthX = 0;
		var growthY = 0;

		if ((_resizeEdgeFlags & ResizeEdgeRight) != 0)
			newWidth = Math.Clamp(_resizeOriginalWidth + (int)Math.Round(deltaX), MinCanvasDimension, MaxCanvasDimension);
		else if ((_resizeEdgeFlags & ResizeEdgeLeft) != 0)
		{
			newWidth = Math.Clamp(_resizeOriginalWidth - (int)Math.Round(deltaX), MinCanvasDimension, MaxCanvasDimension);
			growthX = newWidth - _resizeOriginalWidth;
		}

		if ((_resizeEdgeFlags & ResizeEdgeBottom) != 0)
			newHeight = Math.Clamp(_resizeOriginalHeight + (int)Math.Round(deltaY), MinCanvasDimension, MaxCanvasDimension);
		else if ((_resizeEdgeFlags & ResizeEdgeTop) != 0)
		{
			newHeight = Math.Clamp(_resizeOriginalHeight - (int)Math.Round(deltaY), MinCanvasDimension, MaxCanvasDimension);
			growthY = newHeight - _resizeOriginalHeight;
		}

		_offsetX = _resizeOriginalOffsetX + growthX;
		_offsetY = _resizeOriginalOffsetY + growthY;
		_canvasWidth = newWidth;
		_canvasHeight = newHeight;

		_panTransform.X = _resizeOriginalPanX - growthX * _zoom;
		_panTransform.Y = _resizeOriginalPanY - growthY * _zoom;

		ClampOffset();
		RenderAll();
		UpdateCanvasSizeLabel();
	}

	private TimelineFrame CaptureCurrentAsFrame() =>
		new((byte[])_spriteBgra.Clone(), _spriteWidth, _spriteHeight,
			_offsetX, _offsetY, GetCurrentFrameDuration());

	private int GetCurrentFrameDuration() =>
		_timelineFrames.Count > 0 ? _timelineFrames[_activeFrameIndex].DurationMs : DefaultFrameDurationMs;

	private void ApplyFrame(TimelineFrame frame)
	{
		_spriteBgra = (byte[])frame.SpriteBgra.Clone();
		_spriteWidth = frame.SpriteWidth;
		_spriteHeight = frame.SpriteHeight;
		_offsetX = frame.OffsetX;
		_offsetY = frame.OffsetY;
		_hasLastStrokeEnd = false;
	}

	private void InitTimeline()
	{
		_timelineFrames.Add(CaptureCurrentAsFrame());
		_activeFrameIndex = 0;
		UpdateTimelineButtons();
		RebuildFrameStrip();
	}

	private void InitTimelineFromFrames(IReadOnlyList<CursorCanvasImage> frames, IReadOnlyList<int> frameDelaysMs)
	{
		_timelineFrames.Clear();
		for (var i = 0; i < frames.Count && i < MaxTimelineFrames; i++)
		{
			var currentFrame = frames[i];
			var bounds = FindOpaqueBounds(currentFrame);
			var sprite = ExtractRegion(currentFrame.Bgra, currentFrame.Width, bounds);
			var duration = i < frameDelaysMs.Count
				? Math.Clamp(frameDelaysMs[i], MinFrameDurationMs, MaxFrameDurationMs)
				: DefaultFrameDurationMs;
			_timelineFrames.Add(new TimelineFrame(sprite, bounds.Width, bounds.Height, bounds.X, bounds.Y, duration));
		}
		_canvasWidth = frames[0].Width;
		_canvasHeight = frames[0].Height;
		_activeFrameIndex = 0;
		ApplyFrame(_timelineFrames[0]);
		RebuildFrameStrip();
		UpdateTimelineButtons();
		_exportGifButton.IsVisible = true;
		_timelinePanel.IsVisible = true;
		RenderAll();
	}

	private void RebuildFrameStrip()
	{
		_timelineFramesPanel.Children.Clear();
		for (var i = 0; i < _timelineFrames.Count; i++)
		{
			var frameIndex = i;
			var frame = _timelineFrames[i];
			var bitmap = new WriteableBitmap(
				new PixelSize(_canvasWidth, _canvasHeight),
				new Vector(Dpi, Dpi),
				Avalonia.Platform.PixelFormat.Bgra8888,
				Avalonia.Platform.AlphaFormat.Unpremul);
			var buffer = new byte[_canvasWidth * _canvasHeight * BytesPerPixel];
			Blit(buffer, _canvasWidth, _canvasHeight, frame.SpriteBgra, frame.SpriteWidth, frame.SpriteHeight, frame.OffsetX, frame.OffsetY);
			using var lockedBitmap = bitmap.Lock();
			Marshal.Copy(buffer, 0, lockedBitmap.Address, buffer.Length);

			var thumbnail = new Image
			{
				Source = bitmap,
				Width = ThumbnailSize,
				Height = ThumbnailSize,
				Stretch = Stretch.Uniform,
				Margin = new Thickness(ThumbnailMargin),
			};
			var border = new Border
			{
				Child = thumbnail,
				BorderBrush = frameIndex == _activeFrameIndex ? Brushes.DodgerBlue : Brushes.Gray,
				BorderThickness = new Thickness(frameIndex == _activeFrameIndex ? ActiveFrameBorderThickness : InactiveFrameBorderThickness),
				Tag = frameIndex,
				Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
			};
			border.PointerPressed += (_, eventArgs) => SelectFrame(frameIndex);
			_timelineFramesPanel.Children.Add(border);
		}
	}

	private void SelectFrame(int index)
	{
		if (index < 0 || index >= _timelineFrames.Count)
			return;
		_timelineFrames[_activeFrameIndex] = CaptureCurrentAsFrame();
		_activeFrameIndex = index;
		ApplyFrame(_timelineFrames[index]);
		RebuildFrameStrip();
		UpdateTimelineButtons();
		RenderAll();
	}

	private void UpdateTimelineButtons()
	{
		_removeFrameButton.IsEnabled = _timelineFrames.Count > 1;
		_frameStatusLabel.Text = $"{_activeFrameIndex + 1} / {_timelineFrames.Count}";
		if (_timelineFrames.Count > 0)
			_frameDurationBox.Text = _timelineFrames[_activeFrameIndex].DurationMs.ToString();
		_exportGifButton.IsVisible = IsAnimated;
		_timelinePanel.IsVisible = IsAnimated;
	}

	private void OnAddFrameClick(object? sender, RoutedEventArgs e)
	{
		if (_timelineFrames.Count >= MaxTimelineFrames)
			return;
		_timelineFrames[_activeFrameIndex] = CaptureCurrentAsFrame();
		var newFrame = CaptureCurrentAsFrame();
		_timelineFrames.Insert(_activeFrameIndex + 1, newFrame);
		_activeFrameIndex++;
		RebuildFrameStrip();
		UpdateTimelineButtons();
		RenderAll();
	}

	private void OnRemoveFrameClick(object? sender, RoutedEventArgs e)
	{
		if (_timelineFrames.Count <= 1)
			return;
		_timelineFrames.RemoveAt(_activeFrameIndex);
		if (_activeFrameIndex >= _timelineFrames.Count)
			_activeFrameIndex = _timelineFrames.Count - 1;
		ApplyFrame(_timelineFrames[_activeFrameIndex]);
		RebuildFrameStrip();
		UpdateTimelineButtons();
		RenderAll();
	}

	private void OnPlayStopClick(object? sender, RoutedEventArgs e)
	{
		if (_isPlaying)
			StopTimelinePlayback();
		else
			StartTimelinePlayback();
	}

	private void StartTimelinePlayback()
	{
		if (_timelineFrames.Count <= 1)
			return;
		_isPlaying = true;
		_playStopButton.Content = "⏹";
		_timelineFrames[_activeFrameIndex] = CaptureCurrentAsFrame();
		_playbackTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(_timelineFrames[_activeFrameIndex].DurationMs),
		};
		_playbackTimer.Tick += OnPlaybackTick;
		_playbackTimer.Start();
	}

	private void OnPlaybackTick(object? sender, EventArgs e)
	{
		if (!_isPlaying)
			return;
		_playbackTimer!.Stop();
		var nextIndex = (_activeFrameIndex + 1) % _timelineFrames.Count;
		_activeFrameIndex = nextIndex;
		ApplyFrame(_timelineFrames[nextIndex]);
		RebuildFrameStrip();
		RenderAll();
		_playbackTimer.Interval = TimeSpan.FromMilliseconds(_timelineFrames[_activeFrameIndex].DurationMs);
		_playbackTimer.Start();
	}

	private void StopTimelinePlayback()
	{
		if (!_isPlaying)
			return;
		_isPlaying = false;
		_playStopButton.Content = "▶";
		_playbackTimer?.Stop();
		_playbackTimer = null;
	}

	private void OnFrameDurationChanged(object? sender, TextChangedEventArgs e)
	{
		if (_timelineFrames.Count == 0)
			return;
		if (int.TryParse(_frameDurationBox.Text, out var duration))
		{
			duration = Math.Clamp(duration, MinFrameDurationMs, MaxFrameDurationMs);
			var frame = _timelineFrames[_activeFrameIndex];
			_timelineFrames[_activeFrameIndex] = frame with { DurationMs = duration };
		}
	}
}
