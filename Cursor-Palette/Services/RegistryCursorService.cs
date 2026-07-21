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
	public const int SizeStep = 16;

	private const string User32Dll = "user32.dll";
	private const string SchemeSourceName = "Scheme Source";
	private const int SchemeSourceUserDefined = 2;
	private const string CursorBaseSizeName = "CursorBaseSize";
	private const string CursorSizeName = "CursorSize";
	private const string WindowsDefaultSchemeName = "Windows Default";
	private const char SchemePathSeparator = ',';

	private const string CursorsKeyPath = @"Control Panel\Cursors";
	private const string AccessibilityKeyPath = @"Software\Microsoft\Accessibility";
	private const string SystemSchemesKeyPath =
		@"SOFTWARE\Microsoft\Windows\CurrentVersion\Control Panel\Cursors\Schemes";

	private const uint SpiSetCursors = 0x0057;
	private const uint SpiSetCursorSize = 0x2029;
	private const uint SpifUpdateIniFile = 0x01;
	private const uint SpifSendChange = 0x02;

	[DllImport(User32Dll, SetLastError = true)]
	private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

	public static void Refresh() =>
		SystemParametersInfo(SpiSetCursors, 0, IntPtr.Zero, SpifUpdateIniFile | SpifSendChange);

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
			var value = values.TryGetValue(role.RegistryName, out var resolvedValue) ? resolvedValue : "";
			key.SetValue(role.RegistryName, value, RegistryValueKind.ExpandString);
		}

		key.SetValue(SchemeSourceName, SchemeSourceUserDefined, RegistryValueKind.DWord);

		Refresh();
	}

	public static int GetBaseSize()
	{
		using var key = Registry.CurrentUser.OpenSubKey(CursorsKeyPath);
		return key?.GetValue(CursorBaseSizeName) is int size and >= DefaultBaseSize and <= MaxBaseSize
			? size
			: DefaultBaseSize;
	}

	public static void SetBaseSize(int sizeInPixels)
	{
		sizeInPixels = Math.Clamp(sizeInPixels / SizeStep * SizeStep, DefaultBaseSize, MaxBaseSize);
		using (var cursorsKey = Registry.CurrentUser.CreateSubKey(CursorsKeyPath)!)
			cursorsKey.SetValue(CursorBaseSizeName, sizeInPixels, RegistryValueKind.DWord);
		using (var accessibilityKey = Registry.CurrentUser.CreateSubKey(AccessibilityKeyPath)!)
			accessibilityKey.SetValue(CursorSizeName, (sizeInPixels - SizeStep) / SizeStep, RegistryValueKind.DWord);

		SystemParametersInfo(SpiSetCursorSize, 0, (IntPtr)sizeInPixels, SpifUpdateIniFile | SpifSendChange);
		SystemParametersInfo(SpiSetCursors, 0, IntPtr.Zero, SpifUpdateIniFile | SpifSendChange);
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

			if (schemes?.GetValue(WindowsDefaultSchemeName, null, RegistryValueOptions.DoNotExpandEnvironmentNames)
				is string scheme && scheme.Length > 0)
			{
				var parts = scheme.Split(SchemePathSeparator);

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
