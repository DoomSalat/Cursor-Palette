using System.Runtime.InteropServices;
using System.Windows.Media;

namespace CursorPalette.Services;

internal static class NativeColorPicker
{
	[StructLayout(LayoutKind.Sequential)]
	private struct NativePoint
	{
		public int X;
		public int Y;
	}

	[DllImport("user32.dll")]
	private static extern bool GetCursorPos(out NativePoint point);

	[DllImport("user32.dll")]
	private static extern nint GetDC(nint hwnd);

	[DllImport("user32.dll")]
	private static extern int ReleaseDC(nint hwnd, nint hdc);

	[DllImport("gdi32.dll")]
	private static extern uint GetPixel(nint hdc, int x, int y);

	public static bool TryGetScreenPixelColor(out Color color)
	{
		if (!GetCursorPos(out var point))
		{
			color = default;
			return false;
		}

		color = GetScreenPixelColor(point.X, point.Y);
		return true;
	}

	public static Color GetScreenPixelColor(int screenX, int screenY)
	{
		var hdc = GetDC(0);

		try
		{
			var pixel = GetPixel(hdc, screenX, screenY);
			var red = (byte)(pixel & 0xFF);
			var green = (byte)((pixel >> 8) & 0xFF);
			var blue = (byte)((pixel >> 16) & 0xFF);

			return Color.FromRgb(red, green, blue);
		}
		finally
		{
			ReleaseDC(0, hdc);
		}
	}
}
