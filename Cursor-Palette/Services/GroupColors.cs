namespace CursorPalette.Services;

public static class GroupColors
{
	public static readonly (string Key, string Hex)[] Palette =
	{
		("Red", "#FFE5484D"),
		("Orange", "#FFF2994A"),
		("Yellow", "#FFE0B93D"),
		("Green", "#FF4CAF6D"),
		("Teal", "#FF2FB8B8"),
		("Blue", "#FF4F8CFF"),
		("Purple", "#FF9B6BF2"),
		("Pink", "#FFEF6FA7"),
	};

	public static string ResolveHex(string colorKey)
	{
		foreach (var entry in Palette)
		{
			if (entry.Key == colorKey)
				return entry.Hex;
		}

		return Palette[0].Hex;
	}
}
