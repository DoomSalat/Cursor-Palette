using System.Reflection;
using CursorPalette.Services;

namespace CursorPalette.Linux.Services;

public sealed class LinuxAssetLoader : IAssetLoader
{
	private const string AssetsFolder = "Assets";

	public Stream? TryOpenAsset(string relativePath)
	{
		try
		{
			var fullPath = Path.Combine(AppContext.BaseDirectory, AssetsFolder, relativePath);
			if (File.Exists(fullPath))
				return File.OpenRead(fullPath);
		}
		catch
		{
		}

		return null;
	}
}
