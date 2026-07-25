using System.Diagnostics;
using CursorPalette.Services;

namespace CursorPalette.Linux.Services;

public sealed class LinuxFileExplorer : IFileExplorer
{
	public void RevealFile(string filePath)
	{
		if (!File.Exists(filePath) && !Directory.Exists(filePath))
			return;

		var dir = File.Exists(filePath) ? Path.GetDirectoryName(filePath) : filePath;
		if (dir == null)
			return;

		try
		{
			var psi = new ProcessStartInfo
			{
				FileName = "xdg-open",
				Arguments = $"\"{dir}\"",
				UseShellExecute = false,
				CreateNoWindow = true,
			};

			using var process = Process.Start(psi);
			process?.WaitForExit();
		}
		catch
		{
		}
	}
}
