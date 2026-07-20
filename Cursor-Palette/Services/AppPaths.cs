namespace CursorPalette.Services;

public static class AppPaths
{
	public static string Root { get; } = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		Constants.App.AppDataFolderName);

	public static string PresetsDir => Path.Combine(Root, Constants.Paths.PresetsDirName);
	public static string StateDir => Path.Combine(Root, Constants.Paths.StateDirName);

	public static string ActiveStateFile => Path.Combine(StateDir, Constants.Paths.ActiveStateFileName);
	public static string SettingsFile => Path.Combine(StateDir, Constants.Paths.SettingsFileName);
	public static string PreviousSnapshotFile => Path.Combine(StateDir, Constants.Paths.PreviousSnapshotFileName);

	public static void EnsureCreated()
	{
		Directory.CreateDirectory(PresetsDir);
		Directory.CreateDirectory(StateDir);
	}
}
