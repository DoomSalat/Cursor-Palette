using System.Diagnostics;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using CursorPalette.Linux.Services;
using CursorPalette.Services;

namespace CursorPalette.Linux.Views;

public class UpdateWindow : Window
{
	private const double DialogWidth = 420;
	private const double DialogHeight = 280;
	private const double DialogMinWidth = 360;
	private const double DialogMinHeight = 200;
	private const double DialogPadding = 20;
	private const double CloseButtonMinWidth = 90;
	private const double DownloadButtonMinWidth = 130;
	private const double ButtonPadding = 16;
	private const double HeaderFontSize = 15;
	private const double VersionInfoFontSize = 13;
	private const double StatusFontSize = 11;
	private const double HeaderIconFontSize = 16;

	private const string UserAgent = "Cursor-Palette-App";
	private const string DownloadsFolderName = "Downloads";
	private const string ArchiveFileNameFormat = "Cursor-Palette-v{0}.tar.gz";
	private const string VersionLabelFormat = "{0}: {1}  →  {2}: {3}";

	private const string LocWindowTitle = "S.Update.WindowTitle";
	private const string LocCurrentVersion = "S.Update.CurrentVersion";
	private const string LocNewVersion = "S.Update.NewVersion";
	private const string LocDownloading = "S.Update.Downloading";
	private const string LocDownloadedTo = "S.Update.DownloadedTo";
	private const string LocDownloadFailed = "S.Update.DownloadFailed";
	private const string LocUpdateCancel = "S.Update.Cancel";
	private const string LocUpdateManualDownload = "S.Update.ManualDownload";
	private const string LocToastManualDownload = "S.Toast.ManualDownload";

	private readonly UpdateInfo _updateInfo;
	private readonly Panel _toastHost;
	private readonly TextBlock _statusText;

	private static readonly HttpClient HttpClient = new();

	static UpdateWindow()
	{
		HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
	}

	public UpdateWindow(UpdateInfo updateInfo, Panel toastHost)
	{
		_updateInfo = updateInfo;
		_toastHost = toastHost;

		Title = Loc.Get(LocWindowTitle);
		Width = DialogWidth;
		Height = DialogHeight;
		MinWidth = DialogMinWidth;
		MinHeight = DialogMinHeight;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		CanResize = false;

		var root = new Grid();
		root.RowDefinitions.Add(new RowDefinition(0, GridUnitType.Auto));
		root.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
		root.RowDefinitions.Add(new RowDefinition(0, GridUnitType.Auto));

		var headerIcon = new TextBlock
		{
			Text = "⬇",
			FontSize = HeaderIconFontSize,
			Foreground = Brushes.CornflowerBlue,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0, 0, 8, 0),
		};

		var headerText = new TextBlock
		{
			Text = Loc.Get(LocWindowTitle),
			FontSize = HeaderFontSize,
			FontWeight = FontWeight.SemiBold,
			VerticalAlignment = VerticalAlignment.Center,
		};

		var headerPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
		};
		headerPanel.Children.Add(headerIcon);
		headerPanel.Children.Add(headerText);

		var topBar = new Border
		{
			BorderBrush = Brushes.DarkGray,
			BorderThickness = new Thickness(0, 0, 0, 1),
			Padding = new Thickness(DialogPadding, 10),
			Child = headerPanel,
		};
		Grid.SetRow(topBar, 0);
		root.Children.Add(topBar);

		var versionInfoText = new TextBlock
		{
			FontSize = VersionInfoFontSize,
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0, 0, 0, 20),
			Text = string.Format(VersionLabelFormat,
				Loc.Get(LocCurrentVersion), GetCurrentVersion(),
				Loc.Get(LocNewVersion), updateInfo.Version),
		};

		var downloadButton = new Button
		{
			Content = Loc.Get(LocUpdateManualDownload),
			MinWidth = DownloadButtonMinWidth,
			Padding = new Thickness(ButtonPadding, 10),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		downloadButton.Click += OnDownloadClick;

		_statusText = new TextBlock
		{
			FontSize = StatusFontSize,
			Foreground = Brushes.Gray,
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0, 16, 0, 0),
			IsVisible = false,
		};

		var centerPanel = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(DialogPadding),
		};
		centerPanel.Children.Add(versionInfoText);
		centerPanel.Children.Add(downloadButton);
		centerPanel.Children.Add(_statusText);
		Grid.SetRow(centerPanel, 1);
		root.Children.Add(centerPanel);

		var cancelButton = new Button
		{
			Content = Loc.Get(LocUpdateCancel),
			HorizontalAlignment = HorizontalAlignment.Right,
			MinWidth = CloseButtonMinWidth,
		};
		cancelButton.Click += (_, _) => Close();

		var bottomBar = new Border
		{
			BorderBrush = Brushes.DarkGray,
			BorderThickness = new Thickness(0, 1, 0, 0),
			Padding = new Thickness(DialogPadding, 10),
			Child = cancelButton,
		};
		Grid.SetRow(bottomBar, 2);
		root.Children.Add(bottomBar);

		Content = root;
	}

	private static string GetCurrentVersion() =>
		System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? AppInfo.DefaultVersion;

	private void OnDownloadClick(object? sender, RoutedEventArgs e)
	{
		_statusText.Text = Loc.Get(LocDownloading);
		_statusText.IsVisible = true;
		_ = DownloadAsync();
	}

	private async Task DownloadAsync()
	{
		try
		{
			var downloadsFolder = GetDownloadsFolder();
			var fileName = string.Format(ArchiveFileNameFormat, _updateInfo.Version);
			var destPath = Path.Combine(downloadsFolder, fileName);

			using var response = await HttpClient.GetAsync(_updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
			response.EnsureSuccessStatusCode();

			await using var fs = File.Create(destPath);
			await response.Content.CopyToAsync(fs);

			_statusText.Text = $"{Loc.Get(LocDownloadedTo)} {destPath}";
			ShowToast(Loc.Get(LocToastManualDownload));
		}
		catch (Exception ex)
		{
			_statusText.Text = $"{Loc.Get(LocDownloadFailed)}: {ex.Message}";
		}
	}

	private void ShowToast(string message)
	{
		var toast = new Border
		{
			Background = new SolidColorBrush(Colors.DarkSlateGray, 0.9),
			CornerRadius = new CornerRadius(6),
			Padding = new Thickness(12, 8),
			Margin = new Thickness(0, 0, 0, 8),
			Child = new TextBlock
			{
				Text = message,
				Foreground = Brushes.White,
				FontSize = 12,
			},
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Bottom,
		};

		_toastHost.Children.Add(toast);

		_ = Task.Delay(3000).ContinueWith(_ =>
		{
			Avalonia.Threading.Dispatcher.UIThread.Post(() => _toastHost.Children.Remove(toast));
		});
	}

	private static string GetDownloadsFolder()
	{
		var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		var path = Path.Combine(homeDir, DownloadsFolderName);
		return Directory.Exists(path) ? path : homeDir;
	}
}
