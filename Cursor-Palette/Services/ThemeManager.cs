using System.Windows;
using Microsoft.Win32;

namespace CursorPalette.Services;

/// <summary>
/// Переключение темы оформления. При первом запуске читает системную тему
/// Windows (HKCU\...\Personalize\AppsUseLightTheme) и запоминает выбор —
/// дальше системные изменения темы не отслеживаются, только явный тумблер.
/// </summary>
public static class ThemeManager
{
	public const string Dark = "Dark";
	public const string Light = "Light";

	private const string SystemPersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
	private const string AppsUseLightThemeValueName = "AppsUseLightTheme";

	// Тема — это MergedDictionaries[0] в App.xaml; строки — [1]. Индекс
	// фиксирован в App.xaml и не должен меняться порядком дальнейших правок.
	private const int ThemeDictionaryIndex = 0;

	public static string Current { get; private set; } = Dark;

	public static void Initialize()
	{
		var saved = AppState.GetThemeMode();

		if (saved is Dark or Light)
		{
			Current = saved;
		}
		else
		{
			Current = DetectSystemTheme();
			AppState.SetThemeMode(Current);
		}

		Apply(Current);
	}

	public static void SetTheme(string mode)
	{
		Current = mode;
		AppState.SetThemeMode(Current);
		Apply(Current);
	}

	private static void Apply(string mode)
	{
		var dictionary = new ResourceDictionary
		{
			Source = new Uri($"Themes/{mode}.xaml", UriKind.Relative),
		};
		Application.Current.Resources.MergedDictionaries[ThemeDictionaryIndex] = dictionary;
	}

	private static string DetectSystemTheme()
	{
		try
		{
			using var key = Registry.CurrentUser.OpenSubKey(SystemPersonalizeKeyPath);
			if (key?.GetValue(AppsUseLightThemeValueName) is int useLight)
				return useLight == 0 ? Dark : Light;
		}
		catch
		{
			// нет доступа/ключа — остаёмся на тёмной по умолчанию
		}

		return Dark;
	}
}
