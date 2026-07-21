using CursorPalette.Services;

namespace CursorPalette.Models;

public sealed class Preset
{
	public required string Id { get; init; }
	public required string Name { get; set; }
	public DateTime CreatedAt { get; init; } = DateTime.Now;
	public int BaseSize { get; set; } = RegistryCursorService.DefaultBaseSize;

	public Dictionary<string, string> Roles { get; init; } = new();
	public Dictionary<string, RoleRef> RoleRefs { get; init; } = new();
	public HashSet<string> LockedRoles { get; init; } = new();
}

public sealed class RoleRef
{
	public required string PresetId { get; init; }
	public required string FileName { get; init; }
}

public sealed class PresetDraft
{
	public string? Id { get; set; }
	public string Name { get; set; } = "";
	public int BaseSize { get; set; } = RegistryCursorService.DefaultBaseSize;
	public Dictionary<string, RoleSourceDraft> RoleSources { get; init; } = new();
	public HashSet<string> LockedRoles { get; init; } = new();
}

public sealed class RoleSourceDraft
{
	public string? OwnFilePath { get; init; }
	public RoleRef? Ref { get; init; }
}