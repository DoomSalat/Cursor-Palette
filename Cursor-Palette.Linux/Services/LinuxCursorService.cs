using System.Diagnostics;
using System.Text.Json;
using CursorPalette.Models;
using CursorPalette.Services;

namespace CursorPalette.Linux.Services;

public sealed class LinuxCursorService : ICursorService
{
	private const string DefaultCursorSize = "24";
	private const string SnapshotFileName = "cursor-snapshot.json";
	private const string CursorsSubdir = "cursors";
	private const string IndexThemeFileName = "index.theme";
	private const string OriginalThemeFileName = "original-theme.json";
	private const string ActiveDefaultThemeFileName = "active-default-theme.json";
	private const string CursorPalettePresetThemePrefix = "cursor-palette-";
	public const string AdwaitaThemeName = "Adwaita";

	private static readonly string HomeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
	private static readonly string IconsDir =
		Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? Path.Combine(HomeDir, ".local", "share", "icons");

	private static readonly string[] SizeOptions = { "16", "22", "24", "32", "48", "64" };

	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

	private string GetThemeDir(string themeName) => Path.Combine(IconsDir, themeName);
	private string GetCursorsDir(string themeName) => Path.Combine(GetThemeDir(themeName), CursorsSubdir);

	public void ApplyValues(IReadOnlyDictionary<string, string> values)
	{
		var themeName = $"cursor-palette-{DateTime.Now:yyyyMMddHHmmss}";
		var themeDir = GetThemeDir(themeName);
		var cursorsDir = GetCursorsDir(themeName);

		Directory.CreateDirectory(cursorsDir);

		foreach (var (roleName, sourcePath) in values)
		{
			if (!File.Exists(sourcePath))
				continue;

			var frames = XcursorWriter.LoadFrames(sourcePath);
			if (frames == null || frames.Count == 0)
				continue;

			var bytes = XcursorWriter.Build(frames);

			var aliases = XcursorWriter.RoleAliases.TryGetValue(roleName, out var names)
				? names
				: new[] { roleName.ToLowerInvariant() };

			foreach (var alias in aliases)
				File.WriteAllBytes(Path.Combine(cursorsDir, alias), bytes);
		}

		File.WriteAllText(Path.Combine(themeDir, IndexThemeFileName),
			$"[Icon Theme]\nName={themeName}\nInherits={GetOriginalThemeName()}\n");

		TryRunGsettings("org.gnome.desktop.interface", "cursor-theme", themeName);
	}

	public void SetBaseSize(int sizeInPixels)
	{
		var sizeStr = FindClosestSize(sizeInPixels);
		TryRunGsettings("org.gnome.desktop.interface", "cursor-size", sizeStr);
	}

	public int GetBaseSize()
	{
		var sizeStr = TryReadGsettings("org.gnome.desktop.interface", "cursor-size") ?? DefaultCursorSize;

		return int.TryParse(sizeStr, out var size) ? size : CursorConstants.DefaultBaseSize;
	}

	public Dictionary<string, string> ReadCurrentValues()
	{
		var themeName = TryReadGsettings("org.gnome.desktop.interface", "cursor-theme") ?? GetOriginalThemeName();
		var cursorsDir = GetCursorsDir(themeName);

		if (!Directory.Exists(cursorsDir))
			return new();

		var result = new Dictionary<string, string>();

		foreach (var role in CursorRoles.All)
		{
			if (!XcursorWriter.RoleAliases.TryGetValue(role.RegistryName, out var aliases))
				continue;

			foreach (var alias in aliases)
			{
				var path = Path.Combine(cursorsDir, alias);
				if (File.Exists(path))
				{
					result[role.RegistryName] = path;
					break;
				}
			}
		}

		return result;
	}

	public Dictionary<string, string> GetDefaultValues() => new();

	public void ResetToDefault() => SetDefaultTheme(GetOriginalThemeName());

	public void SetDefaultTheme(string themeName)
	{
		TryRunGsettings("org.gnome.desktop.interface", "cursor-theme", themeName);
		TryRunGsettings("org.gnome.desktop.interface", "cursor-size", DefaultCursorSize);
	}

	public void EnsureOriginalThemeCaptured()
	{
		var path = Path.Combine(PathProvider.Current.StateDir, OriginalThemeFileName);
		if (File.Exists(path))
			return;

		var current = TryReadGsettings("org.gnome.desktop.interface", "cursor-theme");
		var themeToStore = string.IsNullOrWhiteSpace(current)
			|| string.Equals(current, "default", StringComparison.OrdinalIgnoreCase)
			|| current.StartsWith(CursorPalettePresetThemePrefix, StringComparison.OrdinalIgnoreCase)
			? AdwaitaThemeName
			: current;

		Directory.CreateDirectory(PathProvider.Current.StateDir);
		File.WriteAllText(path, JsonSerializer.Serialize(new ThemeNameRecord { ThemeName = themeToStore }, JsonOptions));
	}

	public string GetOriginalThemeName()
	{
		var path = Path.Combine(PathProvider.Current.StateDir, OriginalThemeFileName);
		if (!File.Exists(path))
			return AdwaitaThemeName;

		try
		{
			var record = JsonSerializer.Deserialize<ThemeNameRecord>(File.ReadAllText(path));
			return string.IsNullOrWhiteSpace(record?.ThemeName) ? AdwaitaThemeName : record.ThemeName;
		}
		catch
		{
			return AdwaitaThemeName;
		}
	}

	public string? GetActiveDefaultThemeName()
	{
		var path = Path.Combine(PathProvider.Current.StateDir, ActiveDefaultThemeFileName);
		if (!File.Exists(path))
			return null;

		try
		{
			var record = JsonSerializer.Deserialize<ThemeNameRecord>(File.ReadAllText(path));
			return string.IsNullOrWhiteSpace(record?.ThemeName) ? null : record.ThemeName;
		}
		catch
		{
			return null;
		}
	}

	public void SetActiveDefaultThemeName(string? themeName)
	{
		var path = Path.Combine(PathProvider.Current.StateDir, ActiveDefaultThemeFileName);

		if (string.IsNullOrWhiteSpace(themeName))
		{
			File.Delete(path);
			return;
		}

		Directory.CreateDirectory(PathProvider.Current.StateDir);
		File.WriteAllText(path, JsonSerializer.Serialize(new ThemeNameRecord { ThemeName = themeName }, JsonOptions));
	}

	private sealed class ThemeNameRecord
	{
		public string ThemeName { get; set; } = "";
	}

	public CursorSnapshot TakeSnapshot()
	{
		return new CursorSnapshot
		{
			Values = ReadCurrentValues(),
			BaseSize = GetBaseSize(),
		};
	}

	public void RestoreSnapshot(CursorSnapshot snapshot)
	{
		if (snapshot.Values.Count > 0)
			ApplyValues(snapshot.Values);

		SetBaseSize(snapshot.BaseSize);
	}

	public void SaveSnapshotToDisk(CursorSnapshot snapshot)
	{
		var path = Path.Combine(PathProvider.Current.StateDir, SnapshotFileName);
		File.WriteAllText(path, JsonSerializer.Serialize(snapshot, JsonOptions));
	}

	public CursorSnapshot? LoadSnapshotFromDisk()
	{
		var path = Path.Combine(PathProvider.Current.StateDir, SnapshotFileName);

		if (!File.Exists(path))
			return null;

		try
		{
			return JsonSerializer.Deserialize<CursorSnapshot>(File.ReadAllText(path));
		}
		catch
		{
			return null;
		}
	}

	private static string FindClosestSize(int target)
	{
		var closest = SizeOptions[0];
		var minDiff = int.MaxValue;

		foreach (var sizeStr in SizeOptions)
		{
			if (int.TryParse(sizeStr, out var size))
			{
				var diff = Math.Abs(size - target);
				if (diff < minDiff)
				{
					minDiff = diff;
					closest = sizeStr;
				}
			}
		}

		return closest;
	}

	private static void TryRunGsettings(string schema, string key, string value)
	{
		try
		{
			var psi = new ProcessStartInfo
			{
				FileName = "gsettings",
				Arguments = $"set {schema} {key} {value}",
				UseShellExecute = false,
				RedirectStandardError = true,
				CreateNoWindow = true,
			};

			using var process = Process.Start(psi);
			process?.WaitForExit();
		}
		catch
		{
		}
	}

	private static string? TryReadGsettings(string schema, string key)
	{
		try
		{
			var psi = new ProcessStartInfo
			{
				FileName = "gsettings",
				Arguments = $"get {schema} {key}",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				CreateNoWindow = true,
			};

			using var process = Process.Start(psi);
			if (process == null)
				return null;

			var output = process.StandardOutput.ReadToEnd().Trim();
			process.WaitForExit();

			return output.StartsWith("'") && output.EndsWith("'")
				? output[1..^1]
				: output;
		}
		catch
		{
			return null;
		}
	}
}
