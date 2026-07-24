namespace CursorPalette.Services;

public static class HelpTextService
{
	private static readonly Dictionary<string, string> Cache = new();

	private static string[] GetFallbackOrder() =>
		new[] { LocalizationManager.Current, "en", "ru" };

	public static string Get(string topic)
	{
		var cacheKey = $"{LocalizationManager.Current}/{topic}";
		if (Cache.TryGetValue(cacheKey, out var cached))
			return cached;

		foreach (var lang in GetFallbackOrder())
		{
			var text = TryLoad(lang, topic);
			if (text != null)
			{
				Cache[cacheKey] = text;
				return text;
			}
		}

		Cache[cacheKey] = string.Empty;
		return string.Empty;
	}

	private static string? TryLoad(string lang, string topic)
	{
		var assetLoader = AssetLoaderProvider.Current;
		if (assetLoader == null)
			return null;

		try
		{
			using var stream = assetLoader.TryOpenAsset($"Localization/Help/{lang}/{topic}.md");

			if (stream == null)
				return null;

			using var reader = new StreamReader(stream);

			return reader.ReadToEnd().Replace("\r\n", "\n").TrimEnd('\n');
		}
		catch
		{
			return null;
		}
	}
}
