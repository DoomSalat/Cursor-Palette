namespace CursorPalette.Services;

public static class PlaceholderCursorDefaults
{
	private const string DefaultCursorsDirName = "DefaultCursors";

	private static readonly Dictionary<string, string> BundledFileNames = new(StringComparer.Ordinal)
	{
		["Crosshair"] = "Crosshair.cur",
		["IBeam"] = "IBeam.cur",
	};

	private static readonly Dictionary<string, string> ExtractedPaths = new(StringComparer.Ordinal);

	public static string? GetPath(string roleRegistryName)
	{
		if (!BundledFileNames.TryGetValue(roleRegistryName, out var fileName))
			return null;

		if (ExtractedPaths.TryGetValue(fileName, out var cachedPath))
			return cachedPath;

		var path = ExtractToCache(fileName);
		if (path != null)
			ExtractedPaths[fileName] = path;

		return path;
	}

	private static string? ExtractToCache(string fileName)
	{
		var destinationPath = Path.Combine(PathProvider.Current.Root, DefaultCursorsDirName, fileName);

		try
		{
			if (!File.Exists(destinationPath))
			{
				var assetLoader = AssetLoaderProvider.Current;
				if (assetLoader == null)
					return null;

				using var stream = assetLoader.TryOpenAsset($"{DefaultCursorsDirName}/{fileName}");
				if (stream == null)
					return null;

				Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

				using var fileStream = File.Create(destinationPath);
				stream.CopyTo(fileStream);
			}

			return destinationPath;
		}
		catch
		{
			return null;
		}
	}
}

public static class AssetLoaderProvider
{
	public static IAssetLoader? Current { get; set; }
}

public static class ScreenColorPickerProvider
{
	public static IScreenColorPicker? Current { get; set; }
}

public static class SingleInstanceProvider
{
	public static ISingleInstance? Current { get; set; }
}
