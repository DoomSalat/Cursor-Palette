namespace CursorPalette.Models;

public sealed record CursorRoleInfo(string RegistryName, string DisplayKey, string[] Aliases);

public static class CursorRoles
{
	public const string ArrowRoleName = "Arrow";

	public static CursorRoleInfo[] All { get; } =
	{
		new("Arrow",       "Role.Arrow",       new[] { "normal", "arrow", "default", "pointer", "aero_arrow" }),
		new("Help",        "Role.Help",        new[] { "help", "helpsel", "aero_helpsel" }),
		new("AppStarting", "Role.AppStarting", new[] { "working", "appstarting", "workinginbackground", "aero_working" }),
		new("Wait",        "Role.Wait",        new[] { "busy", "wait", "aero_busy" }),
		new("Crosshair",   "Role.Crosshair",   new[] { "precision", "cross", "crosshair" }),
		new("IBeam",       "Role.IBeam",       new[] { "text", "ibeam", "beam" }),
		new("NWPen",       "Role.NWPen",       new[] { "handwriting", "pen", "nwpen", "aero_pen" }),
		new("No",          "Role.No",          new[] { "unavailable", "no", "unavail", "aero_unavail" }),
		new("SizeNS",      "Role.SizeNS",      new[] { "vertical", "ns", "sizens", "vert", "aero_ns" }),
		new("SizeWE",      "Role.SizeWE",      new[] { "horizontal", "ew", "sizewe", "horz", "aero_ew" }),
		new("SizeNWSE",    "Role.SizeNWSE",    new[] { "diagonal1", "nwse", "sizenwse", "dgn1", "aero_nwse", "diagonal resize 1" }),
		new("SizeNESW",    "Role.SizeNESW",    new[] { "diagonal2", "nesw", "sizenesw", "dgn2", "aero_nesw", "diagonal resize 2" }),
		new("SizeAll",     "Role.SizeAll",     new[] { "move", "sizeall", "aero_move" }),
		new("UpArrow",     "Role.UpArrow",     new[] { "alternate", "up", "uparrow", "aero_up" }),
		new("Hand",        "Role.Hand",        new[] { "link", "hand", "aero_link" }),
		new("Person",      "Role.Person",      new[] { "person", "aero_person" }),
		new("Pin",         "Role.Pin",         new[] { "pin", "aero_pin", "location" }),
	};

	private static readonly char[] FileNameWordSeparators = { ' ', '-', '_', '.', '(', ')', '[', ']' };

	public static CursorRoleInfo? MatchByFileName(string filePath)
	{
		var stem = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();

		var exactMatch = All.FirstOrDefault(r => r.Aliases.Contains(stem));
		if (exactMatch != null)
			return exactMatch;

		var words = stem.Split(FileNameWordSeparators, StringSplitOptions.RemoveEmptyEntries);
		return All.FirstOrDefault(r => r.Aliases.Any(words.Contains));
	}
}
