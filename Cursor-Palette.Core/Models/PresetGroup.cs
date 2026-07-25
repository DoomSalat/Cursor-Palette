namespace CursorPalette.Models;

public sealed class PresetGroup
{
	public required string Id { get; init; }
	public required string Name { get; set; }
	public required string ColorKey { get; set; }
	public bool Collapsed { get; set; }
	public List<string> MemberPresetIds { get; init; } = new();
}
