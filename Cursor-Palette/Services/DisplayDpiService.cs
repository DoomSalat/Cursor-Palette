using System.Runtime.InteropServices;

namespace CursorPalette.Services;

public static class DisplayDpiService
{
	private const string User32Dll = "user32.dll";
	private const string ShcoreDll = "shcore.dll";
	private const int MonitorDpiTypeEffective = 0;
	private const int SuccessResult = 0;
	private const double BaseDpi = 96.0;

	private static readonly IntPtr DpiAwarenessContextPerMonitorAwareV2 = new(-4);

	private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr deviceContext, IntPtr monitorRect, IntPtr data);

	[DllImport(User32Dll)]
	private static extern bool EnumDisplayMonitors(IntPtr deviceContext, IntPtr clipRect, MonitorEnumProc callback, IntPtr data);

	[DllImport(User32Dll)]
	private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

	[DllImport(ShcoreDll)]
	private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

	public static IReadOnlyList<double> GetDistinctMonitorScales()
	{
		var scales = new SortedSet<double> { 1.0 };

		// The app is only system-DPI-aware, so real per-monitor DPI is visible only under a per-monitor thread context.
		var previousContext = SetThreadDpiAwarenessContext(DpiAwarenessContextPerMonitorAwareV2);

		try
		{
			EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
			{
				if (GetDpiForMonitor(monitor, MonitorDpiTypeEffective, out var dpiX, out _) == SuccessResult)
					scales.Add(dpiX / BaseDpi);

				return true;
			}, IntPtr.Zero);
		}
		finally
		{
			if (previousContext != IntPtr.Zero)
				SetThreadDpiAwarenessContext(previousContext);
		}

		return scales.ToList();
	}
}
