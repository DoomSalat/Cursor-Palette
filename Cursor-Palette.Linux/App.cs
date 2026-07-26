using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CursorPalette.Linux.Services;
using CursorPalette.Linux.Views;
using CursorPalette.Services;

namespace CursorPalette.Linux;

public partial class App : Avalonia.Application
{
	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		PlatformBootstrapper.Initialize();

		if (SingleInstanceProvider.Current is { } singleInstance)
		{
			if (!singleInstance.TryAcquire())
			{
				singleInstance.NotifyExistingInstance();
				if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
					desktopLifetime.Shutdown();
				return;
			}
		}

		ThemeManager.Initialize();

		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.MainWindow = new MainWindow
			{
				Title = AppInfo.Name,
				Width = 860,
				Height = 640,
			};
		}

		base.OnFrameworkInitializationCompleted();
	}
}
