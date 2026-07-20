using System.Reflection;
using System.Windows;

namespace CursorPalette.Views;

public partial class AboutWindow : Window
{
	public AboutWindow()
	{
		InitializeComponent();

		var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
		VersionText.Text = $"Capitan Salat  ·  v{version}  ·  MIT License";
	}
}
