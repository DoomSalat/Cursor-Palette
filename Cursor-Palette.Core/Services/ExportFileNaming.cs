namespace CursorPalette.Services;

public static class ExportFileNaming
{
	public static string Build(string? presetName, string? roleName, string fallbackPrefix, int? width = null, int? height = null)
	{
		var parts = new[] { presetName, roleName }
			.Select(Sanitize)
			.Where(part => part.Length > 0);

		var prefix = string.Join(" ", parts);

		if (string.IsNullOrEmpty(prefix))
			prefix = fallbackPrefix;

		return width.HasValue && height.HasValue ? $"{prefix} {width}x{height}" : prefix;
	}

	private static string Sanitize(string? part)
	{
		if (string.IsNullOrWhiteSpace(part))
			return "";

		var invalid = Path.GetInvalidFileNameChars();

		return string.Join("", part.Where(character => !invalid.Contains(character))).Trim();
	}
}
