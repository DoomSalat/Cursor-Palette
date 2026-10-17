using System.Security.Cryptography;
using System.Text;

namespace CursorPalette.Services;

public static class CursorApplyStagingService
{
	private const int HashLength = 12;

	public static Dictionary<string, string> Stage(IReadOnlyDictionary<string, string> values)
	{
		var result = new Dictionary<string, string>(values.Count);

		foreach (var (role, path) in values)
			result[role] = string.IsNullOrEmpty(path) || !File.Exists(path) ? path : StagePath(path);

		return result;
	}

	private static string StagePath(string sourcePath)
	{
		var cacheDir = AppPaths.ApplyCacheDir;
		Directory.CreateDirectory(cacheDir);

		var stamp = File.GetLastWriteTimeUtc(sourcePath).Ticks;
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{sourcePath.ToLowerInvariant()}|{stamp}"));
		var hashText = Convert.ToHexString(hash)[..HashLength];
		var fileName = $"{Path.GetFileNameWithoutExtension(sourcePath)}-{hashText}{Path.GetExtension(sourcePath)}";
		var destPath = Path.Combine(cacheDir, fileName);

		if (!File.Exists(destPath))
			File.Copy(sourcePath, destPath, overwrite: true);

		return destPath;
	}
}
