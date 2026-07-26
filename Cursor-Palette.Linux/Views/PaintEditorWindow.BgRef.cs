using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CursorPalette.Services;
using System.Runtime.InteropServices;

namespace CursorPalette.Linux.Views;

public partial class PaintEditorWindow
{
	private List<Avalonia.Media.Imaging.Bitmap> _bgRefFrames = new();
	private int _bgRefFrameIndex;
	private Avalonia.Media.Imaging.Bitmap? _bgRefBitmap =>
		_bgRefFrames.Count > 0 ? _bgRefFrames[Math.Clamp(_bgRefFrameIndex, 0, _bgRefFrames.Count - 1)] : null;
	private double _bgRefOpacity = 50;
	private int _bgRefMargin;
	private int _bgRefOffsetX;
	private int _bgRefOffsetY;
	private bool _bgRefReady;
	private string? _bgRefCustomPath;
	private bool _bgRefBilinear;
	private bool _isDraggingBgRef;
	private Point _bgRefDragStart;
	private int _bgRefDragStartOffsetX;
	private int _bgRefDragStartOffsetY;

	private const double BgRefOpacityDefault = 50;
	private const double BgRefOpacityMaximum = 100;
	private const double BgRefMarginMaximum = 64;
	private const double BgRefMarginLargeChange = 4;
	private const double BgRefOpacityLargeChange = 10;
	private const int BgRefFrameButtonMinWidth = 28;
	private const int BgRefFrameButtonPadding = 4;
	private const int BgRefPanelSpacing = 4;
	private const int BgRefPanelSpacingLarge = 8;

	private Slider _bgRefOpacitySlider = null!;
	private Slider _bgRefMarginSlider = null!;
	private TextBlock _bgRefOpacityValue = null!;
	private TextBlock _bgRefMarginValue = null!;
	private Button _bgRefLoadButton = null!;
	private Button _bgRefResetButton = null!;
	private Button _bgRefResetSettingsButton = null!;
	private CheckBox _bgRefBilinearCheck = null!;
	private CheckBox _hideMainImageCheck = null!;
	private Button _bgRefFramePrevButton = null!;
	private Button _bgRefFrameNextButton = null!;
	private TextBlock _bgRefFrameIndexText = null!;

	private void BuildBgRefToolPanel()
	{
		_bgRefOpacitySlider = new Slider { Minimum = 0, Maximum = BgRefOpacityMaximum, Value = BgRefOpacityDefault, SmallChange = 1, LargeChange = BgRefOpacityLargeChange };
		_bgRefMarginSlider = new Slider { Minimum = 0, Maximum = BgRefMarginMaximum, Value = 0, SmallChange = 1, LargeChange = BgRefMarginLargeChange };
		_bgRefOpacityValue = new TextBlock { FontSize = ToolPanelLabelFontSize, Margin = new Thickness(0, 0, 0, BgRefPanelSpacing) };
		_bgRefMarginValue = new TextBlock { FontSize = ToolPanelLabelFontSize, Margin = new Thickness(0, 0, 0, BgRefPanelSpacing) };
		_bgRefLoadButton = new Button { Content = "Load Image", Padding = new Thickness(ActionButtonPaddingHorizontal, ActionButtonPaddingVertical, ActionButtonPaddingHorizontal, ActionButtonPaddingVertical) };
		_bgRefResetButton = new Button { Content = "Reset to Default", Padding = new Thickness(ActionButtonPaddingHorizontal, ActionButtonPaddingVertical, ActionButtonPaddingHorizontal, ActionButtonPaddingVertical) };
		_bgRefResetSettingsButton = new Button { Content = "Reset Settings", Padding = new Thickness(ActionButtonPaddingHorizontal, ActionButtonPaddingVertical, ActionButtonPaddingHorizontal, ActionButtonPaddingVertical) };
		_bgRefBilinearCheck = new CheckBox { Content = "Bilinear" };
		_hideMainImageCheck = new CheckBox { Content = "Hide Main Image" };
		_bgRefFramePrevButton = new Button { Content = "◀", Padding = new Thickness(BgRefFrameButtonPadding), MinWidth = BgRefFrameButtonMinWidth };
		_bgRefFrameNextButton = new Button { Content = "▶", Padding = new Thickness(BgRefFrameButtonPadding), MinWidth = BgRefFrameButtonMinWidth };
		_bgRefFrameIndexText = new TextBlock { FontSize = ToolPanelLabelFontSize, VerticalAlignment = VerticalAlignment.Center };

		_bgRefToolPanel.Children.Add(new TextBlock { Text = "Background Reference", FontWeight = FontWeight.SemiBold });
		_bgRefToolPanel.Children.Add(new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Spacing = BgRefPanelSpacing,
			Margin = new Thickness(0, BgRefPanelSpacing, 0, 0),
			Children = { _bgRefLoadButton, _bgRefResetButton },
		});
		_bgRefToolPanel.Children.Add(_bgRefResetSettingsButton);
		_bgRefToolPanel.Children.Add(new TextBlock { Text = "Opacity", FontSize = ToolPanelLabelFontSize, Margin = new Thickness(0, BgRefPanelSpacingLarge, 0, 0) });
		_bgRefToolPanel.Children.Add(_bgRefOpacityValue);
		_bgRefToolPanel.Children.Add(_bgRefOpacitySlider);
		_bgRefToolPanel.Children.Add(new TextBlock { Text = "Margin", FontSize = ToolPanelLabelFontSize, Margin = new Thickness(0, BgRefPanelSpacingLarge, 0, 0) });
		_bgRefToolPanel.Children.Add(_bgRefMarginValue);
		_bgRefToolPanel.Children.Add(_bgRefMarginSlider);
		_bgRefToolPanel.Children.Add(new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Spacing = BgRefPanelSpacingLarge,
			Margin = new Thickness(0, BgRefPanelSpacingLarge, 0, 0),
			Children = { _bgRefBilinearCheck, _hideMainImageCheck },
		});
		_bgRefToolPanel.Children.Add(new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Spacing = BgRefPanelSpacing,
			Margin = new Thickness(0, BgRefPanelSpacingLarge, 0, 0),
			Children = { _bgRefFramePrevButton, _bgRefFrameIndexText, _bgRefFrameNextButton },
		});

		_bgRefOpacitySlider.ValueChanged += OnBgRefOpacityChanged;
		_bgRefMarginSlider.ValueChanged += OnBgRefMarginChanged;
		_bgRefLoadButton.Click += OnBgRefLoadClick;
		_bgRefResetButton.Click += OnBgRefResetClick;
		_bgRefResetSettingsButton.Click += OnBgRefResetSettingsClick;
		_bgRefBilinearCheck.IsCheckedChanged += OnBgRefBilinearChanged;
		_hideMainImageCheck.IsCheckedChanged += OnHideMainImageChanged;
		_bgRefFramePrevButton.Click += OnBgRefFramePrevClick;
		_bgRefFrameNextButton.Click += OnBgRefFrameNextClick;
	}

	private void InitBgRef()
	{
		_bgRefOpacity = AppState.GetBgRefOpacity();
		_bgRefMargin = AppState.GetBgRefMargin();
		var (offsetX, offsetY) = AppState.GetBgRefOffset();
		_bgRefOffsetX = offsetX;
		_bgRefOffsetY = offsetY;

		_bgRefOpacitySlider.Value = _bgRefOpacity;
		_bgRefMarginSlider.Value = _bgRefMargin;
		_bgRefOpacityValue.Text = $"{_bgRefOpacity:0}%";
		_bgRefMarginValue.Text = _bgRefMargin.ToString("+0;-0;0", System.Globalization.CultureInfo.InvariantCulture);

		_bgRefBilinear = AppState.GetBgRefBilinear();
		_bgRefBilinearCheck.IsChecked = _bgRefBilinear;

		LoadDefaultRefImage();
		_bgRefReady = true;

		var customPath = AppState.GetBgRefCustomPath();
		if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
			LoadCustomRefImage(customPath);
	}

	private void SaveBgRefSettings()
	{
		AppState.SetBgRefOpacity(_bgRefOpacity);
		AppState.SetBgRefMargin(_bgRefMargin);
		AppState.SetBgRefOffset(_bgRefOffsetX, _bgRefOffsetY);
		AppState.SetBgRefCustomPath(_bgRefCustomPath);
		AppState.SetBgRefBilinear(_bgRefBilinear);
	}

	private void LoadDefaultRefImage()
	{
		if (string.IsNullOrEmpty(_roleName))
		{
			SetBgRefFrames(new List<Avalonia.Media.Imaging.Bitmap>());
			_bgRefCustomPath = null;
			UpdateBgRefRender();
			return;
		}

		var defaultPath = PlaceholderCursorDefaults.GetPath(_roleName);
		if (string.IsNullOrWhiteSpace(defaultPath) || !File.Exists(defaultPath))
		{
			SetBgRefFrames(new List<Avalonia.Media.Imaging.Bitmap>());
			_bgRefCustomPath = null;
			UpdateBgRefRender();
			return;
		}

		SetBgRefFrames(LoadCursorAsBitmapFrames(defaultPath));
		_bgRefCustomPath = null;
		UpdateBgRefRender();
	}

	private static List<Avalonia.Media.Imaging.Bitmap> LoadCursorAsBitmapFrames(string filePath)
	{
		try
		{
			var image = CursorCanvasService.TryRead(filePath);
			if (image != null)
			{
				var bitmap = new WriteableBitmap(
					new PixelSize(image.Width, image.Height),
					new Vector(Dpi, Dpi),
					Avalonia.Platform.PixelFormat.Bgra8888,
					Avalonia.Platform.AlphaFormat.Unpremul);
				using var lockedBitmap = bitmap.Lock();
				Marshal.Copy(image.Bgra, 0, lockedBitmap.Address, image.Bgra.Length);
				return new List<Avalonia.Media.Imaging.Bitmap> { bitmap };
			}
		}
		catch { }
		return new List<Avalonia.Media.Imaging.Bitmap>();
	}

	private async void LoadCustomRefImage(string path)
	{
		try
		{
			var bitmap = new WriteableBitmap(new PixelSize(1, 1), new Vector(Dpi, Dpi),
				Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Unpremul);
			await using var stream = File.OpenRead(path);
			var loaded = WriteableBitmap.Decode(stream);
			SetBgRefFrames(new List<Avalonia.Media.Imaging.Bitmap> { loaded });
			_bgRefCustomPath = path;
			UpdateBgRefRender();
		}
		catch { }
	}

	private void SetBgRefFrames(List<Avalonia.Media.Imaging.Bitmap> frames)
	{
		_bgRefFrames = frames;
		_bgRefFrameIndex = 0;
		UpdateBgRefFrameNav();
	}

	private void UpdateBgRefRender()
	{
		if (_bgRefBitmap == null)
		{
			_bgRefImage.IsVisible = false;
			return;
		}

		_bgRefImage.IsVisible = true;
		_bgRefImage.Source = _bgRefBitmap;

		var referenceWidth = (double)_bgRefBitmap.PixelSize.Width;
		var referenceHeight = (double)_bgRefBitmap.PixelSize.Height;

		if (referenceWidth <= 0 || referenceHeight <= 0)
		{
			_bgRefImage.IsVisible = false;
			return;
		}

		var availableWidth = Math.Max(0, _canvasWidth - 2 * _bgRefMargin);
		var availableHeight = Math.Max(0, _canvasHeight - 2 * _bgRefMargin);

		double displayWidth, displayHeight;

		if (referenceWidth <= availableWidth && referenceHeight <= availableHeight)
		{
			displayWidth = referenceWidth;
			displayHeight = referenceHeight;
		}
		else
		{
			var scale = Math.Min(availableWidth / referenceWidth, availableHeight / referenceHeight);
			displayWidth = referenceWidth * scale;
			displayHeight = referenceHeight * scale;
		}

		var positionX = (_canvasWidth - displayWidth) / 2.0 + _bgRefOffsetX;
		var positionY = (_canvasHeight - displayHeight) / 2.0 + _bgRefOffsetY;

		Avalonia.Controls.Canvas.SetLeft(_bgRefImage, positionX);
		Avalonia.Controls.Canvas.SetTop(_bgRefImage, positionY);
		_bgRefImage.Width = displayWidth;
		_bgRefImage.Height = displayHeight;
		_bgRefImage.Opacity = _bgRefOpacity / BgRefOpacityMaximum;
	}

	private void OnBgRefOpacityChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
	{
		if (!_bgRefReady) return;
		_bgRefOpacity = e.NewValue;
		_bgRefOpacityValue.Text = $"{_bgRefOpacity:0}%";
		UpdateBgRefRender();
	}

	private void OnBgRefMarginChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
	{
		if (!_bgRefReady) return;
		_bgRefMargin = (int)Math.Round(e.NewValue);
		_bgRefMarginValue.Text = _bgRefMargin.ToString("+0;-0;0", System.Globalization.CultureInfo.InvariantCulture);
		UpdateBgRefRender();
	}

	private async void OnBgRefLoadClick(object? sender, RoutedEventArgs e)
	{
		var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
		{
			Title = "Load reference image",
			AllowMultiple = false,
			FileTypeFilter = new[] { new FilePickerFileType("Image files") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif" } } },
		});

		if (files.Count > 0)
			LoadCustomRefImage(files[0].Path.LocalPath);
	}

	private void OnBgRefResetClick(object? sender, RoutedEventArgs e) => LoadDefaultRefImage();

	private void OnBgRefResetSettingsClick(object? sender, RoutedEventArgs e)
	{
		_bgRefOpacity = BgRefOpacityDefault;
		_bgRefMargin = 0;
		_bgRefOffsetX = 0;
		_bgRefOffsetY = 0;
		_bgRefOpacitySlider.Value = _bgRefOpacity;
		_bgRefMarginSlider.Value = _bgRefMargin;
		_bgRefOpacityValue.Text = $"{_bgRefOpacity:0}%";
		_bgRefMarginValue.Text = _bgRefMargin.ToString("+0;-0;0", System.Globalization.CultureInfo.InvariantCulture);
		_bgRefBilinear = false;
		_bgRefBilinearCheck.IsChecked = false;
		UpdateBgRefRender();
	}

	private void OnBgRefBilinearChanged(object? sender, EventArgs e)
	{
		if (!_bgRefReady) return;
		_bgRefBilinear = _bgRefBilinearCheck.IsChecked == true;
		UpdateBgRefRender();
	}

	private void OnHideMainImageChanged(object? sender, EventArgs e)
	{
		_hideMainImage = _hideMainImageCheck.IsChecked == true;
		_previewImage.IsVisible = !_hideMainImage;
	}

	private void UpdateBgRefFrameNav()
	{
		_bgRefFrameIndexText.Text = $"{_bgRefFrameIndex + 1} / {Math.Max(_bgRefFrames.Count, 1)}";
		_bgRefFramePrevButton.IsEnabled = _bgRefFrameIndex > 0;
		_bgRefFrameNextButton.IsEnabled = _bgRefFrameIndex < _bgRefFrames.Count - 1;
	}

	private void OnBgRefFramePrevClick(object? sender, RoutedEventArgs e)
	{
		if (_bgRefFrameIndex <= 0) return;
		_bgRefFrameIndex--;
		UpdateBgRefFrameNav();
		UpdateBgRefRender();
	}

	private void OnBgRefFrameNextClick(object? sender, RoutedEventArgs e)
	{
		if (_bgRefFrameIndex >= _bgRefFrames.Count - 1) return;
		_bgRefFrameIndex++;
		UpdateBgRefFrameNav();
		UpdateBgRefRender();
	}
}
