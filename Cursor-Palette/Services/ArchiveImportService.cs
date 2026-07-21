using SharpCompress.Archives;
using SharpCompress.Common;

namespace CursorPalette.Services;

public static class ArchiveImportService
{
	private static readonly string[] ArchiveExtensions = { ".zip", ".rar", ".7z" };
	private const string TempFolderPrefix = "cursor-palette-archive-";

	public static bool IsArchiveFile(string path) =>
		ArchiveExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

	public static string ExtractToTempFolder(string archivePath)
	{
		var destination = Path.Combine(Path.GetTempPath(), $"{TempFolderPrefix}{Guid.NewGuid():N}");
		Directory.CreateDirectory(destination);

		using var archive = ArchiveFactory.OpenArchive(archivePath);

		foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
		{
			var entryPath = Path.GetFullPath(Path.Combine(destination, entry.Key!));
			if (!entryPath.StartsWith(destination, StringComparison.OrdinalIgnoreCase))
				continue;

			Directory.CreateDirectory(Path.GetDirectoryName(entryPath)!);
			entry.WriteToFile(entryPath, new ExtractionOptions { Overwrite = true });
		}

		return destination;
	}
}
