using System.Windows;
using System.Windows.Media;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class PaintEditorWindow : Window
{
	private const int MinCanvasDimension = 1;
	private const int MaxCanvasDimension = 256;
	private const int BytesPerPixel = 4;
	private const double UiZoomStep = 0.1;
	private const double CanvasZoomStep = 1.2;

	// On-screen sizes (device-independent pixels) for canvas overlay chrome; divided by
	// zoom so thumbs/border/labels stay a constant, usable size at any zoom level.
	private const double ThumbEdgeLengthPx = 16;
	private const double ThumbEdgeThicknessPx = 6;
	private const double ThumbCornerSizePx = 8;
	private const double BorderStrokePx = 2;
	private const double SpriteBoundsStrokePx = 2;
	private const double ShadowStrokePx = 1;
	private const double ResizeLabelFontSizePx = 11;
	private const string CoordsFormat = "{0} × {1} px   X: {2}   Y: {3}";
	private const string StyleAccentButton = "Style.AccentButton";
	private const string StyleButton = "Style.Button";

	private const string LocInfoTitle = "S.Info.Title";
	private const string LocInfoPosition = "S.Info.Position";

	private readonly int _spriteWidth;
	private readonly int _spriteHeight;
	private readonly byte[] _spriteBgra;
	private readonly int _hotspotOffsetX;
	private readonly int _hotspotOffsetY;

	private int _canvasWidth;
	private int _canvasHeight;
	private int _offsetX;
	private int _offsetY;
	private double _zoom;
	private string _currentTool = "";
	private bool _ready;

	private bool _isPanning;
	private Point _panStart;
	private double _panStartHOffset;
	private double _panStartVOffset;

	private bool _isDraggingSprite;
	private Point _spriteDragStart;
	private int _dragStartOffsetX;
	private int _dragStartOffsetY;

	private int _resizeOriginalWidth;
	private int _resizeOriginalHeight;
	private int _resizeOriginalOffsetX;
	private int _resizeOriginalOffsetY;

	// Snapshot taken when the Canvas tool is entered; restored if the tool is switched away
	// from without hitting Apply (see SetTool in PaintEditorWindow.Tools.cs).
	private bool _hasCanvasResizeSnapshot;
	private int _canvasResizeSnapshotWidth;
	private int _canvasResizeSnapshotHeight;
	private int _canvasResizeSnapshotOffsetX;
	private int _canvasResizeSnapshotOffsetY;

	// Thumb.DragDelta reports the change since the *previous* event, not since the drag
	// started, so we accumulate the raw movement ourselves and always resize relative to
	// the drag-start dimensions (matches how the *Original* fields above are used below).
	private double _resizeAccumX;
	private double _resizeAccumY;

	public CursorCanvasImage? Result { get; private set; }

	public PaintEditorWindow(CursorCanvasImage source)
	{
		InitializeComponent();

		Width = AppState.GetPaintEditorWidth();
		Height = AppState.GetPaintEditorHeight();

		var uiScale = AppState.GetEditorUiScale();
		UiScaleTransform.ScaleX = uiScale;
		UiScaleTransform.ScaleY = uiScale;
		UiZoomText.Text = $"{(int)Math.Round(uiScale * 100)}%";

		var bounds = FindOpaqueBounds(source);

		_spriteWidth = bounds.Width;
		_spriteHeight = bounds.Height;
		_spriteBgra = ExtractRegion(source.Bgra, source.Width, bounds);
		_hotspotOffsetX = Math.Clamp(source.HotspotX - bounds.X, 0, _spriteWidth - 1);
		_hotspotOffsetY = Math.Clamp(source.HotspotY - bounds.Y, 0, _spriteHeight - 1);

		_canvasWidth = source.Width;
		_canvasHeight = source.Height;
		_offsetX = bounds.X;
		_offsetY = bounds.Y;

		_zoom = AppState.GetPaintEditorZoom();
		_currentTool = AppState.GetPaintEditorTool();

		var showBounds = AppState.GetShowSpriteBounds();
		ShowSpriteBoundsCheck.IsChecked = showBounds;
		SpriteBoundsRect.Visibility = showBounds ? Visibility.Visible : Visibility.Collapsed;

		UpdateToolButtons();

		_ready = true;

		RenderAll();
	}

	protected override void OnClosed(EventArgs e)
	{
		AppState.SetPaintEditorSize(Width, Height);
		AppState.SetPaintEditorZoom(_zoom);
		AppState.SetPaintEditorTool(_currentTool);
		base.OnClosed(e);
	}
}
