using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace CursorPalette.Services;

public static class CursorPreviewService
{
	private const string User32Dll = "user32.dll";
	private const string AniExtension = ".ani";
	private const string CurExtension = ".cur";

	private const uint ImageCursor = 2;
	private const uint LrLoadFromFile = 0x00000010;

	private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
	private static readonly Dictionary<string, AnimatedCursorFrames?> AnimatedCache = new(StringComparer.OrdinalIgnoreCase);

	[DllImport(User32Dll, CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

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
			if (string.Equals(Path.GetExtension(expanded), CurExtension, StringComparison.OrdinalIgnoreCase))
			{
				image = CursorCanvasService.TryReadAsBitmap(expanded);

				if (image != null)
				{
					Cache[expanded] = image;

					return image;
				}
			}

			var handle = LoadImage(IntPtr.Zero, expanded, ImageCursor, 0, 0, LrLoadFromFile);

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

	public static void ApplyPreview(Image image, string? filePath)
	{
		image.BeginAnimation(Image.SourceProperty, null);

		if (string.IsNullOrWhiteSpace(filePath))
		{
			image.Source = null;

			return;
		}

		var expanded = Environment.ExpandEnvironmentVariables(filePath);

		if (string.Equals(Path.GetExtension(expanded), AniExtension, StringComparison.OrdinalIgnoreCase))
		{
			var animated = GetAnimatedFrames(expanded);
			if (animated is { Frames.Count: > 0 })
			{
				image.Source = animated.Frames[animated.StepFrameIndices[0]];
				image.BeginAnimation(Image.SourceProperty, BuildAnimation(animated));
				return;
			}
		}

		image.Source = GetPreview(filePath);
	}

	private static AnimatedCursorFrames? GetAnimatedFrames(string expandedPath)
	{
		if (AnimatedCache.TryGetValue(expandedPath, out var cached))
			return cached;

		var result = File.Exists(expandedPath) ? AniCursorReader.Read(expandedPath) : null;
		AnimatedCache[expandedPath] = result;

		return result;
	}

	private static ObjectAnimationUsingKeyFrames BuildAnimation(AnimatedCursorFrames data)
	{
		var animation = new ObjectAnimationUsingKeyFrames();
		var cumulative = TimeSpan.Zero;

		for (var step = 0; step < data.StepFrameIndices.Count; step++)
		{
			var frame = data.Frames[data.StepFrameIndices[step]];
			animation.KeyFrames.Add(new DiscreteObjectKeyFrame(frame, KeyTime.FromTimeSpan(cumulative)));
			cumulative += data.StepDurations[step];
		}

		animation.Duration = new Duration(cumulative);
		animation.RepeatBehavior = RepeatBehavior.Forever;

		return animation;
	}

	public static void Invalidate(string filePath)
	{
		var expanded = Environment.ExpandEnvironmentVariables(filePath);

		Cache.Remove(expanded);
		AnimatedCache.Remove(expanded);
	}
}
