using System.Text.Json;

namespace CursorPalette.Services;

public static class AppState
{
	private sealed class ActiveState
	{
		public string? ActivePresetId { get; set; }
	}

	public static string? GetActivePresetId()
	{
		if (!File.Exists(AppPaths.ActiveStateFile))
			return null;

		try
		{
			return JsonSerializer.Deserialize<ActiveState>(File.ReadAllText(AppPaths.ActiveStateFile))
				?.ActivePresetId;
		}
		catch
		{
			return null;
		}
	}

	public static void SetActivePresetId(string? id) =>
		File.WriteAllText(AppPaths.ActiveStateFile,
			JsonSerializer.Serialize(new ActiveState { ActivePresetId = id }));
}
