using System.Windows;
using CursorPalette.Services;
using CursorPalette.Views;

namespace CursorPalette;

public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		if (!SingleInstanceService.TryAcquire())
		{
			SingleInstanceService.NotifyExistingInstance();
			Shutdown();
			return;
		}

		AppPaths.EnsureCreated();

		SubscribeToUnhandledExceptions();
		ApplySavedAppearance();

		var window = new MainWindow();
		MainWindow = window;
		window.Show();
	}

	private void SubscribeToUnhandledExceptions()
	{
		DispatcherUnhandledException += (_, args) =>
		{
			MessageBox.Show(args.Exception.Message, Loc.Get("S.Error.Title"),
				MessageBoxButton.OK, MessageBoxImage.Error);
			args.Handled = true;
		};
	}

	private static void ApplySavedAppearance()
	{
		ThemeManager.Initialize();
		LocalizationManager.Initialize();
	}
}

public static class Loc
{
	public static string Get(string key) =>
		Application.Current.TryFindResource(key) as string ?? key;

	public static string Format(string key, params object[] args) =>
		string.Format(Get(key), args);
}
