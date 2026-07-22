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

	private const double ThumbEdgeThicknessPx = 8;
	private const double ThumbCornerSizePx = 12;
	private const double ThumbCornerBorderPx = 2;
	private const double BorderStrokePx = 2;
	private const double SpriteBoundsStrokePx = 2;
	private const double ShadowStrokePx = 1;
	private const double ResizeLabelFontSizePx = 11;
	private const string CoordsFormat = "{0} × {1} px   X: {2}   Y: {3}";
	private const string StyleAccentButton = "Style.AccentButton";
	private const string StyleButton = "Style.Button";

	private const string LocInfoTitle = "S.Info.Title";
	private const string LocInfoPaint = "S.Info.Paint";

	private int _spriteWidth;
	private int _spriteHeight;
	private byte[] _spriteBgra;
	private int _hotspotOffsetX;
	private int _hotspotOffsetY;

	private int _canvasWidth;
	private int _canvasHeight;
	private int _offsetX;
	private int _offsetY;
	private double _zoom;
	private string _currentTool = "";
	private bool _ready;

	private bool _isPanning;
	private Point _panStartPosition;
	private double _panStartHorizontalOffset;
	private double _panStartVerticalOffset;
	private double? _savedPanX;
	private double? _savedPanY;

	private bool _isDraggingSprite;
	private Point _spriteDragStart;
	private int _dragStartOffsetX;
	private int _dragStartOffsetY;

	private int _resizeOriginalWidth;
	private int _resizeOriginalHeight;
	private int _resizeOriginalOffsetX;
	private int _resizeOriginalOffsetY;
	private double _resizeOriginalPanX;
	private double _resizeOriginalPanY;

	private bool _hasCanvasResizeSnapshot;
	private int _canvasResizeSnapshotWidth;
	private int _canvasResizeSnapshotHeight;
	private int _canvasResizeSnapshotOffsetX;
	private int _canvasResizeSnapshotOffsetY;
	private double _canvasResizeSnapshotPanX;
	private double _canvasResizeSnapshotPanY;

	private double _resizeAccumulatorX;
	private double _resizeAccumulatorY;

	private readonly string? _presetName;
	private readonly string? _roleName;

	public CursorCanvasImage? Result { get; private set; }

	public PaintEditorWindow(CursorCanvasImage source, string? presetName = null, string? roleName = null)
	{
		InitializeComponent();

		_presetName = presetName;
		_roleName = roleName;

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

		var (savedPanX, savedPanY) = AppState.GetPaintEditorPan();
		_savedPanX = savedPanX;
		_savedPanY = savedPanY;

		var showBounds = AppState.GetShowSpriteBounds();
		ShowSpriteBoundsCheck.IsChecked = showBounds;
		SpriteBoundsRect.Visibility = showBounds ? Visibility.Visible : Visibility.Collapsed;

		var (hue, saturation, value, alpha) = AppState.GetPaintEditorColor();
		ColorWheel.SetColor(hue, saturation, value, alpha);
		ColorWheel.SetColorMode(AppState.GetPaintEditorColorMode());

		UpdateToolButtons();

		_ready = true;

		RenderAll();
		InitBgRef();

		UpdateUndoRedoButtons();
	}

	protected override void OnClosed(EventArgs e)
	{
		AppState.SetPaintEditorSize(Width, Height);
		AppState.SetPaintEditorZoom(_zoom);
		AppState.SetPaintEditorTool(_currentTool);

		var (hue, saturation, value, alpha) = ColorWheel.GetHsv();
		AppState.SetPaintEditorColor(hue, saturation, value, alpha);
		AppState.SetPaintEditorColorMode(ColorWheel.GetColorMode());
		AppState.SetPaintEditorPan(CanvasPanTransform.X, CanvasPanTransform.Y);
		SaveBgRefSettings();

		ClearHistory();

		base.OnClosed(e);
	}
}
