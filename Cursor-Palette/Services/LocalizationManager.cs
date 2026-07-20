using System.Globalization;
using System.Windows;

namespace CursorPalette.Services;

public sealed record LanguageInfo(string Code, string DisplayName, string ResourcePath);

public static class LocalizationManager
{
	private const int StringsDictionaryIndex = 1;

	public static readonly LanguageInfo[] Available =
	{
		new("ru", "Русский", "Resources/Strings.ru.xaml"),
		new("en", "English", "Resources/Strings.en.xaml"),
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
