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

	private sealed class ActiveState
	{
		public string? ActivePresetId { get; set; }
	}

	private sealed class Settings
	{
		public int DefaultBaseSize { get; set; } = RegistryCursorService.DefaultBaseSize;
		public double UiScale { get; set; } = UiScaleDefault;
		public double GalleryCellScale { get; set; } = GalleryCellScaleDefault;
		public string? ThemeMode { get; set; }
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

	// ---- settings.json: несколько независимых настроек в одном файле ----
	// Каждый геттер/сеттер читает-модифицирует-пишет весь объект, чтобы
	// сохранение одной настройки не затирало остальные.

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

	public static void SetDefaultBaseSize(int sizePx)
	{
		var settings = LoadSettings();
		settings.DefaultBaseSize = sizePx;
		SaveSettings(settings);
	}

	/// <summary>Масштаб всего интерфейса (LayoutTransform окна), 0.8–1.5.</summary>
	public static double GetUiScale() =>
		Math.Clamp(LoadSettings().UiScale, UiScaleMin, UiScaleMax);

	public static void SetUiScale(double scale)
	{
		var settings = LoadSettings();
		settings.UiScale = Math.Clamp(scale, UiScaleMin, UiScaleMax);
		SaveSettings(settings);
	}

	/// <summary>Масштаб ячеек галереи пресетов, 0.75–1.75.</summary>
	public static double GetGalleryCellScale() =>
		Math.Clamp(LoadSettings().GalleryCellScale, GalleryCellScaleMin, GalleryCellScaleMax);

	public static void SetGalleryCellScale(double scale)
	{
		var settings = LoadSettings();
		settings.GalleryCellScale = Math.Clamp(scale, GalleryCellScaleMin, GalleryCellScaleMax);
		SaveSettings(settings);
	}

	/// <summary>"Dark"/"Light" либо null, если тема ещё не выбрана (первый запуск).</summary>
	public static string? GetThemeMode() => LoadSettings().ThemeMode;

	public static void SetThemeMode(string mode)
	{
		var settings = LoadSettings();
		settings.ThemeMode = mode;
		SaveSettings(settings);
	}
}
