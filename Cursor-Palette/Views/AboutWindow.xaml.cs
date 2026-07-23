using System.Reflection;
using System.Windows;

namespace CursorPalette.Views;

public partial class AboutWindow : Window
{
	public AboutWindow()
	{
		InitializeComponent();

		var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? AppInfo.DefaultVersion;
		VersionText.Text = $"{AppInfo.Author}  ·  v{version}  ·  {AppInfo.LicenseName}";
	}

	private void OnInfoButtonClick(object sender, RoutedEventArgs e)
	{
		new InfoHelpWindow(Loc.Get("S.Info.Title"), Services.HelpTextService.Get("About")) { Owner = this }.ShowDialog();
	}
}
