namespace CursorPalette.Models;

public static class CursorDefaultsMap
{
	public static Dictionary<string, string> FallbackDefaults { get; } = new()
	{
		["Arrow"] = $@"{Constants.Cursor.SystemRootVar}\{Constants.Cursor.CursorsSubdir}\aero_arrow.cur",
		["Help"] = $@"{Constants.Cursor.SystemRootVar}\{Constants.Cursor.CursorsSubdir}\aero_helpsel.cur",
		["AppStarting"] = $@"{Constants.Cursor.SystemRootVar}\{Constants.Cursor.CursorsSubdir}\aero_working.ani",
		["Wait"] = $@"{Constants.Cursor.SystemRootVar}\{Constants.Cursor.CursorsSubdir}\aero_busy.ani",
		["Crosshair"] = "",
		["IBeam"] = "",
		["NWPen"] = $@"{Constants.Cursor.SystemRootVar}\{Constants.Cursor.CursorsSubdir}\aero_pen.cur",
		["No"] = $@"{Constants.Cursor.SystemRootVar}\{Constants.Cursor.CursorsSubdir}\aero_unavail.cur",
		["SizeNS"] = $@"{Constants.Cursor.SystemRootVar}\{Constants.Cursor.CursorsSubdir}\aero_ns.cur",
		["SizeWE"] = $@"{Constants.Cursor.SystemRootVar}\{Constants.Cursor.CursorsSubdir}\aero_ew.cur",
		["SizeNWSE"] = $@"{Constants.Cursor.SystemRootVar}\{Constants.Cursor.CursorsSubdir}\aero_nwse.cur",
		["SizeNESW"] = $@"{Constants.Cursor.SystemRootVar}\{Constants.Cursor.CursorsSubdir}\aero_nesw.cur",
		["SizeAll"] = $@"{Constants.Cursor.SystemRootVar}\{Constants.Cursor.CursorsSubdir}\aero_move.cur",
		["UpArrow"] = $@"{Constants.Cursor.SystemRootVar}\{Constants.Cursor.CursorsSubdir}\aero_up.cur",
		["Hand"] = $@"{Constants.Cursor.SystemRootVar}\{Constants.Cursor.CursorsSubdir}\aero_link.cur",
		["Person"] = $@"{Constants.Cursor.SystemRootVar}\{Constants.Cursor.CursorsSubdir}\aero_person.cur",
		["Pin"] = $@"{Constants.Cursor.SystemRootVar}\{Constants.Cursor.CursorsSubdir}\aero_pin.cur",
	};
}
