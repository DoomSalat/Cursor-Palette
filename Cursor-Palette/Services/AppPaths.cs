using System.Runtime.InteropServices;

namespace CursorPalette.Services;

public static class AppPaths
{
	private static readonly Guid FolderIdDownloads = new("374DE290-123F-4565-9164-39C4925E467B");

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	private static extern int SHGetKnownFolderPath(ref Guid id, uint flags, nint token, out nint path);

	public static string DownloadsDir
	{
		get
		{
			var folderId = FolderIdDownloads;

			if (SHGetKnownFolderPath(ref folderId, 0, 0, out var pathPointer) == 0)
			{
				var path = Marshal.PtrToStringUni(pathPointer);
				Marshal.FreeCoTaskMem(pathPointer);

				if (!string.IsNullOrWhiteSpace(path))
					return path;
			}

			return Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
				"Downloads");
		}
	}

	private const string AppDataFolderName = "Cursor-Palette";
	private const string PresetsDirName = "presets";
	private const string StateDirName = "state";
	private const string ActiveStateFileName = "active.json";
	private const string SettingsFileName = "settings.json";
	private const string PreviousSnapshotFileName = "previous.json";
	private const string GroupsFileName = "groups.json";
	private const string BoardOrderFileName = "board-order.json";

	public static string Root { get; } = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		AppDataFolderName);

	public static string PresetsDir => Path.Combine(Root, PresetsDirName);
	public static string StateDir => Path.Combine(Root, StateDirName);
	public static string GroupsFile => Path.Combine(Root, GroupsFileName);
	public static string BoardOrderFile => Path.Combine(Root, BoardOrderFileName);

	public static string ActiveStateFile => Path.Combine(StateDir, ActiveStateFileName);
	public static string SettingsFile => Path.Combine(StateDir, SettingsFileName);
	public static string PreviousSnapshotFile => Path.Combine(StateDir, PreviousSnapshotFileName);

	public static void EnsureCreated()
	{
		Directory.CreateDirectory(PresetsDir);
		Directory.CreateDirectory(StateDir);
	}
}
