using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace CursorPalette.Services;

public static class ExplorerService
{
	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

	private const int SwRestore = 9;

	private const int SvsiSelect = 1;
	private const int SvsiDeselectOthers = 8;
	private const int SvsiEnsureVisible = 16;

	private const string ShellProgId = "Shell.Application";
	private const string ExplorerExe = "explorer.exe";
	private const string ExplorerSelectArgs = "/select,\"{0}\"";

	public static void RevealFile(string filePath)
	{
		var folderPath = Path.GetDirectoryName(filePath);

		if (folderPath != null && TrySelectInExistingWindow(folderPath, Path.GetFileName(filePath)))
			return;

		try
		{
			Process.Start(new ProcessStartInfo(ExplorerExe, string.Format(ExplorerSelectArgs, filePath))
			{
				UseShellExecute = true,
			});
		}
		catch
		{
		}
	}

	private static bool TrySelectInExistingWindow(string folderPath, string fileName)
	{
		try
		{
			var shellType = Type.GetTypeFromProgID(ShellProgId);

			if (shellType == null)
				return false;

			dynamic shell = Activator.CreateInstance(shellType)!;
			dynamic windows = shell.Windows();

			for (var i = 0; i < windows.Count; i++)
			{
				dynamic window = windows.Item(i);
				if (window == null)
					continue;

				dynamic document;
				try
				{
					document = window.Document;
				}
				catch
				{
					continue;
				}

				if (document == null)
					continue;

				dynamic folder;
				try
				{
					folder = document.Folder;
				}
				catch
				{
					continue;
				}

				if (folder == null)
					continue;

				string currentPath;
				try
				{
					currentPath = folder.Self.Path;
				}
				catch
				{
					continue;
				}

				if (!string.Equals(currentPath, folderPath, StringComparison.OrdinalIgnoreCase))
					continue;

				FocusWindow(window);

				try
				{
					document.Refresh();
				}
				catch
				{
				}

				SelectFileInFolder(document, folder, fileName);

				return true;
			}

			return false;
		}
		catch
		{
			return false;
		}
	}

	private static void FocusWindow(dynamic window)
	{
		try
		{
			var hwnd = new IntPtr((int)window.HWND);
			ShowWindow(hwnd, SwRestore);
			SetForegroundWindow(hwnd);
		}
		catch
		{
		}
	}

	private static void SelectFileInFolder(dynamic document, dynamic folder, string fileName)
	{
		try
		{
			dynamic items = folder.Items();

			for (var j = 0; j < items.Count; j++)
			{
				dynamic item = items.Item(j);

				if (item == null)
					continue;

				string itemName;
				try
				{
					itemName = item.Name;
				}
				catch
				{
					continue;
				}

				if (!string.Equals(itemName, fileName, StringComparison.OrdinalIgnoreCase))
					continue;

				try
				{
					document.SelectItem(item, SvsiSelect | SvsiDeselectOthers | SvsiEnsureVisible);
				}
				catch
				{
				}

				return;
			}
		}
		catch
		{
		}
	}
}
