using System.Diagnostics;
using System.Runtime.InteropServices;
using CursorPalette.Services;

namespace CursorPalette.Linux.Services;

public sealed class LinuxScreenColorPicker : IScreenColorPicker
{
	[DllImport("libX11")]
	private static extern IntPtr XOpenDisplay(string? name);

	[DllImport("libX11")]
	private static extern int XCloseDisplay(IntPtr display);

	[DllImport("libX11")]
	private static extern IntPtr XRootWindow(IntPtr display, int screen);

	[DllImport("libX11")]
	private static extern IntPtr XGetImage(IntPtr display, IntPtr drawable, int x, int y, uint width, uint height, uint planeMask, int format);

	private const int ZPixmap = 2;
	private const int BgraBlueOffset = 0;
	private const int BgraGreenOffset = 1;
	private const int BgraRedOffset = 2;

	public bool TryGetScreenPixelColor(out (byte R, byte G, byte B) color)
	{
		try
		{
			color = GetScreenPixelColor(0, 0);
			return true;
		}
		catch
		{
			color = (0, 0, 0);
			return false;
		}
	}

	public (byte R, byte G, byte B) GetScreenPixelColor(int screenX, int screenY)
	{
		var display = IntPtr.Zero;

		try
		{
			display = XOpenDisplay(null);
			if (display == IntPtr.Zero)
				return (0, 0, 0);

			var root = XRootWindow(display, 0);
			var image = XGetImage(display, root, screenX, screenY, 1, 1, uint.MaxValue, ZPixmap);

			if (image == IntPtr.Zero)
				return (0, 0, 0);

			// XImage structure: first 8 bytes are header, pixel data at offset 8
			var blue = Marshal.ReadByte(image, 8 + BgraBlueOffset);
			var green = Marshal.ReadByte(image, 8 + BgraGreenOffset);
			var red = Marshal.ReadByte(image, 8 + BgraRedOffset);

			return (red, green, blue);
		}
		catch
		{
			return (0, 0, 0);
		}
		finally
		{
			if (display != IntPtr.Zero)
				XCloseDisplay(display);
		}
	}
}
