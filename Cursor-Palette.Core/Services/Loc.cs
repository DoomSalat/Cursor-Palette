using System.Text.Json;

namespace CursorPalette.Services;

public static class Loc
{
	private static Dictionary<string, string> _strings = new(StringComparer.Ordinal);
	private static Dictionary<string, string> _fallbackStrings = new(StringComparer.Ordinal);

	public static void Initialize()
	{
		_fallbackStrings = LoadLanguage("en");
		Reload();
	}

	public static void Reload()
	{
		_strings = LoadLanguage(LocalizationManager.Current);
	}

	public static string Get(string key) =>
		_strings.TryGetValue(key, out var value) ? value
		: _fallbackStrings.TryGetValue(key, out var fallback) ? fallback
		: key;

	public static string Format(string key, params object[] args) =>
		string.Format(Get(key), args);

	private static Dictionary<string, string> LoadLanguage(string langCode)
	{
		var assetLoader = AssetLoaderProvider.Current;
		if (assetLoader == null)
			return new Dictionary<string, string>(StringComparer.Ordinal);

		try
		{
			using var stream = assetLoader.TryOpenAsset($"Localization/Strings.{langCode}.json");
			if (stream == null)
				return new Dictionary<string, string>(StringComparer.Ordinal);

			using var reader = new StreamReader(stream);
			var json = reader.ReadToEnd();

			var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
			return dict ?? new Dictionary<string, string>(StringComparer.Ordinal);
		}
		catch
		{
			return new Dictionary<string, string>(StringComparer.Ordinal);
		}
	}
}
