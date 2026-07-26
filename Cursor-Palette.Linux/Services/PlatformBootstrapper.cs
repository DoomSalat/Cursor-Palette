using CursorPalette.Services;

namespace CursorPalette.Linux.Services;

public static class PlatformBootstrapper
{
	public static void Initialize()
	{
		var paths = new LinuxPaths();
		paths.EnsureCreated();

		PathProvider.Current = paths;
		AssetLoaderProvider.Current = new LinuxAssetLoader();
		CursorServiceProvider.Current = new LinuxCursorService();
		ScreenColorPickerProvider.Current = new LinuxScreenColorPicker();
		SingleInstanceProvider.Current = new LinuxSingleInstance();
		FileExplorerProvider.Current = new LinuxFileExplorer();

		LocalizationManager.Initialize();
	}
}
