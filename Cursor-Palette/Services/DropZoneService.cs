using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CursorPalette.Services;

public static class DropZoneService
{
	private const double DefaultCornerRadius = 4;
	private const double IndicatorStrokeThickness = 3;
	private const double IndicatorMargin = 2;

	[DllImport("user32.dll")]
	private static extern IntPtr WindowFromPoint(POINT cursorPoint);

	[DllImport("user32.dll")]
	private static extern IntPtr GetAncestor(IntPtr windowHandle, uint gaFlags);

	[DllImport("user32.dll")]
	private static extern bool GetCursorPos(out POINT cursorPosition);

	private const uint GaRoot = 2;

	[StructLayout(LayoutKind.Sequential)]
	private struct POINT { public int X; public int Y; }

	public static bool IsMouseOverWindow(Window window)
	{
		if (!GetCursorPos(out var cursorPoint))
			return false;

		var handleFromPoint = WindowFromPoint(cursorPoint);
		if (handleFromPoint == IntPtr.Zero)
			return false;

		var windowHandle = new WindowInteropHelper(window).Handle;
		var rootHandle = GetAncestor(handleFromPoint, GaRoot);

		return rootHandle == windowHandle;
	}

	/// <summary>
	/// Runs a DragLeave hide-check one dispatcher tick later instead of synchronously.
	/// A same-process drag crossing into another window of this app can deliver this
	/// window's DragLeave before the OS has finished handing the point over to the
	/// other window's HWND, so an immediate WindowFromPoint check can still see this
	/// window and skip hiding. Deferring lets that handoff settle first.
	/// </summary>
	public static void HandleWindowDragLeave(Window window, Action onConfirmedLeave)
	{
		window.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
		{
			if (!IsMouseOverWindow(window))
				onConfirmedLeave();
		}));
	}

	/// <summary>
	/// Polls whether the cursor is still over <paramref name="window"/> and invokes
	/// <paramref name="onLeave"/> the moment it isn't. Owned windows opened with
	/// ShowDialog() disable their owner, and the OS drag-and-drop hit-test appears to
	/// skip disabled windows entirely - so moving the cursor from a modal editor onto
	/// its disabled owner never delivers a DragLeave/DragEnter pair at all. Polling
	/// doesn't depend on that notification arriving.
	/// </summary>
	public static IDisposable StartLeaveWatchdog(Window window, Action onLeave)
	{
		var timer = new DispatcherTimer(DispatcherPriority.Input)
		{
			Interval = TimeSpan.FromMilliseconds(75)
		};

		timer.Tick += (_, _) =>
		{
			if (IsMouseOverWindow(window))
				return;

			timer.Stop();
			onLeave();
		};

		timer.Start();

		return new Watchdog(timer);
	}

	private sealed class Watchdog(DispatcherTimer timer) : IDisposable
	{
		public void Dispose() => timer.Stop();
	}

	public static Rectangle CreateDropIndicator(double cornerRadius = DefaultCornerRadius)
	{
		return new Rectangle
		{
			Stroke = (Brush)Application.Current.FindResource("Brush.Accent"),
			StrokeThickness = IndicatorStrokeThickness,
			StrokeDashArray = new DoubleCollection { 4, 2 },
			RadiusX = cornerRadius,
			RadiusY = cornerRadius,
			Margin = new Thickness(IndicatorMargin),
			IsHitTestVisible = false,
			Visibility = Visibility.Collapsed
		};
	}

	public static void ShowIndicator(Rectangle indicator) =>
		indicator.Visibility = Visibility.Visible;

	public static void HideIndicator(Rectangle indicator) =>
		indicator.Visibility = Visibility.Collapsed;

	public static void Attach(
		FrameworkElement target,
		Rectangle indicator,
		Func<DragEventArgs, bool> canDrop,
		Action<string[]> onDrop)
	{
		IDisposable? watchdog = null;
		var ownerWindow = Window.GetWindow(target);

		void Hide()
		{
			HideIndicator(indicator);
			watchdog?.Dispose();
			watchdog = null;
		}

		target.DragEnter += (_, e) =>
		{
			if (ownerWindow != null)
				watchdog ??= StartLeaveWatchdog(ownerWindow, Hide);

			if (canDrop(e))
			{
				e.Effects = DragDropEffects.Copy;
				ShowIndicator(indicator);
			}
			else
			{
				e.Effects = DragDropEffects.None;
			}

			e.Handled = true;
		};

		target.DragOver += (_, e) =>
		{
			e.Effects = canDrop(e) ? DragDropEffects.Copy : DragDropEffects.None;
			e.Handled = true;
		};

		target.DragLeave += (_, e) =>
		{
			if (ownerWindow != null)
				HandleWindowDragLeave(ownerWindow, Hide);
			else
				Hide();

			e.Handled = true;
		};

		target.Drop += (_, e) =>
		{
			Hide();

			if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
				return;

			onDrop(paths);
			e.Handled = true;
		};

		if (ownerWindow != null)
			ownerWindow.DragLeave += (_, e) => HandleWindowDragLeave(ownerWindow, Hide);
	}

	public static void AttachManaged(
		FrameworkElement target,
		Func<DragEventArgs, bool> canDrop,
		Action<string[]> onDrop,
		Action? onDropCleanup = null)
	{
		target.DragOver += (_, e) =>
		{
			e.Effects = canDrop(e) ? DragDropEffects.Copy : DragDropEffects.None;
			e.Handled = true;
		};

		target.Drop += (_, e) =>
		{
			onDropCleanup?.Invoke();

			if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
				return;

			onDrop(paths);
		};
	}

	public static string? GetFirstFile(DragEventArgs e, Func<string, bool>? filter = null)
	{
		if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
			return null;

		return filter != null
			? paths.FirstOrDefault(p => File.Exists(p) && filter(p))
			: paths.FirstOrDefault(File.Exists);
	}

	public static bool HasFileDrop(DragEventArgs e) =>
		e.Data.GetDataPresent(DataFormats.FileDrop);
}
