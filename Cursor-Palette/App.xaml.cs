using System.Windows;
using CursorPalette.Services;

namespace CursorPalette;

public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		AppPaths.EnsureCreated();

		DispatcherUnhandledException += (_, args) =>
		{
			MessageBox.Show(args.Exception.Message, Loc.Get(Constants.Strings.ErrorTitle),
				MessageBoxButton.OK, MessageBoxImage.Error);
			args.Handled = true;
		};
	}
}

public static class Loc
{
	public static string Get(string key) =>
		Application.Current.TryFindResource(key) as string ?? key;

	public static string Format(string key, params object[] args) =>
		string.Format(Get(key), args);
}
