using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CursorPalette.Services;

namespace CursorPalette.Linux.Views;

public class ImportImageDialog : Window
{
	private const double DialogWidth = 420;
	private const double DialogHeight = 380;
	private const double DialogPadding = 16;
	private const double PreviewMaxSize = 200;
	private const int SmallImageThreshold = 64;

	public enum ImportMode { Over, Replace }

	public ImportMode ResultMode { get; private set; } = ImportMode.Over;
	public WriteableBitmap? Image { get; private set; }

	private readonly Image _previewImage;
	private readonly TextBlock _imageInfoText;
	private readonly Button _modeOverButton;
	private readonly Button _modeReplaceButton;
	private bool _isReplaceMode = true;

	public ImportImageDialog(WriteableBitmap? preview)
	{
		Title = Loc.Get("S.ImportImage.Title");
		Width = DialogWidth;
		Height = DialogHeight;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		CanResize = false;

		var root = new StackPanel
		{
			Margin = new Thickness(DialogPadding),
			Spacing = 12,
		};

		var infoLabel = new TextBlock
		{
			Text = Loc.Get("S.ImportImage.Info"),
			FontSize = 13,
			TextWrapping = TextWrapping.Wrap,
		};
		root.Children.Add(infoLabel);

		_previewImage = new Image
		{
			MaxWidth = PreviewMaxSize,
			MaxHeight = PreviewMaxSize,
			HorizontalAlignment = HorizontalAlignment.Center,
			Stretch = Stretch.Uniform,
		};

		var previewBorder = new Border
		{
			Child = _previewImage,
			HorizontalAlignment = HorizontalAlignment.Center,
			BorderBrush = Brushes.Gray,
			BorderThickness = new Thickness(1),
			Padding = new Thickness(8),
			MinHeight = 100,
		};
		root.Children.Add(previewBorder);

		_imageInfoText = new TextBlock
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			FontSize = 13,
		};
		root.Children.Add(_imageInfoText);

		var modeLabel = new TextBlock
		{
			Text = Loc.Get("S.ImportImage.Mode"),
			FontSize = 13,
		};
		root.Children.Add(modeLabel);

		var modeBar = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Spacing = 8,
		};

		_modeOverButton = new Button
		{
			Content = Loc.Get("S.ImportImage.Over"),
			MinWidth = 80,
		};
		_modeOverButton.Click += OnModeClick;
		modeBar.Children.Add(_modeOverButton);

		_modeReplaceButton = new Button
		{
			Content = Loc.Get("S.ImportImage.Replace"),
			MinWidth = 80,
			Background = Brushes.CornflowerBlue,
		};
		_modeReplaceButton.Click += OnModeClick;
		modeBar.Children.Add(_modeReplaceButton);

		root.Children.Add(modeBar);

		var bottomBar = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Spacing = 8,
			Margin = new Thickness(0, 4, 0, 0),
		};

		var cancelButton = new Button
		{
			Content = Loc.Get("S.ImportImage.Cancel"),
			MinWidth = 80,
		};
		cancelButton.Click += (_, _) => Close();
		bottomBar.Children.Add(cancelButton);

		var okButton = new Button
		{
			Content = Loc.Get("S.ImportImage.OK"),
			MinWidth = 80,
		};
		okButton.Click += OnOkClick;
		bottomBar.Children.Add(okButton);

		root.Children.Add(bottomBar);

		Content = root;

		var uiScale = AppState.GetUiScale();
		if (uiScale != 1.0)
		{
			root.RenderTransform = new ScaleTransform(uiScale, uiScale);
			root.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
		}

		if (preview != null)
		{
			Image = preview;
			_previewImage.Source = preview;
			_imageInfoText.Text = $"{preview.PixelSize.Width} × {preview.PixelSize.Height} px";
			UpdateScalingMode(preview);
		}

		root.AddHandler(DragDrop.DragOverEvent, OnDragOver);
		root.AddHandler(DragDrop.DropEvent, OnDrop);
	}

	private void UpdateScalingMode(WriteableBitmap bitmap)
	{
	}

	private static bool IsImageFile(string path)
	{
		var ext = Path.GetExtension(path);
		return !string.IsNullOrEmpty(ext) && (
			ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
			ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
			ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
			ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
			ext.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
			ext.Equals(".cur", StringComparison.OrdinalIgnoreCase) ||
			ext.Equals(".ani", StringComparison.OrdinalIgnoreCase));
	}

	private void OnDragOver(object? sender, DragEventArgs e)
	{
		var files = e.Data.GetFiles();
		if (files != null && files.Any(f => IsImageFile(f.Path.LocalPath)))
			e.DragEffects = DragDropEffects.Copy;
		else
			e.DragEffects = DragDropEffects.None;
		e.Handled = true;
	}

	private void OnDrop(object? sender, DragEventArgs e)
	{
		var files = e.Data.GetFiles();
		var file = files?.FirstOrDefault(f => IsImageFile(f.Path.LocalPath));
		if (file == null)
			return;

		LoadImage(file.Path.LocalPath);
		e.Handled = true;
	}

	internal void LoadImage(string path)
	{
		try
		{
			WriteableBitmap? bitmap = null;
			var ext = Path.GetExtension(path);

			if (ext.Equals(".cur", StringComparison.OrdinalIgnoreCase) ||
				ext.Equals(".ani", StringComparison.OrdinalIgnoreCase))
			{
				bitmap = LoadCursorAsBitmap(path);
			}
			else
			{
				using var stream = File.OpenRead(path);
				bitmap = WriteableBitmap.Decode(stream);
			}

			if (bitmap == null)
				return;

			Image = bitmap;
			_previewImage.Source = bitmap;
			_imageInfoText.Text = $"{bitmap.PixelSize.Width} × {bitmap.PixelSize.Height} px";
			UpdateScalingMode(bitmap);
		}
		catch
		{
		}
	}

	private static WriteableBitmap? LoadCursorAsBitmap(string filePath)
	{
		var ext = Path.GetExtension(filePath);

		if (ext.Equals(".ani", StringComparison.OrdinalIgnoreCase))
		{
			var frames = AniCursorReader.Read(filePath);
			if (frames == null || frames.Frames.Count == 0)
				return null;

			var frame = frames.Frames[0];
			return CursorCanvasImageToBitmap(frame);
		}

		var image = CursorCanvasService.TryRead(filePath);
		if (image != null)
			return CursorCanvasImageToBitmap(image);

		return null;
	}

	private static WriteableBitmap CursorCanvasImageToBitmap(CursorCanvasImage image)
	{
		var bmp = new WriteableBitmap(
			new PixelSize(image.Width, image.Height),
			new Vector(96, 96),
			Avalonia.Platform.PixelFormat.Bgra8888,
			Avalonia.Platform.AlphaFormat.Unpremul);
		using var locked = bmp.Lock();
		System.Runtime.InteropServices.Marshal.Copy(image.Bgra, 0, locked.Address, image.Bgra.Length);
		return bmp;
	}

	private void OnModeClick(object? sender, RoutedEventArgs e)
	{
		if (sender == _modeOverButton)
		{
			_isReplaceMode = false;
			_modeOverButton.Background = Brushes.CornflowerBlue;
			_modeReplaceButton.Background = null;
		}
		else
		{
			_isReplaceMode = true;
			_modeReplaceButton.Background = Brushes.CornflowerBlue;
			_modeOverButton.Background = null;
		}
	}

	private void OnOkClick(object? sender, RoutedEventArgs e)
	{
		if (Image == null)
			return;

		ResultMode = _isReplaceMode ? ImportMode.Replace : ImportMode.Over;
		Close(true);
	}
}
