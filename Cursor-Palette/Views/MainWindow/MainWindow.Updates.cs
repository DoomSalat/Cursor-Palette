using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class MainWindow
{
	private UpdateInfo? _updateInfo;

	private async Task CheckForUpdatesAsync(string currentVersion)
	{
		((Storyboard)Resources[UpdateSpinnerStoryboardKey]).Begin(this, true);

		_updateInfo = await UpdateChecker.GetLatestReleaseInfoAsync();

		((Storyboard)Resources[UpdateSpinnerStoryboardKey]).Stop(this);
		UpdateSpinner.Visibility = Visibility.Collapsed;
		UpdateCheckingLabel.Visibility = Visibility.Collapsed;

		if (_updateInfo is null)
			return;

		if (!Version.TryParse(_updateInfo.Version, out var latestVersion))
			return;

		if (!Version.TryParse(currentVersion, out var currentVer))
			return;

		if (latestVersion > currentVer)
		{
			UpdateIndicator.Visibility = Visibility.Visible;
			ToastService.Show(RootGrid, Loc.Get(LocToastUpdateAvailable));
		}
		else
			UpToDateLabel.Visibility = Visibility.Visible;
	}

	private void OnUpdateIndicatorClick(object sender, RoutedEventArgs e)
	{
		if (_updateInfo is null)
			return;

		new UpdateWindow(_updateInfo, RootGrid) { Owner = this }.ShowDialog();
	}

	private void OnUpToDateLabelClick(object sender, MouseButtonEventArgs e)
	{
		var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? AppInfo.DefaultVersion;

		UpToDateLabel.Visibility = Visibility.Collapsed;
		UpdateCheckingLabel.Visibility = Visibility.Visible;

		_ = CheckForUpdatesAsync(version);
	}

	private void OnFooterClick(object sender, RoutedEventArgs e)
	{
		new AboutWindow { Owner = this }.ShowDialog();
	}

	private void OnGitHubIconClick(object sender, MouseButtonEventArgs e)
	{
		System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
		{
			FileName = AppInfo.GitHubUrl,
			UseShellExecute = true,
		});
	}

	private void OnInfoButtonClick(object sender, RoutedEventArgs e)
	{
		new InfoHelpWindow(Loc.Get(LocInfoTitle), Services.HelpTextService.Get("Main")) { Owner = this }.ShowDialog();
	}

	private void OnAppLogoClick(object sender, MouseButtonEventArgs e)
	{
		ApplyRandomFromBoard();
	}

	private void OnOpenFolderToggleClick(object sender, RoutedEventArgs e)
	{
		AppState.SetOpenFolderAfterDownload(!AppState.GetOpenFolderAfterDownload());
		UpdateOpenFolderToggleIcon();
	}

	private void UpdateOpenFolderToggleIcon()
	{
		var brushKey = AppState.GetOpenFolderAfterDownload() ? "Brush.Accent" : "Brush.TextDim";
		OpenFolderIcon.Fill = (Brush)Application.Current.Resources[brushKey];
	}
}
