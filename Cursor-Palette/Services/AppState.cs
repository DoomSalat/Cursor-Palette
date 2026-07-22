using System.Text.Json;

namespace CursorPalette.Services;

public static class AppState
{
	public const double UiScaleMin = 0.8;
	public const double UiScaleMax = 1.5;
	public const double UiScaleDefault = 1.0;

	public const double EditorUiScaleMin = 0.8;
	public const double EditorUiScaleMax = 2.0;
	public const double EditorUiScaleDefault = 1.0;

	public const double GalleryCellScaleMin = 0.5;
	public const double GalleryCellScaleMax = 3.5;
	public const double GalleryCellScaleDefault = 1.0;

	public const double InfoTextScaleMin = 0.8;
	public const double InfoTextScaleMax = 1.6;
	public const double InfoTextScaleDefault = 1.0;

	public const double PaintEditorZoomMin = 1.0;
	public const double PaintEditorZoomMax = 32.0;
	public const double PaintEditorZoomDefault = 8.0;

	public const string PaintEditorToolMove = "Move";
	public const string PaintEditorToolHand = "Hand";
	public const string PaintEditorToolBrush = "Brush";
	public const string PaintEditorToolEraser = "Eraser";
	public const string PaintEditorToolFill = "Fill";
	public const string PaintEditorToolHotspot = "Hotspot";
	public const string PaintEditorToolCanvas = "Canvas";
	public const string PaintEditorToolDefault = PaintEditorToolMove;

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
		public bool ShowSpriteBounds { get; set; }
		public bool OpenFolderAfterDownload { get; set; } = true;
		public double PaintEditorWidth { get; set; } = 680;
		public double PaintEditorHeight { get; set; } = 600;
		public double EditorUiScale { get; set; } = EditorUiScaleDefault;
		public double HotspotEditorWidth { get; set; } = 680;
		public double HotspotEditorHeight { get; set; } = 600;
		public double MainWindowWidth { get; set; } = 860;
		public double MainWindowHeight { get; set; } = 640;
		public double PaintEditorZoom { get; set; } = PaintEditorZoomDefault;
		public string? PaintEditorTool { get; set; } = PaintEditorToolDefault;
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

	public static double GetEditorUiScale() => LoadSettings().EditorUiScale;

	public static void SetEditorUiScale(double scale)
	{
		var settings = LoadSettings();
		settings.EditorUiScale = Math.Clamp(scale, EditorUiScaleMin, EditorUiScaleMax);

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

	public static bool GetShowSpriteBounds() => LoadSettings().ShowSpriteBounds;

	public static void SetShowSpriteBounds(bool value)
	{
		var settings = LoadSettings();
		settings.ShowSpriteBounds = value;

		SaveSettings(settings);
	}

	public static bool GetOpenFolderAfterDownload() => LoadSettings().OpenFolderAfterDownload;

	public static void SetOpenFolderAfterDownload(bool value)
	{
		var settings = LoadSettings();
		settings.OpenFolderAfterDownload = value;

		SaveSettings(settings);
	}

	public static double GetPaintEditorWidth() => LoadSettings().PaintEditorWidth;
	public static double GetPaintEditorHeight() => LoadSettings().PaintEditorHeight;

	public static void SetPaintEditorSize(double width, double height)
	{
		var settings = LoadSettings();
		settings.PaintEditorWidth = width;
		settings.PaintEditorHeight = height;

		SaveSettings(settings);
	}

	public static double GetHotspotEditorWidth() => LoadSettings().HotspotEditorWidth;
	public static double GetHotspotEditorHeight() => LoadSettings().HotspotEditorHeight;

	public static void SetHotspotEditorSize(double width, double height)
	{
		var settings = LoadSettings();
		settings.HotspotEditorWidth = width;
		settings.HotspotEditorHeight = height;

		SaveSettings(settings);
	}

	public static double GetMainWindowWidth() => LoadSettings().MainWindowWidth;
	public static double GetMainWindowHeight() => LoadSettings().MainWindowHeight;

	public static void SetMainWindowSize(double width, double height)
	{
		var settings = LoadSettings();
		settings.MainWindowWidth = width;
		settings.MainWindowHeight = height;

		SaveSettings(settings);
	}

	public static double GetPaintEditorZoom() =>
		Math.Clamp(LoadSettings().PaintEditorZoom, PaintEditorZoomMin, PaintEditorZoomMax);

	public static void SetPaintEditorZoom(double zoom)
	{
		var settings = LoadSettings();
		settings.PaintEditorZoom = Math.Clamp(zoom, PaintEditorZoomMin, PaintEditorZoomMax);

		SaveSettings(settings);
	}

	public static string GetPaintEditorTool() =>
		LoadSettings().PaintEditorTool ?? PaintEditorToolDefault;

	public static void SetPaintEditorTool(string tool)
	{
		var settings = LoadSettings();
		settings.PaintEditorTool = tool;

		SaveSettings(settings);
	}
}
