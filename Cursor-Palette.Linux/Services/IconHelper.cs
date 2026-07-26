using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Controls.Shapes;
using IOPath = System.IO.Path;

namespace CursorPalette.Linux.Services;

public static class IconHelper
{
	private const string ResourcesFolder = "Resources";

	private static readonly Dictionary<string, Bitmap> Cache = new(StringComparer.OrdinalIgnoreCase);

	public static Bitmap Load(string fileName)
	{
		if (Cache.TryGetValue(fileName, out var cached))
			return cached;

		var path = IOPath.Combine(AppContext.BaseDirectory, ResourcesFolder, fileName);
		var bitmap = new Bitmap(path);
		Cache[fileName] = bitmap;

		return bitmap;
	}

	public static Rectangle CreateIcon(string fileName, double size, IBrush fill)
	{
		return new Rectangle
		{
			Width = size,
			Height = size,
			Fill = fill,
			OpacityMask = new ImageBrush { Source = Load(fileName) },
		};
	}

	public static Rectangle CreateIcon(string fileName, double size, IBrush fill, double rotationDegrees)
	{
		var rect = CreateIcon(fileName, size, fill);
		rect.RenderTransform = new RotateTransform(rotationDegrees);
		rect.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

		return rect;
	}
}
