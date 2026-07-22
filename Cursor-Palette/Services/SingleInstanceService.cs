using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CursorPalette.Services;

public static class SingleInstanceService
{
	private const string MutexName = "CursorPalette_SingleInstance_Mutex";
	private const string ActivationMessageName = "CursorPalette_WM_ACTIVATE_INSTANCE";

	private static Mutex? _mutex;
	private static readonly uint ActivationMessage = RegisterWindowMessage(ActivationMessageName);

	public static bool TryAcquire()
	{
		_mutex = new Mutex(true, MutexName, out var createdNew);
		return createdNew;
	}

	public static void NotifyExistingInstance() =>
		PostMessage(HWND_BROADCAST, ActivationMessage, IntPtr.Zero, IntPtr.Zero);

	public static void ListenForActivation(Views.MainWindow window)
	{
		var source = (HwndSource)PresentationSource.FromVisual(window)!;
		source.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
		{
			if (msg != ActivationMessage)
				return IntPtr.Zero;

			BringToForeground(window);
			ToastService.Show(window.RootGrid, Loc.Get("S.Toast.AlreadyRunning"));

			handled = true;
			return IntPtr.Zero;
		});
	}

	private static void BringToForeground(Window window)
	{
		if (window.WindowState == WindowState.Minimized)
			window.WindowState = WindowState.Normal;

		window.Show();

		var hwnd = new WindowInteropHelper(window).Handle;
		ForceForegroundWindow(hwnd);

		window.Activate();

		window.Topmost = true;
		window.Topmost = false;
	}

	private static void ForceForegroundWindow(IntPtr hwnd)
	{
		if (GetForegroundWindow() == hwnd)
		{
			ShowWindow(hwnd, SW_RESTORE);
			return;
		}

		var foregroundWindow = GetForegroundWindow();
		var foregroundThreadId = GetWindowThreadProcessId(foregroundWindow, IntPtr.Zero);
		var currentThreadId = GetCurrentThreadId();

		var attached = foregroundThreadId != currentThreadId &&
			AttachThreadInput(currentThreadId, foregroundThreadId, true);

		ShowWindow(hwnd, SW_RESTORE);
		BringWindowToTop(hwnd);
		SetForegroundWindow(hwnd);

		if (attached)
			AttachThreadInput(currentThreadId, foregroundThreadId, false);
	}

	private static readonly IntPtr HWND_BROADCAST = new(0xffff);
	private const int SW_RESTORE = 9;

	[DllImport("user32.dll")]
	private static extern uint RegisterWindowMessage(string message);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

	[DllImport("user32.dll")]
	private static extern bool BringWindowToTop(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

	[DllImport("kernel32.dll")]
	private static extern uint GetCurrentThreadId();

	[DllImport("user32.dll")]
	private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
}
