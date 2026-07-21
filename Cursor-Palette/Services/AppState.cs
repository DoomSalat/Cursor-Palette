using System.Text.Json;

namespace CursorPalette.Services;

public static class AppState
{
	public const double UiScaleMin = 0.8;
	public const double UiScaleMax = 1.5;
	public const double UiScaleDefault = 1.0;

	public const double GalleryCellScaleMin = 0.5;
	public const double GalleryCellScaleMax = 3.5;
	public const double GalleryCellScaleDefault = 1.0;

	public const double InfoTextScaleMin = 0.8;
	public const double InfoTextScaleMax = 1.6;
	public const double InfoTextScaleDefault = 1.0;

	private sealed class ActiveState
	{
		public string? ActivePresetId { get; set; }
	}

	private sealed class Settings
	{
		public int DefaultBaseSize { get; set; } = RegistryCursorService.DefaultBaseSize;
		public double UiScale { get; set; } = UiScaleDefault;
		public double GalleryCellScale { get; set; } = GalleryCellScaleDefault;
		public double InfoTextScale { get; set; } = InfoTextScaleDefault;
		public string? ThemeMode { get; set; }
		public string? Language { get; set; }
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

	private static Settings LoadSettings()
	{
		if (!File.Exists(AppPaths.SettingsFile))
			return new Settings();

		try
		{
			return JsonSerializer.Deserialize<Settings>(File.ReadAllText(AppPaths.SettingsFile)) ?? new Settings();
		}
		catch
		{
			return new Settings();
		}
	}

	private static void SaveSettings(Settings settings) =>
		File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(settings));

	public static int GetDefaultBaseSize() =>
		Math.Clamp(LoadSettings().DefaultBaseSize, RegistryCursorService.DefaultBaseSize, RegistryCursorService.MaxBaseSize);

	public static void SetDefaultBaseSize(int sizeInPixels)
	{
		var settings = LoadSettings();
		settings.DefaultBaseSize = sizeInPixels;

		SaveSettings(settings);
	}

	public static double GetUiScale() =>
		Math.Clamp(LoadSettings().UiScale, UiScaleMin, UiScaleMax);

	public static void SetUiScale(double scale)
	{
		var settings = LoadSettings();
		settings.UiScale = Math.Clamp(scale, UiScaleMin, UiScaleMax);

		SaveSettings(settings);
	}

	public static double GetGalleryCellScale() =>
		Math.Clamp(LoadSettings().GalleryCellScale, GalleryCellScaleMin, GalleryCellScaleMax);

	public static void SetGalleryCellScale(double scale)
	{
		var settings = LoadSettings();
		settings.GalleryCellScale = Math.Clamp(scale, GalleryCellScaleMin, GalleryCellScaleMax);

		SaveSettings(settings);
	}

	public static double GetInfoTextScale() =>
		Math.Clamp(LoadSettings().InfoTextScale, InfoTextScaleMin, InfoTextScaleMax);

	public static void SetInfoTextScale(double scale)
	{
		var settings = LoadSettings();
		settings.InfoTextScale = Math.Clamp(scale, InfoTextScaleMin, InfoTextScaleMax);

		SaveSettings(settings);
	}

	public static string? GetThemeMode() => LoadSettings().ThemeMode;

	public static void SetThemeMode(string mode)
	{
		var settings = LoadSettings();
		settings.ThemeMode = mode;

		SaveSettings(settings);
	}

	public static string? GetLanguage() => LoadSettings().Language;

	public static void SetLanguage(string code)
	{
		var settings = LoadSettings();
		settings.Language = code;

		SaveSettings(settings);
	}
}
