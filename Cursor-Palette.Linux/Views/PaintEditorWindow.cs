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

	private int _canvasWidth = DefaultCanvasSize;
	private int _canvasHeight = DefaultCanvasSize;
	private byte[] _pixels = new byte[DefaultCanvasSize * DefaultCanvasSize * BytesPerPixel];
	private int _hotspotX;
	private int _hotspotY;
	private double _zoom = 8.0;
	private string _currentTool = "brush";
	private bool _isDrawing;
	private WriteableBitmap? _bitmap;

	private readonly Image _canvasImage = new() { Stretch = Stretch.None };
	private readonly Ellipse _hotspotMarker = new()
	{
		Width = 8,
		Height = 8,
		Fill = Brushes.Red,
		Stroke = Brushes.White,
		StrokeThickness = 1,
		IsVisible = false,
		IsHitTestVisible = false,
	};
	private readonly TextBlock _zoomText = new() { Text = "800%", VerticalAlignment = VerticalAlignment.Center, Margin = new(4, 0) };
	private readonly TextBlock _coordsText = new() { Text = "32 × 32 px", FontSize = 11 };
	private readonly TextBlock _hotspotText = new() { Text = "X: 0  Y: 0", FontSize = 11 };
	private readonly Button _brushButton = new() { Content = "Brush" };
	private readonly Button _eraserButton = new() { Content = "Eraser" };
	private readonly Button _hotspotButton = new() { Content = "Hotspot" };

	public CursorCanvasImage? Result { get; private set; }

	public PaintEditorWindow(CursorCanvasImage? source = null)
	{
		Title = "Paint Editor";
		Width = 900;
		Height = 640;
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

		_brushButton.Click += (_, _) => SelectTool("brush");
		_eraserButton.Click += (_, _) => SelectTool("eraser");
		_hotspotButton.Click += (_, _) => SelectTool("hotspot");

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
			Padding = new(12, 8),
			Child = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Spacing = 8,
				VerticalAlignment = VerticalAlignment.Center,
				Children =
				{
					new TextBlock { Text = "Paint Editor", FontSize = 16, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center },
					new Separator { Margin = new(8, 0) },
					_brushButton,
					_eraserButton,
					_hotspotButton,
					new Separator { Margin = new(8, 0) },
					CreateToolbarButton("Import", OnImportClick),
					new Separator { Margin = new(8, 0) },
					CreateToolbarButton("−", OnZoomOut),
					_zoomText,
					CreateToolbarButton("+", OnZoomIn),
					new Separator { Margin = new(8, 0) },
					CreateToolbarButton("Save", OnSaveClick),
					CreateToolbarButton("Cancel", OnCancelClick),
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
			Padding = new(12, 4),
			Child = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Spacing = 16,
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
		var btn = new Button { Content = text };
		btn.Click += handler;
		return btn;
	}

	private void RenderCanvas()
	{
		_bitmap?.Dispose();
		_bitmap = new WriteableBitmap(
			new PixelSize(_canvasWidth, _canvasHeight),
			new Vector(96, 96),
			Avalonia.Platform.PixelFormat.Bgra8888,
			Avalonia.Platform.AlphaFormat.Unpremul);

		using var fb = _bitmap.Lock();
		Marshal.Copy(_pixels, 0, fb.Address, _pixels.Length);

		_canvasImage.Source = _bitmap;
		_canvasImage.Width = _canvasWidth * _zoom;
		_canvasImage.Height = _canvasHeight * _zoom;
	}

	private void RenderCanvasFromPixels()
	{
		if (_bitmap == null)
			return;

		using var fb = _bitmap.Lock();
		Marshal.Copy(_pixels, 0, fb.Address, _pixels.Length);
		_canvasImage.InvalidateVisual();
	}

	private void UpdateZoomText()
	{
		_zoomText.Text = $"{(int)Math.Round(_zoom * 100)}%";
	}

	private void UpdateCoordsText()
	{
		_coordsText.Text = $"{_canvasWidth} × {_canvasHeight} px";
	}

	private void UpdateHotspotMarker()
	{
		_hotspotMarker.IsVisible = true;
		_hotspotText.Text = $"X: {_hotspotX}  Y: {_hotspotY}";

		var x = _hotspotX * _zoom - 4;
		var y = _hotspotY * _zoom - 4;
		Canvas.SetLeft(_hotspotMarker, x);
		Canvas.SetTop(_hotspotMarker, y);
	}

	private void UpdateToolButtons()
	{
		_brushButton.FontWeight = _currentTool == "brush" ? FontWeight.Bold : FontWeight.Normal;
		_eraserButton.FontWeight = _currentTool == "eraser" ? FontWeight.Bold : FontWeight.Normal;
		_hotspotButton.FontWeight = _currentTool == "hotspot" ? FontWeight.Bold : FontWeight.Normal;
	}

	private void SelectTool(string tool)
	{
		_currentTool = tool;
		UpdateToolButtons();
	}

	private (int x, int y) GetPixelPosition(PointerEventArgs e)
	{
		var pos = e.GetPosition(_canvasImage);
		var px = (int)(pos.X / _zoom);
		var py = (int)(pos.Y / _zoom);
		px = Math.Clamp(px, 0, _canvasWidth - 1);
		py = Math.Clamp(py, 0, _canvasHeight - 1);
		return (px, py);
	}

	private void SetPixel(int x, int y, byte r, byte g, byte b, byte a)
	{
		if (x < 0 || x >= _canvasWidth || y < 0 || y >= _canvasHeight)
			return;

		var idx = (y * _canvasWidth + x) * BytesPerPixel;
		_pixels[idx + 0] = b;
		_pixels[idx + 1] = g;
		_pixels[idx + 2] = r;
		_pixels[idx + 3] = a;
	}

	private void DrawAt(int x, int y)
	{
		if (_currentTool == "brush")
			SetPixel(x, y, 0, 0, 0, 255);
		else if (_currentTool == "eraser")
			SetPixel(x, y, 0, 0, 0, 0);
	}

	private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (_currentTool == "hotspot")
		{
			var (x, y) = GetPixelPosition(e);
			_hotspotX = x;
			_hotspotY = y;
			UpdateHotspotMarker();
			return;
		}

		_isDrawing = true;
		var (px, py) = GetPixelPosition(e);
		DrawAt(px, py);
		RenderCanvasFromPixels();
	}

	private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
	{
		if (!_isDrawing)
			return;

		var (px, py) = GetPixelPosition(e);
		DrawAt(px, py);
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
			Title = "Import Image",
			AllowMultiple = false,
			FileTypeFilter = new[]
			{
				new FilePickerFileType("Images")
				{
					Patterns = new[] { "*.png", "*.bmp", "*.jpg", "*.jpeg", "*.gif", "*.cur", "*.ani" }
				}
			}
		});

		if (files.Count == 0)
			return;

		var path = files[0].Path.LocalPath;
		var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();

		if (ext == ".cur" || ext == ".ani")
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
				using var bmp = new Bitmap(path);
				var w = Math.Min(bmp.PixelSize.Width, MaxCanvasDimension);
				var h = Math.Min(bmp.PixelSize.Height, MaxCanvasDimension);

				_canvasWidth = w;
				_canvasHeight = h;
				_pixels = new byte[w * h * BytesPerPixel];

				var tempBmp = new WriteableBitmap(
					new PixelSize(w, h),
					new Vector(96, 96),
					Avalonia.Platform.PixelFormat.Bgra8888,
					Avalonia.Platform.AlphaFormat.Unpremul);

				using (var fb = tempBmp.Lock())
				{
					bmp.CopyPixels(
						new PixelRect(0, 0, w, h),
						fb.Address,
						w * h * BytesPerPixel,
						w * BytesPerPixel);
					Marshal.Copy(fb.Address, _pixels, 0, _pixels.Length);
				}

				tempBmp.Dispose();
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
