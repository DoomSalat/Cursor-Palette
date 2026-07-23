using System.IO;
using System.Windows;

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
		var uri = new Uri($"pack://application:,,,/Resources/Localization/Help/{lang}/{topic}.md", UriKind.Absolute);

		try
		{
			using var stream = Application.GetResourceStream(uri)?.Stream;

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
