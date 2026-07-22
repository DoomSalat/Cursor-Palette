namespace CursorPalette.Services;

public static class GroupColors
{
	public static readonly (string Key, string Hex)[] Palette =
	{
		("Red", "#FFE5484D"),
		("Orange", "#FFF2994A"),
		("Yellow", "#FFE0B93D"),
		("Lime", "#FF9CCB3D"),
		("Green", "#FF4CAF6D"),
		("Teal", "#FF2FB8B8"),
		("Cyan", "#FF3DAFE0"),
		("Blue", "#FF4F8CFF"),
		("Indigo", "#FF6B7AF0"),
		("Purple", "#FF9B6BF2"),
		("Pink", "#FFEF6FA7"),
		("Brown", "#FFB0784B"),
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
