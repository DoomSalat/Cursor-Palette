using System.Windows;

namespace CursorPalette.Services;

public static class PlaceholderCursorDefaults
{
	private const string DefaultCursorsDirName = "DefaultCursors";
	private const string PackUriPrefix = "pack://application:,,,/Resources/";

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
		var destinationPath = Path.Combine(AppPaths.Root, DefaultCursorsDirName, fileName);

		try
		{
			if (!File.Exists(destinationPath))
			{
				var uri = new Uri($"{PackUriPrefix}{DefaultCursorsDirName}/{fileName}");
				var resource = Application.GetResourceStream(uri);
				if (resource == null)
					return null;

				Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

				using var fileStream = File.Create(destinationPath);
				resource.Stream.CopyTo(fileStream);
			}

			return destinationPath;
		}
		catch
		{
			return null;
		}
	}
}
