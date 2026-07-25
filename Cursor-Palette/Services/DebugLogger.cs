using System.Text;

namespace CursorPalette.Services;

public static class DebugLogger
{
	private static readonly string LogPath = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
		"Downloads", "cursor-debug-log.txt");

	private static readonly object Lock = new();

	public static void Log(string message)
	{
		try
		{
			lock (Lock)
			{
				var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}";
				File.AppendAllText(LogPath, line, Encoding.UTF8);
			}
		}
		catch
		{
		}
	}

	public static void LogValues(string label, IReadOnlyDictionary<string, string> values)
	{
		var sb = new StringBuilder();
		sb.AppendLine(label);
		foreach (var (role, path) in values)
			sb.AppendLine($"  {role}: {path}");
		Log(sb.ToString());
	}

	public static void Clear()
	{
		try
		{
			lock (Lock)
				File.WriteAllText(LogPath, $"=== Cursor Debug Log started {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==={Environment.NewLine}", Encoding.UTF8);
		}
		catch
		{
		}
	}
}
