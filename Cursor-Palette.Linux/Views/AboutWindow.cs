using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using CursorPalette.Services;

namespace CursorPalette.Linux.Views;

public class AboutWindow : Window
{
	private const double DialogWidth = 520;
	private const double DialogHeight = 460;
	private const double DialogMinWidth = 400;
	private const double DialogMinHeight = 320;
	private const double DialogPadding = 20;
	private const double CloseButtonMinWidth = 90;
	private const double TitleFontSize = 18;
	private const double VersionFontSize = 12;
	private const double LicenseHeaderFontSize = 13;
	private const double LicenseBodyFontSize = 12;
	private const double InfoButtonFontSize = 15;

	private const string LocAboutTitle = "S.About.Title";
	private const string LocAboutClose = "S.About.Close";
	private const string LocInfoTitle = "S.Info.Title";
	private const string LocInfoTooltip = "S.Info.Tooltip";

	private const string LicenseText = "Copyright (c) 2026 Capitan Salat\n\nPermission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the \"Software\"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:\n\nThe above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.\n\nTHE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.";

	public AboutWindow()
	{
		Title = Loc.Get(LocAboutTitle);
		Width = DialogWidth;
		Height = DialogHeight;
		MinWidth = DialogMinWidth;
		MinHeight = DialogMinHeight;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		CanResize = true;
		ShowInTaskbar = false;

		var root = new Grid();
		root.RowDefinitions.Add(new RowDefinition(0, GridUnitType.Auto));
		root.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
		root.RowDefinitions.Add(new RowDefinition(0, GridUnitType.Auto));

		var infoButton = new Button
		{
			Content = new TextBlock { Text = "ⓘ", FontSize = InfoButtonFontSize },
			Padding = new Thickness(8, 6),
			HorizontalAlignment = HorizontalAlignment.Right,
		};
		ToolTip.SetTip(infoButton, Loc.Get(LocInfoTooltip));
		infoButton.Click += OnInfoButtonClick;

		var topBar = new Border
		{
			BorderBrush = Brushes.DarkGray,
			BorderThickness = new Thickness(0, 0, 0, 1),
			Padding = new Thickness(DialogPadding, 8),
			Child = infoButton,
		};
		Grid.SetRow(topBar, 0);
		root.Children.Add(topBar);

		var contentPanel = new StackPanel
		{
			Spacing = 12,
		};

		var titlePanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(0, 0, 0, 4),
		};
		titlePanel.Children.Add(new TextBlock
		{
			Text = "Cursor ",
			FontSize = TitleFontSize,
			FontWeight = FontWeight.SemiBold,
			VerticalAlignment = VerticalAlignment.Center,
		});
		titlePanel.Children.Add(new TextBlock
		{
			Text = "Palette",
			FontSize = TitleFontSize,
			FontWeight = FontWeight.SemiBold,
			Foreground = Brushes.CornflowerBlue,
			VerticalAlignment = VerticalAlignment.Center,
		});
		contentPanel.Children.Add(titlePanel);

		var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? AppInfo.DefaultVersion;
		contentPanel.Children.Add(new TextBlock
		{
			Text = $"{AppInfo.Author}  ·  v{version}  ·  {AppInfo.LicenseName}",
			FontSize = VersionFontSize,
			Foreground = Brushes.Gray,
		});

		contentPanel.Children.Add(new TextBlock
		{
			Text = AppInfo.LicenseName,
			FontSize = LicenseHeaderFontSize,
			FontWeight = FontWeight.SemiBold,
		});

		contentPanel.Children.Add(new TextBlock
		{
			Text = LicenseText,
			FontSize = LicenseBodyFontSize,
			TextWrapping = TextWrapping.Wrap,
		});

		var scrollViewer = new ScrollViewer
		{
			Content = contentPanel,
			Padding = new Thickness(DialogPadding),
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
		};
		Grid.SetRow(scrollViewer, 1);
		root.Children.Add(scrollViewer);

		var closeButton = new Button
		{
			Content = Loc.Get(LocAboutClose),
			HorizontalAlignment = HorizontalAlignment.Right,
			MinWidth = CloseButtonMinWidth,
			Margin = new Thickness(DialogPadding, 10, DialogPadding, 10),
		};
		closeButton.Click += (_, _) => Close();
		Grid.SetRow(closeButton, 2);
		root.Children.Add(closeButton);

		Content = root;
	}

	private void OnInfoButtonClick(object? sender, RoutedEventArgs e)
	{
		new InfoHelpWindow(Loc.Get(LocInfoTitle), HelpTextService.Get("About")).ShowDialog(this);
	}
}
