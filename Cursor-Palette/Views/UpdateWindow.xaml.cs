using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class UpdateWindow : Window
{
	private const string UserAgent = "Cursor-Palette-App";
	private const string ExplorerExe = "explorer.exe";
	private const string DownloadsFolderName = "Downloads";
	private const string FileNameFormat = "Cursor-Palette-v{0}.exe";
	private const string TempFileFormat = "cursor-palette-update-{0}.exe";
	private const string TempBatFormat = "cursor-palette-update-{0}.bat";
	private const string ExplorerSelectArgs = "/select,\"{0}\"";
	private const string LocWindowTitle = "S.Update.WindowTitle";
	private const string LocCurrentVersion = "S.Update.CurrentVersion";
	private const string LocNewVersion = "S.Update.NewVersion";
	private const string LocDownloading = "S.Update.Downloading";
	private const string LocDownloadedTo = "S.Update.DownloadedTo";
	private const string LocDownloadFailed = "S.Update.DownloadFailed";
	private const string LocToastManualDownload = "S.Toast.ManualDownload";
	private const string LocToastDownloadStarted = "S.Toast.DownloadStarted";
	private const string VersionLabelFormat = "{0}: {1}  →  {2}: {3}";
	private const string DownloadedToFormat = "{0} {1}";
	private const string DownloadFailedFormat = "{0}: {1}";

	private readonly UpdateInfo _updateInfo;
	private readonly Panel _toastHost;
	private static readonly HttpClient HttpClient = new();

	static UpdateWindow()
	{
		HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
	}

	public UpdateWindow(UpdateInfo updateInfo, Panel toastHost)
	{
		InitializeComponent();
		_updateInfo = updateInfo;
		_toastHost = toastHost;

		HeaderText.Text = Loc.Get(LocWindowTitle);
		VersionInfoText.Text = string.Format(VersionLabelFormat,
			Loc.Get(LocCurrentVersion), GetCurrentVersion(),
			Loc.Get(LocNewVersion), updateInfo.Version);
	}

	private static string GetCurrentVersion() =>
		System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? AppInfo.DefaultVersion;

	private void OnManualDownloadClick(object sender, RoutedEventArgs e)
	{
		ToastService.Show(_toastHost, Loc.Get(LocToastDownloadStarted));
		Close();
		_ = DownloadManualAsync();
	}

	private async Task DownloadManualAsync()
	{
		try
		{
			var downloadsFolder = GetDownloadsFolder();
			var fileName = string.Format(FileNameFormat, _updateInfo.Version);
			var destPath = Path.Combine(downloadsFolder, fileName);

			using var response = await HttpClient.GetAsync(_updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
			response.EnsureSuccessStatusCode();

			await using var fs = File.Create(destPath);
			await response.Content.CopyToAsync(fs);

			ToastService.Show(_toastHost, Loc.Get(LocToastManualDownload));

			Process.Start(new ProcessStartInfo
			{
				FileName = ExplorerExe,
				Arguments = string.Format(ExplorerSelectArgs, destPath),
				UseShellExecute = true,
			});
		}
		catch
		{
		}
	}

	private async void OnAutoUpdateClick(object sender, RoutedEventArgs e)
	{
		StatusText.Text = Loc.Get(LocDownloading);
		StatusText.Visibility = Visibility.Visible;

		try
		{
			var tempPath = Path.Combine(Path.GetTempPath(), string.Format(TempFileFormat, _updateInfo.Version));

			using var response = await HttpClient.GetAsync(_updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
			response.EnsureSuccessStatusCode();

			await using var fs = File.Create(tempPath);
			await response.Content.CopyToAsync(fs);

			var currentExe = Environment.ProcessPath!;
			var batPath = Path.Combine(Path.GetTempPath(), string.Format(TempBatFormat, Guid.NewGuid().ToString("N")));

			var batContent = $"""
				@echo off
				:wait
				tasklist /fi "pid eq {Environment.ProcessId}" 2>nul | find "{Environment.ProcessId}" >nul
				if not errorlevel 1 (
					timeout /t 1 /nobreak >nul
					goto wait
				)
				copy /y "{tempPath}" "{currentExe}"
				del "{tempPath}"
				start "" "{currentExe}"
				del "%~f0"
				""";

			await File.WriteAllTextAsync(batPath, batContent);

			Process.Start(new ProcessStartInfo
			{
				FileName = batPath,
				CreateNoWindow = true,
				UseShellExecute = false,
			});

			Application.Current.Shutdown();
		}
		catch (Exception ex)
		{
			StatusText.Text = string.Format(DownloadFailedFormat, Loc.Get(LocDownloadFailed), ex.Message);
		}
	}

	private void OnCancelClick(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private static string GetDownloadsFolder()
	{
		var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DownloadsFolderName);
		return Directory.Exists(path) ? path : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
	}
}
