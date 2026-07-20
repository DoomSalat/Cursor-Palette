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

	private sealed class Settings
	{
		public int DefaultBaseSize { get; set; } = RegistryCursorService.DefaultBaseSize;
	}

	public static int GetDefaultBaseSize()
	{
		if (!File.Exists(AppPaths.SettingsFile))
			return RegistryCursorService.DefaultBaseSize;

		try
		{
			var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(AppPaths.SettingsFile));
			return Math.Clamp(settings?.DefaultBaseSize ?? RegistryCursorService.DefaultBaseSize,
				RegistryCursorService.DefaultBaseSize, RegistryCursorService.MaxBaseSize);
		}
		catch
		{
			return RegistryCursorService.DefaultBaseSize;
		}
	}

	public static void SetDefaultBaseSize(int sizePx) =>
		File.WriteAllText(AppPaths.SettingsFile,
			JsonSerializer.Serialize(new Settings { DefaultBaseSize = sizePx }));
}
