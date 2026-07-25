namespace CursorPalette.Models;

public enum PackageKind
{
	Bundle,
	Archive,
	Manifest,
	XcursorTheme,
	SinglePreset,
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
	public List<PackageGroupEntry> Groups { get; init; } = new();
}

public sealed class PackageGroupEntry
{
	public required string Id { get; init; }
	public required string Name { get; init; }
	public required string ColorKey { get; init; }
	public bool Collapsed { get; init; }
	public List<string> MemberKeys { get; init; } = new();
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

public sealed class ArchiveManifest
{
	public string Format { get; set; } = "";
	public int Version { get; set; }
	public List<ArchiveManifestPreset> Presets { get; set; } = new();
	public List<PackageGroupEntry> Groups { get; init; } = new();
}

public sealed class ArchiveManifestPreset
{
	public required string Id { get; set; }
	public required string Name { get; set; }
	public required string Folder { get; set; }
	public DateTimeOffset CreatedAt { get; set; }
	public int SortOrder { get; set; }
	public int BaseSize { get; set; }
	public Dictionary<string, string> Roles { get; set; } = new();
	public HashSet<string> LockedRoles { get; set; } = new();
}

public sealed class SinglePresetMarker
{
	public string Format { get; set; } = "";
	public int Version { get; set; }
	public string Name { get; set; } = "";
	public int BaseSize { get; set; }
	public HashSet<string> LockedRoles { get; set; } = new();
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
