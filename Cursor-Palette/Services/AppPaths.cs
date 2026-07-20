namespace CursorPalette.Services;

public static class AppPaths
{
	private const string AppDataFolderName = "Cursor-Palette";
	private const string PresetsDirName = "presets";
	private const string StateDirName = "state";
	private const string ActiveStateFileName = "active.json";
	private const string SettingsFileName = "settings.json";
	private const string PreviousSnapshotFileName = "previous.json";

	public static string Root { get; } = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		AppDataFolderName);

	public static string PresetsDir => Path.Combine(Root, PresetsDirName);
	public static string StateDir => Path.Combine(Root, StateDirName);

	public static string ActiveStateFile => Path.Combine(StateDir, ActiveStateFileName);
	public static string SettingsFile => Path.Combine(StateDir, SettingsFileName);
	public static string PreviousSnapshotFile => Path.Combine(StateDir, PreviousSnapshotFileName);

	public static void EnsureCreated()
	{
		Directory.CreateDirectory(PresetsDir);
		Directory.CreateDirectory(StateDir);
	}
}
