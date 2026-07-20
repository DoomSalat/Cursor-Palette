using CursorPalette.Services;

namespace CursorPalette.Models;

public sealed class Preset
{
	public required string Id { get; init; }
	public required string Name { get; set; }
	public DateTime CreatedAt { get; init; } = DateTime.Now;
	public int BaseSize { get; set; } = RegistryCursorService.DefaultBaseSize;

	public Dictionary<string, string> Roles { get; init; } = new();
}

public sealed class PresetDraft
{
	public string? Id { get; set; }
	public string Name { get; set; } = "";
	public int BaseSize { get; set; } = RegistryCursorService.DefaultBaseSize;
	public Dictionary<string, string> RoleSources { get; init; } = new();
}