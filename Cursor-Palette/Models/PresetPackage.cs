namespace CursorPalette.Models;

public enum PackageKind
{
	Bundle,
	Archive,
}

public sealed class PackageEntry
{
	public required string Key { get; init; }
	public required string DisplayName { get; init; }
	public int RoleCount { get; init; }
	public int BaseSize { get; init; }
	public string? PreviewPath { get; init; }
}

public sealed class DetectedPackage
{
	public required PackageKind Kind { get; init; }
	public required string ExtractedDir { get; init; }
	public required List<PackageEntry> Entries { get; init; }
}

public sealed class PackageMarker
{
	public string Format { get; set; } = "";
	public int Version { get; set; }
}

public sealed class ArchiveMarker
{
	public string Format { get; set; } = "";
	public int Version { get; set; }
	public List<ArchiveMarkerEntry> Presets { get; set; } = new();
}

public sealed class ArchiveMarkerEntry
{
	public required string Folder { get; set; }
	public required string Name { get; set; }
}

public sealed class PackageVersionUnsupportedException : Exception
{
	public int FoundVersion { get; }
	public int MaxSupportedVersion { get; }

	public PackageVersionUnsupportedException(int foundVersion, int maxSupportedVersion)
		: base($"Package version {foundVersion} is newer than the supported version {maxSupportedVersion}.")
	{
		FoundVersion = foundVersion;
		MaxSupportedVersion = maxSupportedVersion;
	}
}
