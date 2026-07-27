using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CursorPalette.Linux.Services;
using CursorPalette.Models;
using CursorPalette.Services;

namespace CursorPalette.Linux.Views;

public partial class PaintEditorWindow
{
	private const int IconSizesThumbnailBoxPx = 28;
	private const double IconSizesInactiveOpacity = 0.5;
	private const string LocIconSizesSummary = "S.Editor.Tool.IconSizesSummary";
	private const string LocIconSizesScaleMode = "S.Editor.Tool.IconSizesScaleMode";
	private const string LocIconSizesScaleModeFor = "S.Editor.Tool.IconSizesScaleModeFor";
	private const string LocIconSizesApplyAll = "S.Editor.Tool.IconSizesApplyAll";
	private const string LocIconSizesApplyOne = "S.Editor.Tool.IconSizesApplyOne";
	private const string LocIconSizesGenerateDefaults = "S.Editor.Tool.IconSizesGenerateDefaults";
	private const string LocIconSizesHint = "S.Editor.Tool.IconSizesHint";
	private const string LocIconSizesUnavailableHint = "S.Editor.Tool.IconSizesUnavailableHint";
	private const string LocIconSizesEditMode = "S.Editor.Tool.IconSizesEditMode";
	private const string LocIconSizesScaleReset = "S.Editor.Tool.IconSizesScaleReset";
	private const string LocToastIconSizeAppliedAll = "S.Toast.IconSizeAppliedAll";
	private const string LocToastIconSizeAppliedOne = "S.Toast.IconSizeAppliedOne";

	private static readonly HashSet<int> DefaultIconSizes = [32, 48, 64, 96, 128, 256];

	private readonly HashSet<int> _iconSizes = [];
	private readonly Dictionary<int, ScaleMode> _iconSizeScaleOverrides = [];
	private readonly Dictionary<int, CursorCanvasImage> _iconSizeCustomImages = [];
	private IReadOnlyList<CursorCanvasImage> _seededIconImages = [];
	private ScaleMode _iconSizesScaleMode = ScaleMode.AreaWeighted;
	private int? _iconSizesPreviewSize;
	private ScaleMode? _iconSizesPendingScaleOverride;
	private bool _iconSizesEditMode;

	private bool _hasIconSizesSnapshot;
	private byte[] _iconSizesSnapshotSpriteBgra = [];
	private int _iconSizesSnapshotSpriteWidth;
	private int _iconSizesSnapshotSpriteHeight;
	private int _iconSizesSnapshotCanvasWidth;
	private int _iconSizesSnapshotCanvasHeight;
	private int _iconSizesSnapshotOffsetX;
	private int _iconSizesSnapshotOffsetY;
	private List<TimelineFrame>? _iconSizesSnapshotTimelineFrames;

	public IReadOnlyList<int>? ResultIconSizes { get; private set; }
	public ScaleMode ResultIconSizesScaleMode { get; private set; }
	public IReadOnlyDictionary<int, ScaleMode>? ResultIconSizeScaleModeOverrides { get; private set; }
	public IReadOnlyDictionary<int, CursorCanvasImage>? ResultIconSizeCustomImages { get; private set; }

	private int IconSizesNativeSize => _hasIconSizesSnapshot ? _iconSizesSnapshotCanvasWidth : _canvasWidth;

	private bool IsIconSizesAvailable => _canvasWidth == _canvasHeight;

	private bool IsIconSizesEditingActive => _iconSizesEditMode && _iconSizesPreviewSize != null;

	private ScaleMode GetIconSizeEffectiveScaleMode(int size)
	{
		if (size == _iconSizesPreviewSize && _iconSizesPendingScaleOverride is { } pending)
			return pending;

		return _iconSizeScaleOverrides.TryGetValue(size, out var mode) ? mode : _iconSizesScaleMode;
	}

	private CursorCanvasImage GetIconSizeImage(CursorCanvasImage master, int size)
	{
		if (size != master.Width && _iconSizeCustomImages.TryGetValue(size, out var custom))
			return custom;

		return size == master.Width
			? master
			: CursorScalerService.ScaleImage(master, size, size, GetIconSizeEffectiveScaleMode(size));
	}

	private void SeedIconSizes(IReadOnlyList<CursorCanvasImage> images)
	{
		_seededIconImages = images;
		_iconSizeCustomImages.Clear();

		var nativeSize = _hasIconSizesSnapshot ? _iconSizesSnapshotCanvasWidth : _canvasWidth;

		foreach (var img in images)
		{
			_iconSizes.Add(img.Width);

			if (img.Width != nativeSize)
				_iconSizeCustomImages[img.Width] = img;
		}
	}

	private void RefreshIconSizesPanel()
	{
		_iconSizesUnavailableHint.Text = Loc.Get(LocIconSizesUnavailableHint);
		_iconSizesHintText.Text = Loc.Get(LocIconSizesHint);
		_iconSizesEditModeCheck.Content = Loc.Get(LocIconSizesEditMode);

		var available = IsIconSizesAvailable;

		_iconSizesUnavailableHint.IsVisible = !available;
		_iconSizesContentPanel.IsVisible = available;

		if (!available)
			return;

		UpdateIconSizesScaleModeHeader();
		UpdateIconSizesEditModeCheck();
		UpdateIconSizesAddRemoveEnabled();
		RebuildIconSizesList();
		UpdateIconSizesSummary();
		UpdateIconSizesApplyButton();
	}

	private void UpdateIconSizesEditModeCheck()
	{
		_iconSizesEditModeCheck.IsEnabled = IsIconSizesAvailable;
		_iconSizesEditModeCheck.IsChecked = _iconSizesEditMode;
	}

	private void UpdateIconSizesAddRemoveEnabled()
	{
		_iconSizesAddSizeButton.IsEnabled = _iconSizesEditMode;
		_iconSizesAddSizeBox.IsEnabled = _iconSizesEditMode;
	}

	private void OnIconSizesEditModeChanged(object? sender, RoutedEventArgs e)
	{
		var newValue = _iconSizesEditModeCheck.IsChecked == true;

		if (newValue == _iconSizesEditMode)
			return;

		if (!newValue)
		{
			CommitIconSizeEditIfNeeded();
			_iconSizeCustomImages.Clear();
			_iconSizeScaleOverrides.Clear();
			_iconSizesPendingScaleOverride = null;
			SeedIconSizes(_seededIconImages);
		}
		else
		{
			_iconSizeCustomImages.Clear();
		}

		_iconSizesEditMode = newValue;

		if (_iconSizesPreviewSize is { } size)
			ApplyIconSizesPreview(size, discardCustom: newValue);

		UpdateIconSizesScaleModeHeader();
		UpdateIconSizesAddRemoveEnabled();
		RebuildIconSizesList();
		UpdateIconSizesApplyButton();
	}

	private void UpdateIconSizesApplyButton()
	{
		var nativeSize = IconSizesNativeSize;
		var hasCustomSizes = _iconSizes.Any(s => s != nativeSize) ||
			_iconSizeCustomImages.Any(kv => kv.Key != nativeSize);

		if (!hasCustomSizes)
		{
			_iconSizesApplyButton.Content = Loc.Get(LocIconSizesGenerateDefaults);
			_iconSizesApplyButton.IsEnabled = true;
			return;
		}

		_iconSizesApplyButton.Content = _iconSizesPreviewSize is { } size
			? Loc.Format(LocIconSizesApplyOne, size)
			: Loc.Get(LocIconSizesApplyAll);

		_iconSizesApplyButton.IsEnabled = _iconSizesEditMode;
	}

	private void OnIconSizesApplyClick(object? sender, RoutedEventArgs e)
	{
		var nativeSize = IconSizesNativeSize;
		var hasCustomSizes = _iconSizes.Any(s => s != nativeSize) ||
			_iconSizeCustomImages.Any(kv => kv.Key != nativeSize);

		if (!hasCustomSizes)
		{
			GenerateDefaultIconSizes();
			return;
		}

		if (!_iconSizesEditMode)
			return;

		if (_iconSizesPreviewSize is not { } size)
		{
			CommitIconSizeEditIfNeeded();

			var master = GetIconSizesMasterImage();
			var allSizes = new SortedSet<int>(_iconSizes) { master.Width };

			foreach (var s in allSizes)
			{
				if (s == master.Width)
					continue;

				var mode = _iconSizeScaleOverrides.TryGetValue(s, out var ov) ? ov : _iconSizesScaleMode;
				_iconSizeCustomImages[s] = CursorScalerService.ScaleImage(master, s, s, mode);
			}

			RebuildIconSizesList();
			UpdateIconSizesApplyButton();
			ShowToast(Loc.Get(LocToastIconSizeAppliedAll));

			return;
		}

		if (_iconSizesPendingScaleOverride is { } pending)
		{
			_iconSizeScaleOverrides[size] = pending;
			_iconSizesPendingScaleOverride = null;
		}

		CommitIconSizeEditIfNeeded();

		ShowToast(Loc.Format(LocToastIconSizeAppliedOne, size));

		UpdateIconSizesScaleModeHeader();
		RebuildIconSizesList();
		UpdateIconSizesApplyButton();
	}

	private void GenerateDefaultIconSizes()
	{
		var master = GetIconSizesMasterImage();

		_iconSizes.Clear();
		_iconSizeCustomImages.Clear();
		_iconSizeScaleOverrides.Clear();

		foreach (var size in DefaultIconSizes)
		{
			if (size == master.Width)
				continue;

			_iconSizes.Add(size);
			_iconSizeCustomImages[size] = CursorScalerService.ScaleImage(master, size, size, _iconSizesScaleMode);
		}

		_iconSizesEditMode = true;
		_iconSizesEditModeCheck.IsChecked = true;

		RefreshIconSizesPanel();
		ShowToast(Loc.Get(LocToastIconSizeAppliedAll));
	}

	private void UpdateIconSizesScaleModeHeader()
	{
		var editingActive = IsIconSizesEditingActive;
		_iconSizesScaleModeIconBorder.IsEnabled = _iconSizesPreviewSize == null || editingActive;

		if (!_iconSizesEditMode && _iconSizesPreviewSize == null)
			_iconSizesScaleModeIconBorder.IsEnabled = false;

		_iconSizesScaleModeIconBorder.Opacity = _iconSizesScaleModeIconBorder.IsEnabled ? 1.0 : IconSizesInactiveOpacity;

		if (_iconSizesPreviewSize is { } size)
		{
			_iconSizesScaleModeLabel.Text = Loc.Format(LocIconSizesScaleModeFor, size);
			_iconSizesScaleModeIcon.Source = GetScaleModeIconImage(GetIconSizeEffectiveScaleMode(size));
			_iconSizesScaleModeResetButton.IsVisible = editingActive && (_iconSizeScaleOverrides.ContainsKey(size) || _iconSizesPendingScaleOverride.HasValue);
			ToolTip.SetTip(_iconSizesScaleModeResetButton, Loc.Get(LocIconSizesScaleReset));
		}
		else
		{
			_iconSizesScaleModeLabel.Text = Loc.Get(LocIconSizesScaleMode);
			_iconSizesScaleModeIcon.Source = GetScaleModeIconImage(_iconSizesScaleMode);
			_iconSizesScaleModeResetButton.IsVisible = false;
		}
	}

	private static Bitmap GetScaleModeIconImage(ScaleMode mode)
	{
		var fileName = mode == ScaleMode.NearestNeighbor ? "StairIcon24.png" : "ExpandIcon32.png";
		using var stream = AssetLoader.Open(new Uri($"avares://Cursor-Palette.Linux/Resources/{fileName}"));
		return new Bitmap(stream);
	}

	private void OnIconSizesScaleModeIconClick(object? sender, PointerPressedEventArgs e)
	{
		if (_iconSizesPreviewSize is { } size)
		{
			if (!IsIconSizesEditingActive)
				return;

			_iconSizesPendingScaleOverride = GetIconSizeEffectiveScaleMode(size) == ScaleMode.NearestNeighbor
				? ScaleMode.AreaWeighted
				: ScaleMode.NearestNeighbor;

			ApplyIconSizesPreview(size, discardCustom: true);
		}
		else
		{
			_iconSizesScaleMode = _iconSizesScaleMode == ScaleMode.NearestNeighbor
				? ScaleMode.AreaWeighted
				: ScaleMode.NearestNeighbor;
		}

		UpdateIconSizesScaleModeHeader();
		RebuildIconSizesList();
	}

	private void OnIconSizesScaleModeResetClick(object? sender, RoutedEventArgs e)
	{
		if (!IsIconSizesEditingActive || _iconSizesPreviewSize is not { } size)
			return;

		var hadOverride = _iconSizeScaleOverrides.Remove(size);
		var hadPending = _iconSizesPendingScaleOverride.HasValue;
		var hadCustomImage = _iconSizeCustomImages.Remove(size);
		_iconSizesPendingScaleOverride = null;

		if (!hadOverride && !hadPending && !hadCustomImage)
			return;

		ApplyIconSizesPreview(size, discardCustom: true);
		UpdateIconSizesScaleModeHeader();
		RebuildIconSizesList();
	}

	private void OnIconSizesAddSizeClick(object? sender, RoutedEventArgs e)
	{
		if (!_iconSizesEditMode)
			return;

		if (!int.TryParse(_iconSizesAddSizeBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size))
			return;

		size = Math.Clamp(size, MinCanvasDimension, MaxCanvasDimension);

		if (size != IconSizesNativeSize)
			_iconSizes.Add(size);

		_iconSizesAddSizeBox.Text = "";

		RebuildIconSizesList();
		UpdateIconSizesSummary();
	}

	private void RemoveIconSize(int size)
	{
		if (!_iconSizesEditMode)
			return;

		_iconSizes.Remove(size);
		_iconSizeScaleOverrides.Remove(size);
		_iconSizeCustomImages.Remove(size);

		if (_iconSizesPreviewSize == size)
			RestoreIconSizesPreview();

		UpdateIconSizesScaleModeHeader();
		UpdateIconSizesEditModeCheck();
		UpdateIconSizesAddRemoveEnabled();
		RebuildIconSizesList();
		UpdateIconSizesSummary();
		UpdateIconSizesApplyButton();
	}

	private void RebuildIconSizesList()
	{
		_iconSizesListPanel.Children.Clear();

		var master = GetIconSizesMasterImage();
		var nativeSize = master.Width;

		var sizes = new SortedSet<int>(_iconSizes) { nativeSize };
		var isFirst = true;

		foreach (var size in sizes)
		{
			if (!isFirst)
			{
				_iconSizesListPanel.Children.Add(new Border
				{
					Height = 1,
					Background = Brushes.Gray,
					Margin = new Thickness(0, 6, 0, 6),
				});
			}

			isFirst = false;

			_iconSizesListPanel.Children.Add(BuildIconSizeRow(master, size, isNative: size == nativeSize));
		}
	}

	private Control BuildIconSizeRow(CursorCanvasImage master, int size, bool isNative)
	{
		var isPreviewed = isNative ? _iconSizesPreviewSize == null : _iconSizesPreviewSize == size;

		var row = new Border
		{
			Background = isPreviewed ? new SolidColorBrush(Colors.DodgerBlue, 0.15) : Brushes.Transparent,
			BorderBrush = isPreviewed ? Brushes.DodgerBlue : Brushes.Transparent,
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(4),
			Padding = new Thickness(4),
			Cursor = new Avalonia.Input.Cursor(StandardCursorType.Hand),
		};

		row.PointerPressed += (_, _) => OnIconSizeRowClick(size);

		var content = new DockPanel();

		if (!isNative)
		{
			var removeButton = new Button
			{
				Content = "×",
				Padding = new Thickness(6, 0, 6, 2),
				Margin = new Thickness(0, 0, 6, 0),
				IsEnabled = _iconSizesEditMode,
			};

			removeButton.Click += (_, _) => RemoveIconSize(size);

			DockPanel.SetDock(removeButton, Dock.Left);
			content.Children.Add(removeButton);
		}

		var thumbnailHost = new Border
		{
			Width = IconSizesThumbnailBoxPx,
			Height = IconSizesThumbnailBoxPx,
			Background = CreateCheckerboardBrush(),
			BorderBrush = Brushes.Gray,
			BorderThickness = new Thickness(1),
		};

		var thumbnailImage = new Image
		{
			Source = BuildIconSizeThumbnail(GetIconSizeImage(master, size)),
			Stretch = Stretch.Uniform,
		};

		thumbnailHost.Child = thumbnailImage;
		DockPanel.SetDock(thumbnailHost, Dock.Right);
		content.Children.Add(thumbnailHost);

		if (!isNative)
		{
			var scaleIcon = new Image
			{
				Source = GetScaleModeIconImage(GetIconSizeEffectiveScaleMode(size)),
				Width = 14,
				Height = 14,
				Margin = new Thickness(0, 0, 6, 0),
				VerticalAlignment = VerticalAlignment.Center,
			};
			ToolTip.SetTip(scaleIcon, Loc.Format(LocIconSizesScaleModeFor, size));

			DockPanel.SetDock(scaleIcon, Dock.Right);
			content.Children.Add(scaleIcon);
		}

		var label = new TextBlock
		{
			Text = string.Format(CultureInfo.InvariantCulture, "{0}×{0}", size),
			VerticalAlignment = VerticalAlignment.Center,
		};

		content.Children.Add(label);

		row.Child = content;

		return row;
	}

	private static WriteableBitmap BuildIconSizeThumbnail(CursorCanvasImage image)
	{
		var bitmap = new WriteableBitmap(
			new PixelSize(image.Width, image.Height),
			new Vector(96, 96),
			PixelFormat.Bgra8888,
			AlphaFormat.Unpremul);

		using var locked = bitmap.Lock();
		Marshal.Copy(image.Bgra, 0, locked.Address, image.Bgra.Length);

		return bitmap;
	}

	private void UpdateIconSizesSummary()
	{
		var count = new SortedSet<int>(_iconSizes) { IconSizesNativeSize }.Count;
		_iconSizesSummaryText.Text = Loc.Format(LocIconSizesSummary, count);
	}

	private void OnIconSizeRowClick(int size)
	{
		_iconSizesPendingScaleOverride = null;

		RestoreIconSizesCanvas();

		_iconSizesPreviewSize = size == IconSizesNativeSize ? null : size;

		if (_iconSizesPreviewSize is { } selected)
			ApplyIconSizesPreview(selected);

		UpdateIconSizesScaleModeHeader();
		UpdateIconSizesEditModeCheck();
		UpdateIconSizesAddRemoveEnabled();
		RebuildIconSizesList();
		UpdateIconSizesApplyButton();
	}

	private CursorCanvasImage GetIconSizesMasterImage()
	{
		if (_hasIconSizesSnapshot)
		{
			var buffer = new byte[_iconSizesSnapshotCanvasWidth * _iconSizesSnapshotCanvasHeight * BytesPerPixel];
			Blit(buffer, _iconSizesSnapshotCanvasWidth, _iconSizesSnapshotCanvasHeight,
				_iconSizesSnapshotSpriteBgra, _iconSizesSnapshotSpriteWidth, _iconSizesSnapshotSpriteHeight,
				_iconSizesSnapshotOffsetX, _iconSizesSnapshotOffsetY);

			var snapshotHotspotX = Math.Clamp(_iconSizesSnapshotOffsetX + _hotspotOffsetX, 0, _iconSizesSnapshotCanvasWidth - 1);
			var snapshotHotspotY = Math.Clamp(_iconSizesSnapshotOffsetY + _hotspotOffsetY, 0, _iconSizesSnapshotCanvasHeight - 1);

			return new CursorCanvasImage(_iconSizesSnapshotCanvasWidth, _iconSizesSnapshotCanvasHeight, snapshotHotspotX, snapshotHotspotY, buffer);
		}

		var hotspotX = Math.Clamp(_offsetX + _hotspotOffsetX, 0, _canvasWidth - 1);
		var hotspotY = Math.Clamp(_offsetY + _hotspotOffsetY, 0, _canvasHeight - 1);

		return new CursorCanvasImage(_canvasWidth, _canvasHeight, hotspotX, hotspotY, Compose());
	}

	private void CaptureIconSizesSnapshot()
	{
		if (_hasIconSizesSnapshot)
			return;

		_iconSizesSnapshotSpriteBgra = (byte[])_spriteBgra.Clone();
		_iconSizesSnapshotSpriteWidth = _spriteWidth;
		_iconSizesSnapshotSpriteHeight = _spriteHeight;
		_iconSizesSnapshotCanvasWidth = _canvasWidth;
		_iconSizesSnapshotCanvasHeight = _canvasHeight;
		_iconSizesSnapshotOffsetX = _offsetX;
		_iconSizesSnapshotOffsetY = _offsetY;

		if (IsAnimated)
		{
			_timelineFrames[_activeFrameIndex] = CaptureCurrentAsFrame();
			_iconSizesSnapshotTimelineFrames = _timelineFrames.Select(f => f with { }).ToList();
		}

		_hasIconSizesSnapshot = true;
	}

	private void CommitIconSizeEditIfNeeded()
	{
		if (!_iconSizesEditMode || _iconSizesPreviewSize is not { } size || !_hasIconSizesSnapshot)
			return;

		var hotspotX = Math.Clamp(_offsetX + _hotspotOffsetX, 0, _canvasWidth - 1);
		var hotspotY = Math.Clamp(_offsetY + _hotspotOffsetY, 0, _canvasHeight - 1);

		_iconSizeCustomImages[size] = new CursorCanvasImage(_canvasWidth, _canvasHeight, hotspotX, hotspotY, Compose());
	}

	private void ApplyIconSizesPreview(int size, bool discardCustom = false)
	{
		CommitIconSizeEditIfNeeded();
		CaptureIconSizesSnapshot();

		if (discardCustom)
			_iconSizeCustomImages.Remove(size);

		var master = GetIconSizesMasterImage();
		var image = GetIconSizeImage(master, size);

		_spriteBgra = (byte[])image.Bgra.Clone();
		_spriteWidth = size;
		_spriteHeight = size;
		_offsetX = 0;
		_offsetY = 0;
		_canvasWidth = size;
		_canvasHeight = size;
		_iconSizesPreviewSize = size;

		if (IsAnimated && _iconSizesSnapshotTimelineFrames is { Count: > 0 } snapshotFrames)
		{
			var nativeSize = _iconSizesSnapshotCanvasWidth;
			var mode = GetIconSizeEffectiveScaleMode(size);

			for (var i = 0; i < _timelineFrames.Count; i++)
			{
				if (i == _activeFrameIndex)
				{
					_timelineFrames[i] = new TimelineFrame(
						(byte[])image.Bgra.Clone(), size, size, 0, 0, snapshotFrames[i].DurationMs);
					continue;
				}

				var snap = snapshotFrames[i];
				var frameImage = new CursorCanvasImage(nativeSize, nativeSize,
					Math.Clamp(snap.OffsetX + _hotspotOffsetX, 0, nativeSize - 1),
					Math.Clamp(snap.OffsetY + _hotspotOffsetY, 0, nativeSize - 1),
					ComposeFrame(snap, nativeSize));

				var scaled = size == nativeSize
					? frameImage
					: CursorScalerService.ScaleImage(frameImage, size, size, mode);

				_timelineFrames[i] = new TimelineFrame(
					(byte[])scaled.Bgra.Clone(), size, size, 0, 0, snap.DurationMs);
			}
		}

		ClearHistory();
		RenderAll();
		UpdateUndoRedoButtons();
	}

	private void RestoreIconSizesCanvas()
	{
		CommitIconSizeEditIfNeeded();

		if (!_hasIconSizesSnapshot)
			return;

		_spriteBgra = _iconSizesSnapshotSpriteBgra;
		_spriteWidth = _iconSizesSnapshotSpriteWidth;
		_spriteHeight = _iconSizesSnapshotSpriteHeight;
		_canvasWidth = _iconSizesSnapshotCanvasWidth;
		_canvasHeight = _iconSizesSnapshotCanvasHeight;
		_offsetX = _iconSizesSnapshotOffsetX;
		_offsetY = _iconSizesSnapshotOffsetY;

		if (_iconSizesSnapshotTimelineFrames is { Count: > 0 } snapshotFrames)
		{
			for (var i = 0; i < _timelineFrames.Count && i < snapshotFrames.Count; i++)
				_timelineFrames[i] = snapshotFrames[i] with { };
		}

		_iconSizesSnapshotTimelineFrames = null;
		_hasIconSizesSnapshot = false;
		_iconSizesPendingScaleOverride = null;

		ClearHistory();
		RenderAll();
		UpdateUndoRedoButtons();
	}

	private void RestoreIconSizesPreview()
	{
		RestoreIconSizesCanvas();
		_iconSizesPreviewSize = null;
	}

	private void CaptureIconSizesResult()
	{
		RestoreIconSizesPreview();

		if (!IsIconSizesAvailable)
		{
			ResultIconSizes = null;
			ResultIconSizeScaleModeOverrides = null;
			ResultIconSizeCustomImages = null;
			return;
		}

		var sizes = new SortedSet<int>(_iconSizes) { _canvasWidth };
		ResultIconSizes = sizes.Count > 1 ? sizes.ToList() : null;
		ResultIconSizesScaleMode = _iconSizesScaleMode;
		ResultIconSizeScaleModeOverrides = _iconSizeScaleOverrides.Count > 0
			? new Dictionary<int, ScaleMode>(_iconSizeScaleOverrides)
			: null;
		ResultIconSizeCustomImages = _iconSizeCustomImages.Count > 0
			? new Dictionary<int, CursorCanvasImage>(_iconSizeCustomImages)
			: null;
	}

	private static byte[] ComposeFrame(TimelineFrame frame, int canvasSize)
	{
		var buffer = new byte[canvasSize * canvasSize * BytesPerPixel];
		Blit(buffer, canvasSize, canvasSize, frame.SpriteBgra, frame.SpriteWidth, frame.SpriteHeight, frame.OffsetX, frame.OffsetY);

		return buffer;
	}

	private void ShowToast(string message)
	{
		if (_rootGrid is { } panel)
			ToastService.Show(panel, message);
	}
}
