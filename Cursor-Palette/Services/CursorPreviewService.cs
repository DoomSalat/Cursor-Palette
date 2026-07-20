using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CursorPalette.Services;

public static class CursorPreviewService
{
	private const string User32Dll = "user32.dll";

	private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

	[DllImport(User32Dll, CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr LoadCursorFromFile(string lpFileName);

	[DllImport(User32Dll, SetLastError = true)]
	private static extern bool DestroyCursor(IntPtr hCursor);

	public static ImageSource? GetPreview(string? filePath)
	{
		if (string.IsNullOrWhiteSpace(filePath))
			return null;

		var expanded = Environment.ExpandEnvironmentVariables(filePath);

		if (Cache.TryGetValue(expanded, out var cached))
			return cached;

		ImageSource? image = null;

		if (File.Exists(expanded))
		{
			var handle = LoadCursorFromFile(expanded);

			if (handle != IntPtr.Zero)
			{
				try
				{
					image = Imaging.CreateBitmapSourceFromHIcon(
						handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

					(image as BitmapSource)?.Freeze();
				}
				catch
				{
					image = null;
				}
				finally
				{
					DestroyCursor(handle);
				}
			}
		}

		Cache[expanded] = image;
		return image;
	}

	public static void Invalidate(string filePath) =>
		Cache.Remove(Environment.ExpandEnvironmentVariables(filePath));
}
