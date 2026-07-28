using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CursorPalette.Linux.Controls;
using CursorPalette.Linux.Services;
using CursorPalette.Services;
using System.Runtime.InteropServices;

namespace CursorPalette.Linux.Views;

public partial class PaintEditorWindow : Window
{
	private const int MinCanvasDimension = 1;
	private const int MaxCanvasDimension = 256;
	private const int BytesPerPixel = 4;
	private const double Dpi = 96.0;
	private const double CanvasZoomStep = 1.2;
	private const double UiZoomStep = 0.1;
	private const double BorderStrokePx = 2;
	private const double SpriteBoundsStrokePx = 2;
	private const double ShadowStrokePx = 1;
	private const double ResizeLabelFontSizePx = 11;
	private const double ThumbEdgeThicknessPx = 8;
	private const double ThumbCornerSizePx = 12;
	private const double ThumbCornerBorderPx = 2;
	private const double HotspotMarkerScreenPx = 12;
	private const double HotspotGlowScreenPx = 20;
	private const double HotspotMarkerStrokeScreenPx = 2;
	private const double DefaultWindowWidth = 820;
	private const double DefaultWindowHeight = 680;
	private const double ToolPanelWidth = 176;
	private const double StatusBarHeight = 24;
	private const double ToolbarSpacing = 8;
	private const double SeparatorMargin = 8;
	private const double TitleFontSize = 16;
	private const double StatusFontSize = 11;

	private const string CoordsFormat = "{0} × {1} px   X: {2}   Y: {3}";
	private const string HotspotCoordsFormat = "X: {0}   Y: {1}";
	private const string ZoomFormat = "{0:0.#}x";

	private const string TitleText = "Paint Editor";
	private const string ImportButtonText = "Import";
	private const string ExportPngButtonText = "PNG";
	private const string ExportGifButtonText = "GIF";
	private const string CanvasSizeButtonText = "Canvas Size";
	private const string SpriteBoundsButtonText = "Bounds";
	private const string SaveButtonText = "Save";
	private const string CancelButtonText = "Cancel";
	private const string ZoomInIcon = "+";
	private const string ZoomOutIcon = "−";
	private const string DelayLabelText = "Delay ms:";
	private const string DefaultFrameDurationText = "100";

	private const double ToolButtonMinWidth = 36;
	private const double ToolButtonPadding = 8;
	private const double ActionButtonPaddingHorizontal = 8;
	private const double ActionButtonPaddingVertical = 4;
	private const double UndoRedoButtonPadding = 8;
	private const double ImportButtonPaddingHorizontal = 12;
	private const double ImportButtonPaddingVertical = 4;
	private const double ToolPanelSpacing = 8;
	private const double ToolPanelMargin = 8;
	private const double TimelineFramesSpacing = 4;
	private const double TimelineFramesMargin = 8;
	private const double FrameButtonPaddingHorizontal = 10;
	private const double FrameButtonPaddingVertical = 4;
	private const double FrameDurationBoxWidth = 60;
	private const double ToolbarPaddingHorizontal = 12;
	private const double ToolbarPaddingVertical = 8;
	private const double StatusBarPaddingHorizontal = 12;
	private const double StatusBarPaddingVertical = 4;
	private const double TimelineBorderThicknessTop = 1;
	private const double TimelinePanelSpacing = 8;
	private const double TimelinePanelMargin = 8;
	private const double SnapGridSize = 120;
	private const double SnapGridButtonPadding = 4;
	private const double ToolPanelLabelFontSize = 11;
	private const int CheckerboardCellSize = 8;
	private const byte CheckerboardLightValue = 200;
	private const byte CheckerboardDarkValue = 150;
	private const int PaintCursorAlpha = 120;
	private const int MaxLzwCode = 4096;
	private const int MaxLzwCodeSize = 12;

	private static readonly Color ViewportBackgroundColor = Color.FromArgb(255, 30, 30, 35);
	private static readonly Color HotspotGlowFillColor = Color.FromArgb(80, 255, 0, 0);
	private static readonly Color PaintCursorEraserFillColor = Color.FromArgb(120, 255, 255, 255);

	private int _spriteWidth;
	private int _spriteHeight;
	private byte[] _spriteBgra = Array.Empty<byte>();
	private int _hotspotOffsetX;
	private int _hotspotOffsetY;

	private int _canvasWidth;
	private int _canvasHeight;
	private int _offsetX;
	private int _offsetY;
	private double _zoom = AppState.PaintEditorZoomDefault;
	private string _currentTool = AppState.PaintEditorToolDefault;
	private bool _ready;

	private bool _isPanning;
	private Point _panStartPosition;
	private double _panStartX;
	private double _panStartY;
	private double? _savedPanX;
	private double? _savedPanY;

	private bool _isDraggingSprite;
	private Point _spriteDragStart;
	private int _dragStartOffsetX;
	private int _dragStartOffsetY;

	private bool _isDraggingHotspot;

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

	private bool _hideMainImage;
	private bool _showSpriteBounds;

	private readonly string? _presetName;
	private readonly string? _roleName;

	private readonly TranslateTransform _panTransform = new();
	private readonly ScaleTransform _zoomTransform = new();
	private WriteableBitmap? _previewBitmap;

	private readonly Image _previewImage;
	private readonly Rectangle _canvasBgRect;
	private readonly Rectangle _spriteBoundsRect;
	private readonly Ellipse _hotspotMarker;
	private readonly Ellipse _hotspotMarkerGlow;
	private readonly Rectangle _paintCursorRect;
	private readonly Rectangle _canvasBorderRect;
	private readonly Panel _viewportContent;
	private readonly Border _viewportHost;
	private readonly ColorWheelControl _colorWheel;

	private readonly TextBlock _coordsText;
	private readonly TextBlock _hotspotCoordsText;
	private readonly TextBlock _zoomText;
	private readonly TextBlock _canvasSizeLabel;

	private readonly StackPanel _toolPanel;
	private readonly StackPanel _moveToolPanel;
	private readonly StackPanel _handToolPanel;
	private readonly StackPanel _canvasToolPanel;
	private readonly StackPanel _brushToolPanel;
	private readonly StackPanel _eraserToolPanel;
	private readonly StackPanel _fillToolPanel;
	private readonly StackPanel _hotspotToolPanel;
	private readonly StackPanel _bgRefToolPanel;
	private readonly StackPanel _iconSizesToolPanel;

	private readonly ToggleButton _toolMoveBtn;
	private readonly ToggleButton _toolHandBtn;
	private readonly ToggleButton _toolBrushBtn;
	private readonly ToggleButton _toolEraserBtn;
	private readonly ToggleButton _toolFillBtn;
	private readonly ToggleButton _toolCanvasBtn;
	private readonly ToggleButton _toolHotspotBtn;
	private readonly ToggleButton _toolBgRefBtn;
	private readonly ToggleButton _toolIconSizesBtn;

	private TextBlock _iconSizesHintText = null!;
	private TextBlock _iconSizesUnavailableHint = null!;
	private StackPanel _iconSizesContentPanel = null!;
	private Border _iconSizesScaleModeIconBorder = null!;
	private Image _iconSizesScaleModeIcon = null!;
	private TextBlock _iconSizesScaleModeLabel = null!;
	private Button _iconSizesScaleModeResetButton = null!;
	private ToggleButton _iconSizesEditModeCheck = null!;
	private Button _iconSizesAddSizeButton = null!;
	private TextBox _iconSizesAddSizeBox = null!;
	private StackPanel _iconSizesListPanel = null!;
	private TextBlock _iconSizesSummaryText = null!;
	private Button _iconSizesApplyButton = null!;

	private Grid _rootGrid = null!;

	private readonly Button _undoButton;
	private readonly Button _redoButton;
	private readonly Button _importButton;
	private readonly Button _exportPngButton;
	private readonly Button _exportGifButton;
	private readonly Button _canvasSizeButton;
	private readonly ToggleButton _showSpriteBoundsCheck;

	private readonly Panel _timelinePanel;
	private readonly StackPanel _timelineFramesPanel;
	private readonly Button _addFrameButton;
	private readonly Button _removeFrameButton;
	private readonly Button _playStopButton;
	private readonly TextBox _frameDurationBox;
	private readonly TextBlock _frameStatusLabel;

	private readonly Image _bgRefImage;
	private readonly Border _resizeOverlay;
	private readonly Panel _resizeThumbsPanel;

	public CursorCanvasImage? Result { get; private set; }
	public IReadOnlyList<CursorCanvasImage>? ResultFrames { get; private set; }
	public IReadOnlyList<int>? ResultFrameDelaysMs { get; private set; }

	public PaintEditorWindow(CursorCanvasImage? source = null, string? presetName = null, string? roleName = null, IReadOnlyList<CursorCanvasImage>? existingIconImages = null)
	{
		_presetName = presetName;
		_roleName = roleName;

		Title = TitleText;
		Width = AppState.GetPaintEditorWidth();
		Height = AppState.GetPaintEditorHeight();
		MinWidth = 560;
		MinHeight = 460;
		Background = Brushes.Transparent;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;

		InitializeWindowState();

		if (source != null)
			SetSourceFrame(source);
		else
			SetEmptySource();

		_previewImage = new Image { Stretch = Stretch.None };
		_canvasBgRect = new Rectangle { Fill = CreateCheckerboardBrush() };
		_spriteBoundsRect = new Rectangle
		{
			Stroke = Brushes.Cyan,
			StrokeThickness = SpriteBoundsStrokePx,
			IsVisible = false,
			IsHitTestVisible = false,
		};
		_hotspotMarker = new Ellipse
		{
			Width = HotspotMarkerScreenPx,
			Height = HotspotMarkerScreenPx,
			Fill = Brushes.Red,
			Stroke = Brushes.White,
			StrokeThickness = HotspotMarkerStrokeScreenPx,
			IsVisible = false,
			IsHitTestVisible = false,
		};
		_hotspotMarkerGlow = new Ellipse
		{
			Width = HotspotGlowScreenPx,
			Height = HotspotGlowScreenPx,
			Fill = new SolidColorBrush(HotspotGlowFillColor),
			IsVisible = false,
			IsHitTestVisible = false,
		};
		_paintCursorRect = new Rectangle
		{
			IsVisible = false,
			IsHitTestVisible = false,
		};
		_canvasBorderRect = new Rectangle
		{
			Stroke = Brushes.DimGray,
			StrokeThickness = BorderStrokePx,
			IsHitTestVisible = false,
		};
		_bgRefImage = new Image { IsVisible = false, IsHitTestVisible = false };

		_viewportContent = new Panel
		{
			Children = { _canvasBgRect, _bgRefImage, _previewImage, _spriteBoundsRect, _hotspotMarkerGlow, _hotspotMarker, _paintCursorRect, _canvasBorderRect },
		};

		var renderTransform = new TransformGroup();
		renderTransform.Children.Add(_zoomTransform);
		renderTransform.Children.Add(_panTransform);
		_viewportContent.RenderTransform = renderTransform;

		_resizeThumbsPanel = new Panel { IsVisible = false };
		_resizeOverlay = new Border
		{
			Child = _resizeThumbsPanel,
			IsVisible = false,
		};

		_viewportHost = new Border
		{
			ClipToBounds = true,
			Background = new SolidColorBrush(ViewportBackgroundColor),
			Child = new Panel
			{
				Children = { _viewportContent, _resizeOverlay },
			},
		};

		_colorWheel = new ColorWheelControl();

		_coordsText = new TextBlock { FontSize = StatusFontSize, Margin = new Thickness(StatusBarPaddingHorizontal, 0) };
		_hotspotCoordsText = new TextBlock { FontSize = StatusFontSize, Margin = new Thickness(StatusBarPaddingHorizontal, 0) };
		_zoomText = new TextBlock { FontSize = StatusFontSize, Margin = new Thickness(SnapGridButtonPadding, 0) };
		_canvasSizeLabel = new TextBlock { FontSize = StatusFontSize, Margin = new Thickness(StatusBarPaddingHorizontal, 0) };

		_toolMoveBtn = CreateToolButton("✥", AppState.PaintEditorToolMove);
		_toolHandBtn = CreateToolButton("✋", AppState.PaintEditorToolHand);
		_toolBrushBtn = CreateToolButton(IconHelper.CreateIcon("PencilIcon48.png", 18, Brushes.White), AppState.PaintEditorToolBrush);
		_toolEraserBtn = CreateToolButton(IconHelper.CreateIcon("EraseIcon32.png", 18, Brushes.White), AppState.PaintEditorToolEraser);
		_toolFillBtn = CreateToolButton(IconHelper.CreateIcon("FillIcon32.png", 18, Brushes.White), AppState.PaintEditorToolFill);
		_toolCanvasBtn = CreateToolButton("⛶", AppState.PaintEditorToolCanvas);
		_toolHotspotBtn = CreateToolButton("🎯", AppState.PaintEditorToolHotspot);
		_toolBgRefBtn = CreateToolButton(IconHelper.CreateIcon("ImageRefIcon32.png", 20, Brushes.White), AppState.PaintEditorToolBgRef);
		_toolIconSizesBtn = CreateToolButton("⊞", AppState.PaintEditorToolIconSizes);

		_undoButton = new Button { Content = "↶", Padding = new Thickness(UndoRedoButtonPadding) };
		_redoButton = new Button { Content = "↷", Padding = new Thickness(UndoRedoButtonPadding) };
		_importButton = new Button
		{
			Content = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Children =
				{
					IconHelper.CreateIcon("DownloadIcon32.png", 18, Brushes.White),
					new TextBlock { Text = ImportButtonText, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) },
				},
			},
			Padding = new Thickness(ImportButtonPaddingHorizontal, ImportButtonPaddingVertical, ImportButtonPaddingHorizontal, ImportButtonPaddingVertical),
		};
		_exportPngButton = new Button
		{
			Content = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Children =
				{
					IconHelper.CreateIcon("DownloadIcon32.png", 18, Brushes.White),
					new TextBlock { Text = ExportPngButtonText, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) },
				},
			},
			Padding = new Thickness(ActionButtonPaddingHorizontal, ActionButtonPaddingVertical, ActionButtonPaddingHorizontal, ActionButtonPaddingVertical),
		};
		_exportGifButton = new Button
		{
			Content = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Children =
				{
					IconHelper.CreateIcon("DownloadIcon32.png", 18, Brushes.White),
					new TextBlock { Text = ExportGifButtonText, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) },
				},
			},
			Padding = new Thickness(ActionButtonPaddingHorizontal, ActionButtonPaddingVertical, ActionButtonPaddingHorizontal, ActionButtonPaddingVertical),
			IsVisible = false,
		};
		_canvasSizeButton = new Button { Content = CanvasSizeButtonText, Padding = new Thickness(ActionButtonPaddingHorizontal, ActionButtonPaddingVertical, ActionButtonPaddingHorizontal, ActionButtonPaddingVertical) };
		_showSpriteBoundsCheck = new ToggleButton { Content = SpriteBoundsButtonText, Padding = new Thickness(ActionButtonPaddingHorizontal, ActionButtonPaddingVertical, ActionButtonPaddingHorizontal, ActionButtonPaddingVertical) };

		_moveToolPanel = new StackPanel { IsVisible = false, Spacing = ToolPanelSpacing, Margin = new Thickness(ToolPanelMargin) };
		_handToolPanel = new StackPanel { IsVisible = false, Spacing = ToolPanelSpacing, Margin = new Thickness(ToolPanelMargin) };
		_canvasToolPanel = new StackPanel { IsVisible = false, Spacing = ToolPanelSpacing, Margin = new Thickness(ToolPanelMargin) };
		_brushToolPanel = new StackPanel { IsVisible = false, Spacing = ToolPanelSpacing, Margin = new Thickness(ToolPanelMargin) };
		_eraserToolPanel = new StackPanel { IsVisible = false, Spacing = ToolPanelSpacing, Margin = new Thickness(ToolPanelMargin) };
		_fillToolPanel = new StackPanel { IsVisible = false, Spacing = ToolPanelSpacing, Margin = new Thickness(ToolPanelMargin) };
		_hotspotToolPanel = new StackPanel { IsVisible = false, Spacing = ToolPanelSpacing, Margin = new Thickness(ToolPanelMargin) };
		_bgRefToolPanel = new StackPanel { IsVisible = false, Spacing = ToolPanelSpacing, Margin = new Thickness(ToolPanelMargin) };
		_iconSizesToolPanel = new StackPanel { IsVisible = false, Spacing = ToolPanelSpacing, Margin = new Thickness(ToolPanelMargin) };

		BuildMoveToolPanel();
		BuildHandToolPanel();
		BuildCanvasToolPanel();
		BuildBrushToolPanel();
		BuildEraserToolPanel();
		BuildFillToolPanel();
		BuildHotspotToolPanel();
		BuildBgRefToolPanel();
		BuildIconSizesToolPanel();

		_toolPanel = new StackPanel
		{
			Width = ToolPanelWidth,
			VerticalAlignment = VerticalAlignment.Stretch,
			Children = { _moveToolPanel, _handToolPanel, _canvasToolPanel, _brushToolPanel, _eraserToolPanel, _fillToolPanel, _hotspotToolPanel, _bgRefToolPanel, _iconSizesToolPanel },
		};

		_timelinePanel = new Panel { IsVisible = false };
		_timelineFramesPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = TimelineFramesSpacing, Margin = new Thickness(TimelineFramesMargin) };
		_addFrameButton = new Button { Content = "+", Padding = new Thickness(FrameButtonPaddingHorizontal, FrameButtonPaddingVertical, FrameButtonPaddingHorizontal, FrameButtonPaddingVertical) };
		_removeFrameButton = new Button { Content = "−", Padding = new Thickness(FrameButtonPaddingHorizontal, FrameButtonPaddingVertical, FrameButtonPaddingHorizontal, FrameButtonPaddingVertical) };
		_playStopButton = new Button { Content = "▶", Padding = new Thickness(FrameButtonPaddingHorizontal, FrameButtonPaddingVertical, FrameButtonPaddingHorizontal, FrameButtonPaddingVertical) };
		_frameDurationBox = new TextBox { Width = FrameDurationBoxWidth, Text = DefaultFrameDurationText };
		_frameStatusLabel = new TextBlock { FontSize = StatusFontSize, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(ToolPanelSpacing, 0) };

		BuildTimelinePanel();

		BuildUi();

		AttachEvents();

		if (existingIconImages is { Count: > 0 })
			SeedIconSizes(existingIconImages);

		_ready = true;
		RenderAll();
		InitTimeline();
		InitBgRef();
		UpdateUndoRedoButtons();

		this.Closed += OnWindowClosed;
	}

	public PaintEditorWindow(IReadOnlyList<CursorCanvasImage> frames, IReadOnlyList<int> frameDelaysMs, string? presetName = null, string? roleName = null, IReadOnlyList<CursorCanvasImage>? existingIconImages = null)
		: this(frames[0], presetName, roleName, existingIconImages)
	{
		InitTimelineFromFrames(frames, frameDelaysMs);
	}

	private void InitializeWindowState()
	{
		_zoom = AppState.GetPaintEditorZoom();
		_currentTool = AppState.GetPaintEditorTool();
		var (panX, panY) = AppState.GetPaintEditorPan();
		_savedPanX = panX;
		_savedPanY = panY;
		_showSpriteBounds = AppState.GetShowSpriteBounds();
		_showSpriteBoundsCheck.IsChecked = _showSpriteBounds;
		_spriteBoundsRect.IsVisible = _showSpriteBounds;

		var (hue, saturation, value, alpha) = AppState.GetPaintEditorColor();
		_colorWheel.SetColor(hue, saturation, value, alpha);
		_colorWheel.SetColorMode(AppState.GetPaintEditorColorMode());
	}

	private void SetSourceFrame(CursorCanvasImage source)
	{
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
	}

	private void SetEmptySource()
	{
		_spriteWidth = MinCanvasDimension;
		_spriteHeight = MinCanvasDimension;
		_spriteBgra = new byte[BytesPerPixel];
		_hotspotOffsetX = 0;
		_hotspotOffsetY = 0;
		_canvasWidth = MinCanvasDimension;
		_canvasHeight = MinCanvasDimension;
		_offsetX = 0;
		_offsetY = 0;
	}

	private void BuildUi()
	{
		var toolbar = new Border
		{
			Padding = new Thickness(ToolbarPaddingHorizontal, ToolbarPaddingVertical),
			Child = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Spacing = ToolbarSpacing,
				VerticalAlignment = VerticalAlignment.Center,
				Children =
				{
					new TextBlock { Text = TitleText, FontSize = TitleFontSize, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center },
					new Separator { Margin = new(SeparatorMargin, 0) },
					_toolMoveBtn, _toolHandBtn, _toolBrushBtn, _toolEraserBtn, _toolFillBtn, _toolCanvasBtn, _toolHotspotBtn, _toolBgRefBtn,
					new Separator { Margin = new(SeparatorMargin, 0) },
					_undoButton, _redoButton,
					new Separator { Margin = new(SeparatorMargin, 0) },
					_importButton, _exportPngButton, _exportGifButton,
					new Separator { Margin = new(SeparatorMargin, 0) },
					_canvasSizeButton, _showSpriteBoundsCheck,
					new Separator { Margin = new(SeparatorMargin, 0) },
					CreateToolbarButton(ZoomOutIcon, OnCanvasZoomOut),
					_zoomText,
					CreateToolbarButton(ZoomInIcon, OnCanvasZoomIn),
				},
			},
		};
		ThemeManager.BindResource(toolbar, Border.BackgroundProperty, "SystemControlDefaultChromeMediumBrush");
		ThemeManager.BindResource(toolbar, Border.BorderBrushProperty, "SystemControlBorderBrush");
		toolbar.BorderThickness = new Thickness(0, 0, 0, 1);

		var mainArea = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions($"{ToolPanelWidth},*"),
			Children =
			{
				_toolPanel,
				_viewportHost,
			},
		};
		Grid.SetColumn(_toolPanel, 0);
		Grid.SetColumn(_viewportHost, 1);

		var statusBar = new Border
		{
			Padding = new Thickness(StatusBarPaddingHorizontal, StatusBarPaddingVertical),
			Child = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Children = { _coordsText, _hotspotCoordsText, _canvasSizeLabel },
			},
		};

		var cancelButton = new Button { Content = CancelButtonText, MinWidth = 90 };
		cancelButton.Click += OnCancelClick;

		var saveButton = new Button { Content = SaveButtonText, MinWidth = 90, Classes = { "accent" } };
		saveButton.Click += OnSaveClick;

		var footerBar = new Border
		{
			Padding = new Thickness(ToolbarPaddingHorizontal, StatusBarPaddingVertical),
			BorderThickness = new Thickness(0, 1, 0, 0),
			Child = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				HorizontalAlignment = HorizontalAlignment.Right,
				Spacing = 8,
				Children = { cancelButton, saveButton },
			},
		};
		ThemeManager.BindResource(footerBar, Border.BackgroundProperty, "SystemControlDefaultChromeMediumBrush");
		ThemeManager.BindResource(footerBar, Border.BorderBrushProperty, "SystemControlBorderBrush");

		_rootGrid = new Grid
		{
			RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto,Auto"),
			Children = { toolbar, mainArea, _timelinePanel, statusBar, footerBar },
		};
		Content = _rootGrid;

		Grid.SetRow(toolbar, 0);
		Grid.SetRow(mainArea, 1);
		Grid.SetRow(_timelinePanel, 2);
		Grid.SetRow(statusBar, 3);
		Grid.SetRow(footerBar, 4);
	}

	private void BuildTimelinePanel()
	{
		_timelinePanel.Children.Add(new Border
		{
			BorderBrush = Brushes.DimGray,
			BorderThickness = new Thickness(0, TimelineBorderThicknessTop, 0, 0),
			Child = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Spacing = TimelinePanelSpacing,
				Margin = new Thickness(TimelinePanelMargin),
				Children =
				{
					_addFrameButton,
					_removeFrameButton,
					_playStopButton,
					new TextBlock { Text = DelayLabelText, VerticalAlignment = VerticalAlignment.Center, FontSize = StatusFontSize },
					_frameDurationBox,
					_frameStatusLabel,
					_timelineFramesPanel,
				},
			},
		});
	}

	private void AttachEvents()
	{
		_toolMoveBtn.Click += (_, _) => SetTool(AppState.PaintEditorToolMove);
		_toolHandBtn.Click += (_, _) => SetTool(AppState.PaintEditorToolHand);
		_toolBrushBtn.Click += (_, _) => SetTool(AppState.PaintEditorToolBrush);
		_toolEraserBtn.Click += (_, _) => SetTool(AppState.PaintEditorToolEraser);
		_toolFillBtn.Click += (_, _) => SetTool(AppState.PaintEditorToolFill);
		_toolCanvasBtn.Click += (_, _) => SetTool(AppState.PaintEditorToolCanvas);
		_toolHotspotBtn.Click += (_, _) => SetTool(AppState.PaintEditorToolHotspot);
		_toolBgRefBtn.Click += (_, _) => SetTool(AppState.PaintEditorToolBgRef);
		_toolIconSizesBtn.Click += (_, _) => SetTool(AppState.PaintEditorToolIconSizes);

		_undoButton.Click += (_, _) => Undo();
		_redoButton.Click += (_, _) => Redo();
		_importButton.Click += OnImportClick;
		_exportPngButton.Click += OnExportPngClick;
		_exportGifButton.Click += OnExportGifClick;
		_canvasSizeButton.Click += OnCanvasSizeClick;
		_showSpriteBoundsCheck.IsCheckedChanged += OnShowSpriteBoundsChanged;

		_colorWheel.ColorChanged += (_, _) => { if (_ready) RenderAll(); };
		_colorWheel.EyedropperRequested += OnEyedropperClick;

		_viewportHost.PointerPressed += OnViewportPointerPressed;
		_viewportHost.PointerMoved += OnViewportPointerMoved;
		_viewportHost.PointerReleased += OnViewportPointerReleased;
		_viewportHost.PointerWheelChanged += OnViewportPointerWheelChanged;

		_addFrameButton.Click += OnAddFrameClick;
		_removeFrameButton.Click += OnRemoveFrameClick;
		_playStopButton.Click += OnPlayStopClick;
		_frameDurationBox.TextChanged += OnFrameDurationChanged;

		this.KeyDown += OnWindowKeyDown;
		this.Loaded += OnWindowLoaded;
	}

	private void OnWindowLoaded(object? sender, RoutedEventArgs e)
	{
		if (_savedPanX.HasValue && _savedPanY.HasValue)
		{
			_panTransform.X = _savedPanX.Value;
			_panTransform.Y = _savedPanY.Value;
		}
		else
		{
			CenterViewport();
		}
	}

	private void CenterViewport()
	{
		if (_viewportHost.Bounds.Width <= 0 || _viewportHost.Bounds.Height <= 0)
			return;
		_panTransform.X = Math.Round((_viewportHost.Bounds.Width - _canvasWidth * _zoom) / 2.0);
		_panTransform.Y = Math.Round((_viewportHost.Bounds.Height - _canvasHeight * _zoom) / 2.0);
	}

	private void OnWindowClosed(object? sender, EventArgs e)
	{
		StopTimelinePlayback();
		AppState.SetPaintEditorSize(Width, Height);
		AppState.SetPaintEditorZoom(_zoom);
		AppState.SetPaintEditorTool(_currentTool);
		var (hue, saturation, value, alpha) = _colorWheel.GetHsv();
		AppState.SetPaintEditorColor(hue, saturation, value, alpha);
		AppState.SetPaintEditorColorMode(_colorWheel.GetColorMode());
		AppState.SetPaintEditorPan(_panTransform.X, _panTransform.Y);
		SaveBgRefSettings();
		ClearHistory();
	}

	private static ToggleButton CreateToolButton(string icon, string tool)
	{
		var button = new ToggleButton
		{
			Content = icon,
			Padding = new Thickness(ToolButtonPadding),
			MinWidth = ToolButtonMinWidth,
			Tag = tool,
		};
		return button;
	}

	private static ToggleButton CreateToolButton(Control icon, string tool)
	{
		var button = new ToggleButton
		{
			Content = icon,
			Padding = new Thickness(ToolButtonPadding),
			MinWidth = ToolButtonMinWidth,
			Tag = tool,
		};

		return button;
	}

	private static Button CreateToolbarButton(string text, EventHandler<RoutedEventArgs> handler)
	{
		var button = new Button { Content = text, Padding = new Thickness(ActionButtonPaddingHorizontal, ActionButtonPaddingVertical, ActionButtonPaddingHorizontal, ActionButtonPaddingVertical) };
		button.Click += handler;

		return button;
	}

	private void RenderAll()
	{
		_zoomTransform.ScaleX = _zoom;
		_zoomTransform.ScaleY = _zoom;

		_viewportContent.Width = _canvasWidth;
		_viewportContent.Height = _canvasHeight;

		Canvas.SetLeft(_canvasBgRect, 0);
		Canvas.SetTop(_canvasBgRect, 0);
		_canvasBgRect.Width = _canvasWidth;
		_canvasBgRect.Height = _canvasHeight;

		Canvas.SetLeft(_previewImage, 0);
		Canvas.SetTop(_previewImage, 0);
		_previewImage.Width = _canvasWidth;
		_previewImage.Height = _canvasHeight;

		if (_previewBitmap == null || _previewBitmap.PixelSize.Width != _canvasWidth || _previewBitmap.PixelSize.Height != _canvasHeight)
		{
			_previewBitmap?.Dispose();
			_previewBitmap = new WriteableBitmap(
				new PixelSize(_canvasWidth, _canvasHeight),
				new Vector(Dpi, Dpi),
				Avalonia.Platform.PixelFormat.Bgra8888,
				Avalonia.Platform.AlphaFormat.Unpremul);
			_previewImage.Source = _previewBitmap;
		}

		var pixels = Compose();
		using var lockedBitmap = _previewBitmap.Lock();
		Marshal.Copy(pixels, 0, lockedBitmap.Address, pixels.Length);

		_previewImage.InvalidateVisual();

		UpdateCoordsText();
		UpdateZoomText();
		UpdateCanvasSizeLabel();
		UpdateSpriteBoundsRect();
		UpdateHotspotMarker();
		UpdateHotspotCoords();
		UpdateMoveButtonsEnabled();
		UpdateResizeOverlay();
		UpdateBgRefRender();
	}

	private void UpdateCoordsText() =>
		_coordsText.Text = string.Format(CoordsFormat, _canvasWidth, _canvasHeight, _offsetX, _offsetY);

	private void UpdateZoomText() =>
		_zoomText.Text = string.Format(ZoomFormat, _zoom);

	private void UpdateCanvasSizeLabel() =>
		_canvasSizeLabel.Text = $"{_canvasWidth}x{_canvasHeight}";

	private void UpdateSpriteBoundsRect()
	{
		_spriteBoundsRect.StrokeThickness = SpriteBoundsStrokePx / _zoom;
		if (!_spriteBoundsRect.IsVisible)
			return;
		Canvas.SetLeft(_spriteBoundsRect, _offsetX);
		Canvas.SetTop(_spriteBoundsRect, _offsetY);
		_spriteBoundsRect.Width = _spriteWidth;
		_spriteBoundsRect.Height = _spriteHeight;
	}

	private void UpdateMoveButtonsEnabled()
	{
		var (minX, maxX) = HorizontalRange();
		var (minY, maxY) = VerticalRange();
		_moveLeftButton.IsEnabled = _offsetX > minX;
		_moveRightButton.IsEnabled = _offsetX < maxX;
		_moveUpButton.IsEnabled = _offsetY > minY;
		_moveDownButton.IsEnabled = _offsetY < maxY;
	}

	private void UpdateToolButtons()
	{
		var isMove = _currentTool == AppState.PaintEditorToolMove;
		var isHand = _currentTool == AppState.PaintEditorToolHand;
		var isCanvas = _currentTool == AppState.PaintEditorToolCanvas;
		var isBrush = _currentTool == AppState.PaintEditorToolBrush;
		var isEraser = _currentTool == AppState.PaintEditorToolEraser;
		var isFill = _currentTool == AppState.PaintEditorToolFill;
		var isHotspot = _currentTool == AppState.PaintEditorToolHotspot;
		var isBgRef = _currentTool == AppState.PaintEditorToolBgRef;
		var isIconSizes = _currentTool == AppState.PaintEditorToolIconSizes;

		_toolMoveBtn.IsChecked = isMove;
		_toolHandBtn.IsChecked = isHand;
		_toolBrushBtn.IsChecked = isBrush;
		_toolEraserBtn.IsChecked = isEraser;
		_toolFillBtn.IsChecked = isFill;
		_toolCanvasBtn.IsChecked = isCanvas;
		_toolHotspotBtn.IsChecked = isHotspot;
		_toolBgRefBtn.IsChecked = isBgRef;
		_toolIconSizesBtn.IsChecked = isIconSizes;

		_moveToolPanel.IsVisible = isMove;
		_handToolPanel.IsVisible = isHand;
		_canvasToolPanel.IsVisible = isCanvas;
		_brushToolPanel.IsVisible = isBrush || isFill;
		_eraserToolPanel.IsVisible = isEraser;
		_fillToolPanel.IsVisible = isFill;
		_hotspotToolPanel.IsVisible = isHotspot;
		_bgRefToolPanel.IsVisible = isBgRef;
		_iconSizesToolPanel.IsVisible = isIconSizes;

		_resizeOverlay.IsVisible = isCanvas;
		_hotspotMarker.IsVisible = isHotspot;
		_hotspotMarkerGlow.IsVisible = isHotspot;
		_paintCursorRect.IsVisible = false;

		if (isCanvas)
			UpdateResizeOverlay();
		if (isHotspot)
		{
			UpdateHotspotMarker();
			UpdateHotspotCoords();
		}
		if (isIconSizes)
			RefreshIconSizesPanel();

		UpdateBgRefRender();
	}

	private void SetTool(string tool)
	{
		if (_currentTool == AppState.PaintEditorToolIconSizes && tool != AppState.PaintEditorToolIconSizes)
		{
			var keepPreview = _iconSizesEditMode && _iconSizesPreviewSize != null;
			if (!keepPreview)
				RestoreIconSizesPreview();
		}

		if (_currentTool == AppState.PaintEditorToolCanvas && tool != AppState.PaintEditorToolCanvas && _hasCanvasResizeSnapshot)
		{
			_canvasWidth = _canvasResizeSnapshotWidth;
			_canvasHeight = _canvasResizeSnapshotHeight;
			_offsetX = _canvasResizeSnapshotOffsetX;
			_offsetY = _canvasResizeSnapshotOffsetY;
			_panTransform.X = _canvasResizeSnapshotPanX;
			_panTransform.Y = _canvasResizeSnapshotPanY;
			RenderAll();
		}

		_hasCanvasResizeSnapshot = false;

		if (tool == AppState.PaintEditorToolCanvas)
		{
			_canvasResizeSnapshotWidth = _canvasWidth;
			_canvasResizeSnapshotHeight = _canvasHeight;
			_canvasResizeSnapshotOffsetX = _offsetX;
			_canvasResizeSnapshotOffsetY = _offsetY;
			_canvasResizeSnapshotPanX = _panTransform.X;
			_canvasResizeSnapshotPanY = _panTransform.Y;
			_hasCanvasResizeSnapshot = true;
		}

		_currentTool = tool;
		AppState.SetPaintEditorTool(_currentTool);
		UpdateToolButtons();
	}

	private Point GetCanvasPosition(PointerEventArgs e)
	{
		return e.GetPosition(_viewportContent);
	}

	private void OnViewportPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		var pointerProperties = e.GetCurrentPoint(_viewportHost);

		if (pointerProperties.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
		{
			if (_currentTool == AppState.PaintEditorToolMove)
			{
				var canvasPosition = GetCanvasPosition(e);

				if (canvasPosition.X < 0 || canvasPosition.X >= _canvasWidth || canvasPosition.Y < 0 || canvasPosition.Y >= _canvasHeight)
					return;

				PushHistory();
				_isDraggingSprite = true;
				_spriteDragStart = GetCanvasPosition(e);
				_dragStartOffsetX = _offsetX;
				_dragStartOffsetY = _offsetY;
				e.Handled = true;

				return;
			}

			if (IsEyedropperActive(e))
			{
				PickColorUnderCursor();
				e.Handled = true;
				return;
			}

			if (IsPaintTool)
			{
				PaintBegin(GetCanvasPosition(e), e.KeyModifiers);
				e.Handled = true;
				return;
			}

			if (_currentTool == AppState.PaintEditorToolFill)
			{
				var canvasPosition = GetCanvasPosition(e);
				PushHistory();
				FloodFill((int)Math.Floor(canvasPosition.X), (int)Math.Floor(canvasPosition.Y));
				RenderAll();
				e.Handled = true;

				return;
			}

			if (_currentTool == AppState.PaintEditorToolHotspot)
			{
				PushHistory();
				_isDraggingHotspot = true;
				SetHotspotFromCanvasPosition(GetCanvasPosition(e));
				e.Handled = true;
				return;
			}

			if (_currentTool == AppState.PaintEditorToolBgRef && _bgRefBitmap != null)
			{
				var canvasPosition = GetCanvasPosition(e);

				if (canvasPosition.X < 0 || canvasPosition.X >= _canvasWidth || canvasPosition.Y < 0 || canvasPosition.Y >= _canvasHeight)
					return;

				_isDraggingBgRef = true;
				_bgRefDragStart = canvasPosition;
				_bgRefDragStartOffsetX = _bgRefOffsetX;
				_bgRefDragStartOffsetY = _bgRefOffsetY;
				e.Handled = true;

				return;
			}

			if (_currentTool == AppState.PaintEditorToolCanvas)
			{
				var resizePosition = e.GetPosition(_viewportHost);
				StartResizeDrag(resizePosition);
				e.Handled = true;

				return;
			}

			if (_currentTool == AppState.PaintEditorToolHand)
			{
				_isPanning = true;
				_panStartPosition = e.GetPosition(_viewportHost);
				_panStartX = _panTransform.X;
				_panStartY = _panTransform.Y;
				e.Handled = true;

				return;
			}
		}

		if (pointerProperties.Properties.PointerUpdateKind == PointerUpdateKind.MiddleButtonPressed)
		{
			_isPanning = true;
			_panStartPosition = e.GetPosition(_viewportHost);
			_panStartX = _panTransform.X;
			_panStartY = _panTransform.Y;
			e.Handled = true;
		}
	}

	private void OnViewportPointerMoved(object? sender, PointerEventArgs e)
	{
		if (_isPainting)
		{
			var canvasPosition = GetCanvasPosition(e);
			PaintStrokeTo(canvasPosition, e.KeyModifiers);
			UpdatePaintCursor(canvasPosition);
			e.Handled = true;

			return;
		}

		if (_isDraggingHotspot)
		{
			SetHotspotFromCanvasPosition(GetCanvasPosition(e));
			e.Handled = true;

			return;
		}

		if (_isDraggingSprite)
		{
			var canvasPosition = GetCanvasPosition(e);
			var deltaX = (int)Math.Round(canvasPosition.X - _spriteDragStart.X);
			var deltaY = (int)Math.Round(canvasPosition.Y - _spriteDragStart.Y);
			var (minX, maxX) = HorizontalRange();
			var (minY, maxY) = VerticalRange();
			_offsetX = Math.Clamp(_dragStartOffsetX + deltaX, minX, maxX);
			_offsetY = Math.Clamp(_dragStartOffsetY + deltaY, minY, maxY);
			RenderAll();
			e.Handled = true;

			return;
		}

		if (_isDraggingBgRef)
		{
			var canvasPosition = GetCanvasPosition(e);
			var deltaX = (int)Math.Round(canvasPosition.X - _bgRefDragStart.X);
			var deltaY = (int)Math.Round(canvasPosition.Y - _bgRefDragStart.Y);
			_bgRefOffsetX = _bgRefDragStartOffsetX + deltaX;
			_bgRefOffsetY = _bgRefDragStartOffsetY + deltaY;
			UpdateBgRefRender();
			e.Handled = true;

			return;
		}

		if (_isResizeDragging)
		{
			var resizePosition = e.GetPosition(_viewportHost);
			UpdateResizeDrag(resizePosition);
			e.Handled = true;

			return;
		}

		if (IsPaintTool || _currentTool == AppState.PaintEditorToolFill)
		{
			UpdatePaintCursor(GetCanvasPosition(e));
		}

		if (!_isPanning)
			return;

		var panPosition = e.GetPosition(_viewportHost);
		_panTransform.X = _panStartX + (panPosition.X - _panStartPosition.X);
		_panTransform.Y = _panStartY + (panPosition.Y - _panStartPosition.Y);
	}

	private void OnViewportPointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		if (_isPainting)
		{
			PaintEnd();
			e.Handled = true;
			return;
		}

		_isDraggingHotspot = false;
		_isDraggingSprite = false;
		_isDraggingBgRef = false;
		_isResizeDragging = false;

		if (_isPanning)
		{
			_isPanning = false;
			e.Handled = true;
		}
	}

	private void OnViewportPointerWheelChanged(object? sender, PointerWheelEventArgs e)
	{
		if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
		{
			var zoomFactor = e.Delta.Y > 0 ? CanvasZoomStep : 1 / CanvasZoomStep;
			ZoomAtPoint(zoomFactor, e.GetPosition(_viewportHost));
			e.Handled = true;
		}
	}

	private void ZoomAtPoint(double zoomFactor, Point anchor)
	{
		var newZoom = Math.Clamp(_zoom * zoomFactor, AppState.PaintEditorZoomMin, AppState.PaintEditorZoomMax);
		if (newZoom == _zoom)
			return;

		var canvasX = (anchor.X - _panTransform.X) / _zoom;
		var canvasY = (anchor.Y - _panTransform.Y) / _zoom;

		_zoom = newZoom;
		AppState.SetPaintEditorZoom(_zoom);

		_panTransform.X = anchor.X - canvasX * _zoom;
		_panTransform.Y = anchor.Y - canvasY * _zoom;

		RenderAll();
	}

	private void OnCanvasZoomIn(object? sender, RoutedEventArgs e) =>
		ZoomAtPoint(CanvasZoomStep, new Point(_viewportHost.Bounds.Width / 2.0, _viewportHost.Bounds.Height / 2.0));

	private void OnCanvasZoomOut(object? sender, RoutedEventArgs e) =>
		ZoomAtPoint(1 / CanvasZoomStep, new Point(_viewportHost.Bounds.Width / 2.0, _viewportHost.Bounds.Height / 2.0));

	private void UpdatePaintCursor(Point canvasPosition)
	{
		var pixelX = (int)Math.Floor(canvasPosition.X);
		var pixelY = (int)Math.Floor(canvasPosition.Y);

		if (pixelX < 0 || pixelX >= _canvasWidth || pixelY < 0 || pixelY >= _canvasHeight)
		{
			_paintCursorRect.IsVisible = false;
			return;
		}

		var strokeThickness = 1.0 / _zoom;
		var size = 1 + strokeThickness;
		_paintCursorRect.StrokeThickness = strokeThickness;
		_paintCursorRect.Width = size;
		_paintCursorRect.Height = size;

		var color = _currentTool == AppState.PaintEditorToolEraser
			? PaintCursorEraserFillColor
			: Color.FromArgb(PaintCursorAlpha, _colorWheel.SelectedColor.R, _colorWheel.SelectedColor.G, _colorWheel.SelectedColor.B);
		_paintCursorRect.Fill = new SolidColorBrush(color);

		Canvas.SetLeft(_paintCursorRect, pixelX - strokeThickness / 2.0);
		Canvas.SetTop(_paintCursorRect, pixelY - strokeThickness / 2.0);
		_paintCursorRect.IsVisible = true;
	}

	private void OnShowSpriteBoundsChanged(object? sender, EventArgs e)
	{
		_showSpriteBounds = _showSpriteBoundsCheck.IsChecked == true;
		_spriteBoundsRect.IsVisible = _showSpriteBounds;
		AppState.SetShowSpriteBounds(_showSpriteBounds);
		UpdateSpriteBoundsRect();
	}

	private void OnSaveClick(object? sender, RoutedEventArgs e)
	{
		var pixels = Compose();
		if (IsFullyTransparent(pixels))
		{
			return;
		}

		var hotspotX = Math.Clamp(_offsetX + _hotspotOffsetX, 0, _canvasWidth - 1);
		var hotspotY = Math.Clamp(_offsetY + _hotspotOffsetY, 0, _canvasHeight - 1);
		Result = new CursorCanvasImage(_canvasWidth, _canvasHeight, hotspotX, hotspotY, pixels);
		CaptureIconSizesResult();

		if (IsAnimated)
		{
			_timelineFrames[_activeFrameIndex] = CaptureCurrentAsFrame();
			var resultFrames = new List<CursorCanvasImage>(_timelineFrames.Count);
			var resultDelays = new List<int>(_timelineFrames.Count);

			foreach (var frame in _timelineFrames)
			{
				var buffer = new byte[_canvasWidth * _canvasHeight * BytesPerPixel];
				Blit(buffer, _canvasWidth, _canvasHeight, frame.SpriteBgra, frame.SpriteWidth, frame.SpriteHeight, frame.OffsetX, frame.OffsetY);
				var frameHotspotX = Math.Clamp(frame.OffsetX + _hotspotOffsetX, 0, _canvasWidth - 1);
				var frameHotspotY = Math.Clamp(frame.OffsetY + _hotspotOffsetY, 0, _canvasHeight - 1);
				resultFrames.Add(new CursorCanvasImage(_canvasWidth, _canvasHeight, frameHotspotX, frameHotspotY, buffer));
				resultDelays.Add(frame.DurationMs);
			}

			ResultFrames = resultFrames;
			ResultFrameDelaysMs = resultDelays;
		}

		Close();
	}

	private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();

	private static IBrush CreateCheckerboardBrush()
	{
		var cellSize = CheckerboardCellSize;
		var bitmap = new WriteableBitmap(
			new PixelSize(cellSize * 2, cellSize * 2),
			new Vector(Dpi, Dpi),
			Avalonia.Platform.PixelFormat.Bgra8888,
			Avalonia.Platform.AlphaFormat.Unpremul);
		var pixels = new byte[(cellSize * 2) * (cellSize * 2) * BytesPerPixel];
		for (var y = 0; y < cellSize * 2; y++)
		{
			for (var x = 0; x < cellSize * 2; x++)
			{
				var index = (y * (cellSize * 2) + x) * BytesPerPixel;
				var isLight = (x / cellSize + y / cellSize) % 2 == 0;
				var cellValue = isLight ? CheckerboardLightValue : CheckerboardDarkValue;
				pixels[index] = cellValue;
				pixels[index + 1] = cellValue;
				pixels[index + 2] = cellValue;
				pixels[index + 3] = 255;
			}
		}
		using var lockedBitmap = bitmap.Lock();
		Marshal.Copy(pixels, 0, lockedBitmap.Address, pixels.Length);
		var brush = new ImageBrush(bitmap)
		{
			Stretch = Stretch.None,
			TileMode = TileMode.Tile,
			DestinationRect = new RelativeRect(0, 0, cellSize * 2, cellSize * 2, RelativeUnit.Absolute),
			SourceRect = new RelativeRect(0, 0, cellSize * 2, cellSize * 2, RelativeUnit.Absolute),
		};
		return brush;
	}

	private static int SnapOffset(double fraction, int min, int max) =>
		fraction == 0 ? min : fraction == 1 ? max : (min + max) / 2;

	private static (double X, double Y) ParseFraction(string tag)
	{
		var parts = tag.Split(',');
		return (double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
				double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
	}
}
