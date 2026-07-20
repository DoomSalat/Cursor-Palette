namespace CursorPalette.Models;

public static class CursorDefaultsMap
{
	private const string SystemRootVar = "%SystemRoot%";
	private const string CursorsSubdir = "cursors";

	public static Dictionary<string, string> FallbackDefaults { get; } = new()
	{
		["Arrow"] = $@"{SystemRootVar}\{CursorsSubdir}\aero_arrow.cur",
		["Help"] = $@"{SystemRootVar}\{CursorsSubdir}\aero_helpsel.cur",
		["AppStarting"] = $@"{SystemRootVar}\{CursorsSubdir}\aero_working.ani",
		["Wait"] = $@"{SystemRootVar}\{CursorsSubdir}\aero_busy.ani",
		["Crosshair"] = "",
		["IBeam"] = "",
		["NWPen"] = $@"{SystemRootVar}\{CursorsSubdir}\aero_pen.cur",
		["No"] = $@"{SystemRootVar}\{CursorsSubdir}\aero_unavail.cur",
		["SizeNS"] = $@"{SystemRootVar}\{CursorsSubdir}\aero_ns.cur",
		["SizeWE"] = $@"{SystemRootVar}\{CursorsSubdir}\aero_ew.cur",
		["SizeNWSE"] = $@"{SystemRootVar}\{CursorsSubdir}\aero_nwse.cur",
		["SizeNESW"] = $@"{SystemRootVar}\{CursorsSubdir}\aero_nesw.cur",
		["SizeAll"] = $@"{SystemRootVar}\{CursorsSubdir}\aero_move.cur",
		["UpArrow"] = $@"{SystemRootVar}\{CursorsSubdir}\aero_up.cur",
		["Hand"] = $@"{SystemRootVar}\{CursorsSubdir}\aero_link.cur",
		["Person"] = $@"{SystemRootVar}\{CursorsSubdir}\aero_person.cur",
		["Pin"] = $@"{SystemRootVar}\{CursorsSubdir}\aero_pin.cur",
	};
}
