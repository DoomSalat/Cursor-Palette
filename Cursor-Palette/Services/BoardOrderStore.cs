using System.Text.Json;

namespace CursorPalette.Services;

public static class BoardOrderStore
{
	public static List<string> Load()
	{
		if (!File.Exists(AppPaths.BoardOrderFile))
			return new();

		try
		{
			return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(AppPaths.BoardOrderFile)) ?? new();
		}
		catch
		{
			return new();
		}
	}

	public static void Save(List<string> order) =>
		File.WriteAllText(AppPaths.BoardOrderFile, JsonSerializer.Serialize(order));
}
