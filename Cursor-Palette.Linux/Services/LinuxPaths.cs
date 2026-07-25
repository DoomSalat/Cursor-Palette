using System.Runtime.InteropServices;
using CursorPalette.Services;

namespace CursorPalette.Linux.Services;

public sealed class LinuxPaths : IPlatformPaths
{
	private static readonly string HomeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
	private static readonly string ConfigDir = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? Path.Combine(HomeDir, ".config");
	private static readonly string DataDir = Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? Path.Combine(HomeDir, ".local", "share");
	private static readonly string CacheDir = Environment.GetEnvironmentVariable("XDG_CACHE_HOME") ?? Path.Combine(HomeDir, ".cache");

	private const string AppFolderName = "cursor-palette";
	private const string PresetsFolderName = "presets";
	private const string StateFolderName = "state";

	private static string RootDir => Path.Combine(ConfigDir, AppFolderName);
	private static string DataRootDir => Path.Combine(DataDir, AppFolderName);

	public string DownloadsDir =>
		Environment.GetEnvironmentVariable("XDG_DOWNLOAD_DIR")
		?? Path.Combine(HomeDir, "Downloads");

	public string Root => RootDir;

	public string PresetsDir => Path.Combine(DataRootDir, PresetsFolderName);

	public string StateDir => Path.Combine(DataRootDir, StateFolderName);

	public string GroupsFile => Path.Combine(DataRootDir, "groups.json");

	public string BoardOrderFile => Path.Combine(DataRootDir, "board-order.json");

	public string ActiveStateFile => Path.Combine(StateDir, "active-state.json");

	public string SettingsFile => Path.Combine(RootDir, "settings.json");

	public string PreviousSnapshotFile => Path.Combine(StateDir, "previous-snapshot.json");

	public string ScaledCursorsDir => Path.Combine(CacheDir, AppFolderName, "scaled");

	public void EnsureCreated()
	{
		Directory.CreateDirectory(RootDir);
		Directory.CreateDirectory(DataRootDir);
		Directory.CreateDirectory(PresetsDir);
		Directory.CreateDirectory(StateDir);
		Directory.CreateDirectory(ScaledCursorsDir);
		Directory.CreateDirectory(DownloadsDir);
	}
}
