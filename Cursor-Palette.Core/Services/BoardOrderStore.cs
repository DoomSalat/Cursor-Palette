using System.Text.Json;

namespace CursorPalette.Services;

public static class BoardOrderStore
{
	public static List<string> Load()
	{
		if (!File.Exists(PathProvider.Current.BoardOrderFile))
			return new();

		try
		{
			return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(PathProvider.Current.BoardOrderFile)) ?? new();
		}
		catch
		{
			return new();
		}
	}

	public static void Save(List<string> order) =>
		File.WriteAllText(PathProvider.Current.BoardOrderFile, JsonSerializer.Serialize(order));
}
