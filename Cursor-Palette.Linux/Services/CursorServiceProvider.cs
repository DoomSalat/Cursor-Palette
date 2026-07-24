using CursorPalette.Services;

namespace CursorPalette.Linux.Services;

public static class CursorServiceProvider
{
	public static ICursorService Current { get; set; } = null!;
}
