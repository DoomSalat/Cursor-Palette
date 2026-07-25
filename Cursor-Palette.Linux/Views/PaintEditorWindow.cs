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
using CursorPalette.Services;
using System.Runtime.InteropServices;

namespace CursorPalette.Linux.Views;

public class PaintEditorWindow : Window
{
	private const int DefaultCanvasSize = 32;
	private const int MaxCanvasDimension = 256;
	private const int BytesPerPixel = 4;
	private const double ZoomStep = 1.2;
	private const double MinZoom = 1.0;
	private const double MaxZoom = 40.0;
	private const double DefaultZoom = 8.0;
	private const double Dpi = 96.0;
	private const double HotspotMarkerSize = 8;
	private const double HotspotMarkerOffset = 4;
	private const double ToolbarPaddingX = 12;
	private const double ToolbarPaddingY = 8;
	private const double StatusBarPaddingX = 12;
	private const double StatusBarPaddingY = 4;
	private const double ToolbarSpacing = 8;
	private const double StatusBarSpacing = 16;
	private const double SeparatorMargin = 8;
	private const double TitleFontSize = 16;
	private const double StatusFontSize = 11;
	private const double ZoomTextMargin = 4;
	private const double WindowWidth = 900;
	private const double WindowHeight = 640;
	private const double BrushAlpha = 255;
	private const double EraserAlpha = 0;

	private const string ToolBrush = "brush";
	private const string ToolEraser = "eraser";
	private const string ToolHotspot = "hotspot";
	private const string TitleText = "Paint Editor";
	private const string BrushText = "Brush";
	private const string EraserText = "Eraser";
	private const string HotspotText = "Hotspot";
	private const string ImportText = "Import";
	private const string ZoomOutText = "−";
	private const string ZoomInText = "+";
	private const string SaveText = "Save";
	private const string CancelText = "Cancel";
	private const string ImportDialogTitle = "Import Image";
	private const string ImageFilterName = "Images";
	private const string ZoomPercentFormat = "{0}%";
	private const string CoordsFormat = "{0} × {1} px";
	private const string HotspotFormat = "X: {0}  Y: {1}";
	private const string DefaultZoomText = "800%";
	private const string DefaultCoordsText = "32 × 32 px";
	private const string DefaultHotspotText = "X: 0  Y: 0";

	private const string CurExtension = ".cur";
	private const string AniExtension = ".ani";
	private const string PngPattern = "*.png";
	private const string BmpPattern = "*.bmp";
	private const string JpgPattern = "*.jpg";
	private const string JpegPattern = "*.jpeg";
	private const string GifPattern = "*.gif";
	private const string CurPattern = "*.cur";
	private const string AniPattern = "*.ani";

	private int _canvasWidth = DefaultCanvasSize;
	private int _canvasHeight = DefaultCanvasSize;
	private byte[] _pixels = new byte[DefaultCanvasSize * DefaultCanvasSize * BytesPerPixel];
	private int _hotspotX;
	private int _hotspotY;
	private double _zoom = DefaultZoom;
	private string _currentTool = ToolBrush;
	private bool _isDrawing;
	private WriteableBitmap? _bitmap;

	private readonly Image _canvasImage = new() { Stretch = Stretch.None };
	private readonly Ellipse _hotspotMarker = new()
	{
		Width = HotspotMarkerSize,
		Height = HotspotMarkerSize,
		Fill = Brushes.Red,
		Stroke = Brushes.White,
		StrokeThickness = 1,
		IsVisible = false,
		IsHitTestVisible = false,
	};
	private readonly TextBlock _zoomText = new() { Text = DefaultZoomText, VerticalAlignment = VerticalAlignment.Center, Margin = new(ZoomTextMargin, 0) };
	private readonly TextBlock _coordsText = new() { Text = DefaultCoordsText, FontSize = StatusFontSize };
	private readonly TextBlock _hotspotText = new() { Text = DefaultHotspotText, FontSize = StatusFontSize };
	private readonly Button _brushButton = new() { Content = BrushText };
	private readonly Button _eraserButton = new() { Content = EraserText };
	private readonly Button _hotspotButton = new() { Content = HotspotText };

	public CursorCanvasImage? Result { get; private set; }

	public PaintEditorWindow(CursorCanvasImage? source = null)
	{
		Title = TitleText;
		Width = WindowWidth;
		Height = WindowHeight;
		Background = Brushes.Transparent;

		if (source != null)
		{
			_canvasWidth = source.Width;
			_canvasHeight = source.Height;
			_pixels = new byte[source.Bgra.Length];
			Array.Copy(source.Bgra, _pixels, source.Bgra.Length);
			_hotspotX = source.HotspotX;
			_hotspotY = source.HotspotY;
		}

		BuildUi();

		_brushButton.Click += (_, _) => SelectTool(ToolBrush);
		_eraserButton.Click += (_, _) => SelectTool(ToolEraser);
		_hotspotButton.Click += (_, _) => SelectTool(ToolHotspot);

		_canvasImage.PointerPressed += OnCanvasPointerPressed;
		_canvasImage.PointerMoved += OnCanvasPointerMoved;
		_canvasImage.PointerReleased += OnCanvasPointerReleased;

		RenderCanvas();
		UpdateZoomText();
		UpdateCoordsText();
		UpdateHotspotMarker();
		UpdateToolButtons();
	}

	private void BuildUi()
	{
		var toolbar = new Border
		{
			Background = Brushes.Transparent,
			Padding = new(ToolbarPaddingX, ToolbarPaddingY),
			Child = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Spacing = ToolbarSpacing,
				VerticalAlignment = VerticalAlignment.Center,
				Children =
				{
					new TextBlock { Text = TitleText, FontSize = TitleFontSize, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center },
					new Separator { Margin = new(SeparatorMargin, 0) },
					_brushButton,
					_eraserButton,
					_hotspotButton,
					new Separator { Margin = new(SeparatorMargin, 0) },
					CreateToolbarButton(ImportText, OnImportClick),
					new Separator { Margin = new(SeparatorMargin, 0) },
					CreateToolbarButton(ZoomOutText, OnZoomOut),
					_zoomText,
					CreateToolbarButton(ZoomInText, OnZoomIn),
					new Separator { Margin = new(SeparatorMargin, 0) },
					CreateToolbarButton(SaveText, OnSaveClick),
					CreateToolbarButton(CancelText, OnCancelClick),
				},
			},
		};

		var canvasContainer = new Panel
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Children = { _canvasImage, _hotspotMarker },
		};

		var scrollViewer = new ScrollViewer
		{
			HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			Content = canvasContainer,
		};

		var statusBar = new Border
		{
			Background = Brushes.Transparent,
			Padding = new(StatusBarPaddingX, StatusBarPaddingY),
			Child = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Spacing = StatusBarSpacing,
				Children = { _coordsText, _hotspotText },
			},
		};

		Content = new Grid
		{
			RowDefinitions = new RowDefinitions("Auto,*,Auto"),
			Children =
			{
				toolbar,
				scrollViewer,
				statusBar,
			},
		};

		Grid.SetRow(toolbar, 0);
		Grid.SetRow(scrollViewer, 1);
		Grid.SetRow(statusBar, 2);
	}

	private static Button CreateToolbarButton(string text, EventHandler<RoutedEventArgs> handler)
	{
		var button = new Button { Content = text };
		button.Click += handler;
		return button;
	}

	private void RenderCanvas()
	{
		_bitmap?.Dispose();
		_bitmap = new WriteableBitmap(
			new PixelSize(_canvasWidth, _canvasHeight),
			new Vector(Dpi, Dpi),
			Avalonia.Platform.PixelFormat.Bgra8888,
			Avalonia.Platform.AlphaFormat.Unpremul);

		using var framebuffer = _bitmap.Lock();
		Marshal.Copy(_pixels, 0, framebuffer.Address, _pixels.Length);

		_canvasImage.Source = _bitmap;
		_canvasImage.Width = _canvasWidth * _zoom;
		_canvasImage.Height = _canvasHeight * _zoom;
	}

	private void RenderCanvasFromPixels()
	{
		if (_bitmap == null)
			return;

		using var framebuffer = _bitmap.Lock();
		Marshal.Copy(_pixels, 0, framebuffer.Address, _pixels.Length);
		_canvasImage.InvalidateVisual();
	}

	private void UpdateZoomText()
	{
		_zoomText.Text = string.Format(ZoomPercentFormat, (int)Math.Round(_zoom * 100));
	}

	private void UpdateCoordsText()
	{
		_coordsText.Text = string.Format(CoordsFormat, _canvasWidth, _canvasHeight);
	}

	private void UpdateHotspotMarker()
	{
		_hotspotMarker.IsVisible = true;
		_hotspotText.Text = string.Format(HotspotFormat, _hotspotX, _hotspotY);

		var x = _hotspotX * _zoom - HotspotMarkerOffset;
		var y = _hotspotY * _zoom - HotspotMarkerOffset;
		Canvas.SetLeft(_hotspotMarker, x);
		Canvas.SetTop(_hotspotMarker, y);
	}

	private void UpdateToolButtons()
	{
		_brushButton.FontWeight = _currentTool == ToolBrush ? FontWeight.Bold : FontWeight.Normal;
		_eraserButton.FontWeight = _currentTool == ToolEraser ? FontWeight.Bold : FontWeight.Normal;
		_hotspotButton.FontWeight = _currentTool == ToolHotspot ? FontWeight.Bold : FontWeight.Normal;
	}

	private void SelectTool(string tool)
	{
		_currentTool = tool;
		UpdateToolButtons();
	}

	private (int x, int y) GetPixelPosition(PointerEventArgs e)
	{
		var position = e.GetPosition(_canvasImage);
		var pixelX = (int)(position.X / _zoom);
		var pixelY = (int)(position.Y / _zoom);

		pixelX = Math.Clamp(pixelX, 0, _canvasWidth - 1);
		pixelY = Math.Clamp(pixelY, 0, _canvasHeight - 1);

		return (pixelX, pixelY);
	}

	private void SetPixel(int x, int y, byte red, byte green, byte blue, byte alpha)
	{
		if (x < 0 || x >= _canvasWidth || y < 0 || y >= _canvasHeight)
			return;

		var pixelIndex = (y * _canvasWidth + x) * BytesPerPixel;

		_pixels[pixelIndex + 0] = blue;
		_pixels[pixelIndex + 1] = green;
		_pixels[pixelIndex + 2] = red;
		_pixels[pixelIndex + 3] = alpha;
	}

	private void DrawAt(int x, int y)
	{
		if (_currentTool == ToolBrush)
			SetPixel(x, y, 0, 0, 0, (byte)BrushAlpha);
		else if (_currentTool == ToolEraser)
			SetPixel(x, y, 0, 0, 0, (byte)EraserAlpha);
	}

	private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (_currentTool == ToolHotspot)
		{
			var (x, y) = GetPixelPosition(e);
			_hotspotX = x;
			_hotspotY = y;

			UpdateHotspotMarker();

			return;
		}

		_isDrawing = true;
		var (pixelX, pixelY) = GetPixelPosition(e);

		DrawAt(pixelX, pixelY);
		RenderCanvasFromPixels();
	}

	private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
	{
		if (!_isDrawing)
			return;

		var (pixelX, pixelY) = GetPixelPosition(e);

		DrawAt(pixelX, pixelY);
		RenderCanvasFromPixels();
	}

	private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		_isDrawing = false;
	}

	public async void OnImportClick(object? sender, RoutedEventArgs e)
	{
		var topLevel = TopLevel.GetTopLevel(this);
		if (topLevel == null)
			return;

		var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
		{
			Title = ImportDialogTitle,
			AllowMultiple = false,
			FileTypeFilter = new[]
			{
				new FilePickerFileType(ImageFilterName)
				{
					Patterns = new[] { PngPattern, BmpPattern, JpgPattern, JpegPattern, GifPattern, CurPattern, AniPattern }
				}
			}
		});

		if (files.Count == 0)
			return;

		var path = files[0].Path.LocalPath;
		var extension = System.IO.Path.GetExtension(path).ToLowerInvariant();

		if (extension == CurExtension || extension == AniExtension)
		{
			var image = CursorCanvasService.TryRead(path);
			if (image != null)
			{
				_canvasWidth = image.Width;
				_canvasHeight = image.Height;
				_pixels = new byte[image.Bgra.Length];
				Array.Copy(image.Bgra, _pixels, image.Bgra.Length);
				_hotspotX = image.HotspotX;
				_hotspotY = image.HotspotY;

				RenderCanvas();
				UpdateCoordsText();
				UpdateHotspotMarker();
			}
		}
		else
		{
			try
			{
				using var bitmap = new Bitmap(path);
				var width = Math.Min(bitmap.PixelSize.Width, MaxCanvasDimension);
				var height = Math.Min(bitmap.PixelSize.Height, MaxCanvasDimension);

				_canvasWidth = width;
				_canvasHeight = height;
				_pixels = new byte[width * height * BytesPerPixel];

				var tempBitmap = new WriteableBitmap(
					new PixelSize(width, height),
					new Vector(Dpi, Dpi),
					Avalonia.Platform.PixelFormat.Bgra8888,
					Avalonia.Platform.AlphaFormat.Unpremul);

				using (var framebuffer = tempBitmap.Lock())
				{
					bitmap.CopyPixels(
						new PixelRect(0, 0, width, height),
						framebuffer.Address,
						width * height * BytesPerPixel,
						width * BytesPerPixel);
					Marshal.Copy(framebuffer.Address, _pixels, 0, _pixels.Length);
				}

				tempBitmap.Dispose();

				RenderCanvas();
				UpdateCoordsText();
				UpdateHotspotMarker();
			}
			catch
			{
			}
		}
	}

	public void OnZoomIn(object? sender, RoutedEventArgs e)
	{
		_zoom = Math.Min(_zoom * ZoomStep, MaxZoom);

		RenderCanvas();
		UpdateZoomText();
		UpdateHotspotMarker();
	}

	public void OnZoomOut(object? sender, RoutedEventArgs e)
	{
		_zoom = Math.Max(_zoom / ZoomStep, MinZoom);

		RenderCanvas();
		UpdateZoomText();
		UpdateHotspotMarker();
	}

	public void OnSaveClick(object? sender, RoutedEventArgs e)
	{
		Result = new CursorCanvasImage(_canvasWidth, _canvasHeight, _hotspotX, _hotspotY, _pixels);

		Close();
	}

	public void OnCancelClick(object? sender, RoutedEventArgs e)
	{
		Close();
	}
}
