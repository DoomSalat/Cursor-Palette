using System.Runtime.InteropServices;
using System.Text.Json;
using CursorPalette.Models;
using Microsoft.Win32;

namespace CursorPalette.Services;

public sealed class CursorSnapshot
{
	public Dictionary<string, string> Values { get; init; } = new();
	public int BaseSize { get; init; } = RegistryCursorService.DefaultBaseSize;
}

public static class RegistryCursorService
{
	public const int DefaultBaseSize = 32;
	public const int MaxBaseSize = 256;

	private const string CursorsKeyPath = @"Control Panel\Cursors";
	private const string AccessibilityKeyPath = @"Software\Microsoft\Accessibility";
	private const string SystemSchemesKeyPath =
		@"SOFTWARE\Microsoft\Windows\CurrentVersion\Control Panel\Cursors\Schemes";

	private const uint SPI_SETCURSORS = 0x0057;
	private const uint SPI_SETCURSORSIZE = 0x2029;
	private const uint SPIF_UPDATEINIFILE = 0x01;
	private const uint SPIF_SENDCHANGE = 0x02;

	[DllImport(Constants.Files.User32Dll, SetLastError = true)]
	private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

	public static void Refresh() =>
		SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

	public static Dictionary<string, string> ReadCurrentValues()
	{
		var result = new Dictionary<string, string>();
		using var key = Registry.CurrentUser.OpenSubKey(CursorsKeyPath);

		foreach (var role in CursorRoles.All)
			result[role.RegistryName] =
				key?.GetValue(role.RegistryName, "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";

		return result;
	}

	public static void ApplyValues(IReadOnlyDictionary<string, string> values)
	{
		using var key = Registry.CurrentUser.CreateSubKey(CursorsKeyPath)!;

		foreach (var role in CursorRoles.All)
		{
			var value = values.TryGetValue(role.RegistryName, out var v) ? v : "";
			key.SetValue(role.RegistryName, value, RegistryValueKind.ExpandString);
		}

		key.SetValue(Constants.Registry.SchemeSourceName, Constants.Registry.SchemeSourceUserDefined, RegistryValueKind.DWord);

		Refresh();
	}

	public static int GetBaseSize()
	{
		using var key = Registry.CurrentUser.OpenSubKey(CursorsKeyPath);
		return key?.GetValue(Constants.Registry.CursorBaseSizeName) is int size and >= DefaultBaseSize and <= MaxBaseSize
			? size
			: DefaultBaseSize;
	}

	public static void SetBaseSize(int sizePx)
	{
		sizePx = Math.Clamp(sizePx / Constants.Cursor.SizeStep * Constants.Cursor.SizeStep, DefaultBaseSize, MaxBaseSize);
		using (var cursors = Registry.CurrentUser.CreateSubKey(CursorsKeyPath)!)
			cursors.SetValue(Constants.Registry.CursorBaseSizeName, sizePx, RegistryValueKind.DWord);
		using (var acc = Registry.CurrentUser.CreateSubKey(AccessibilityKeyPath)!)
			acc.SetValue(Constants.Registry.CursorSizeName, (sizePx - Constants.Cursor.SizeStep) / Constants.Cursor.SizeStep, RegistryValueKind.DWord);

		// Размер передаётся через pvParam (не uiParam) — иначе Windows его игнорирует.
		SystemParametersInfo(SPI_SETCURSORSIZE, 0, (IntPtr)sizePx, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
		SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
	}

	public static CursorSnapshot TakeSnapshot() => new()
	{
		Values = ReadCurrentValues(),
		BaseSize = GetBaseSize(),
	};

	public static void SaveSnapshotToDisk(CursorSnapshot snapshot) =>
		File.WriteAllText(AppPaths.PreviousSnapshotFile,
			JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));

	public static CursorSnapshot? LoadSnapshotFromDisk()
	{
		if (!File.Exists(AppPaths.PreviousSnapshotFile))
			return null;

		try
		{
			return JsonSerializer.Deserialize<CursorSnapshot>(File.ReadAllText(AppPaths.PreviousSnapshotFile));
		}
		catch
		{
			return null;
		}
	}

	public static void RestoreSnapshot(CursorSnapshot snapshot)
	{
		ApplyValues(snapshot.Values);
		SetBaseSize(snapshot.BaseSize);
	}

	public static Dictionary<string, string> GetWindowsDefaultValues()
	{
		var values = GetFallbackDefaults();

		try
		{
			using var schemes = Registry.LocalMachine.OpenSubKey(SystemSchemesKeyPath);
			if (schemes?.GetValue(Constants.Registry.WindowsDefaultSchemeName, null, RegistryValueOptions.DoNotExpandEnvironmentNames)
				is string scheme && scheme.Length > 0)
			{
				var parts = scheme.Split(Constants.Registry.SchemePathSeparator);
				for (var i = 0; i < parts.Length && i < CursorRoles.All.Length; i++)
					values[CursorRoles.All[i].RegistryName] = parts[i].Trim();
			}
		}
		catch
		{
		}

		return values;
	}

	private static Dictionary<string, string> GetFallbackDefaults() =>
		new(CursorDefaultsMap.FallbackDefaults);

	public static void ResetToWindowsDefault()
	{
		ApplyValues(GetWindowsDefaultValues());
		SetBaseSize(DefaultBaseSize);
	}
}
