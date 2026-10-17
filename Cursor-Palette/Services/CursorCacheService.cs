namespace CursorPalette.Services;

public static class CursorCacheService
{
	public static void RemoveUnreferencedFiles()
	{
		var referencedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		referencedPaths.UnionWith(RegistryCursorService.ReadCurrentValues().Values.Select(Environment.ExpandEnvironmentVariables));

		if (RegistryCursorService.LoadSnapshotFromDisk() is { } snapshot)
			referencedPaths.UnionWith(snapshot.Values.Values.Select(Environment.ExpandEnvironmentVariables));

		RemoveUnreferencedFiles(AppPaths.ScaledCursorsDir, referencedPaths);
		RemoveUnreferencedFiles(AppPaths.ApplyCacheDir, referencedPaths);
	}

	private static void RemoveUnreferencedFiles(string directory, IReadOnlySet<string> referencedPaths)
	{
		if (!Directory.Exists(directory))
			return;

		foreach (var filePath in Directory.EnumerateFiles(directory))
		{
			if (referencedPaths.Contains(Path.GetFullPath(filePath)))
				continue;

			try
			{
				File.Delete(filePath);
			}
			catch (IOException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}
		}
	}
}
