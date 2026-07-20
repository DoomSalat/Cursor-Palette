using System.Globalization;
using System.Windows;

namespace CursorPalette.Services;

public sealed record LanguageInfo(string Code, string DisplayName, string ResourcePath);

public static class LocalizationManager
{
	private const int StringsDictionaryIndex = 1;

	public static readonly LanguageInfo[] Available =
	{
		new("ru", "Русский", "Resources/Localization/Strings.ru.xaml"),
		new("en", "English", "Resources/Localization/Strings.en.xaml"),
		new("zh", "简体中文", "Resources/Localization/Strings.zh.xaml"),
		new("ja", "日本語", "Resources/Localization/Strings.ja.xaml"),
		new("es", "Español", "Resources/Localization/Strings.es.xaml"),
		new("de", "Deutsch", "Resources/Localization/Strings.de.xaml"),
	};

	private const string FallbackCode = "ru";

	public static string Current { get; private set; } = FallbackCode;

	public static void Initialize()
	{
		var saved = AppState.GetLanguage();

		if (saved != null && Available.Any(l => l.Code == saved))
		{
			Current = saved;
		}
		else
		{
			Current = DetectSystemLanguage();
			AppState.SetLanguage(Current);
		}

		Apply(Current);
	}

	public static void SetLanguage(string code)
	{
		if (code == Current || Available.All(l => l.Code != code))
			return;

		Current = code;
		AppState.SetLanguage(Current);

		Apply(Current);
	}

	private static void Apply(string code)
	{
		var language = Available.First(l => l.Code == code);
		var dictionary = new ResourceDictionary
		{
			Source = new Uri(language.ResourcePath, UriKind.Relative),
		};
		Application.Current.Resources.MergedDictionaries[StringsDictionaryIndex] = dictionary;
	}

	private static string DetectSystemLanguage()
	{
		var systemCode = CultureInfo.InstalledUICulture.TwoLetterISOLanguageName;

		return Available.Any(l => l.Code == systemCode) ? systemCode : FallbackCode;
	}
}
