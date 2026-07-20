namespace CursorPalette.Services;

public static class PlaceholderCursorDefaults
{
	private static readonly Dictionary<string, string> BundledFileNames = new(StringComparer.Ordinal)
	{
		["Crosshair"] = "Crosshair.cur",
		["IBeam"] = "IBeam.cur",
	};

	public static string? GetPath(string roleRegistryName)
	{
		if (!BundledFileNames.TryGetValue(roleRegistryName, out var fileName))
			return null;

		var path = Path.Combine(AppContext.BaseDirectory, "Resources", "DefaultCursors", fileName);
		return File.Exists(path) ? path : null;
	}
}
