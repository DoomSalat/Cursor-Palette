namespace CursorPalette.Services;

public interface IPlatformPaths
{
	string DownloadsDir { get; }
	string Root { get; }
	string PresetsDir { get; }
	string StateDir { get; }
	string GroupsFile { get; }
	string BoardOrderFile { get; }
	string ActiveStateFile { get; }
	string SettingsFile { get; }
	string PreviousSnapshotFile { get; }
	void EnsureCreated();
}

public static class PathProvider
{
	public static IPlatformPaths Current { get; set; } = null!;
}
