using System.Globalization;

namespace CursorPalette.Services;

public sealed record LanguageInfo(string Code, string DisplayName);

public static class LocalizationManager
{
	public static readonly LanguageInfo[] Available =
	{
		new("ru", "Русский"),
		new("en", "English"),
		new("zh", "简体中文"),
		new("ja", "日本語"),
		new("es", "Español"),
		new("de", "Deutsch"),
	};

	private const string FallbackCode = "ru";

	public static string Current { get; private set; } = FallbackCode;

	public static event Action? LanguageChanged;

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

		Loc.Initialize();
	}

	public static void SetLanguage(string code)
	{
		if (code == Current || Available.All(l => l.Code != code))
			return;

		Current = code;
		AppState.SetLanguage(Current);

		LanguageChanged?.Invoke();
		Loc.Reload();
	}

	private static string DetectSystemLanguage()
	{
		var systemCode = CultureInfo.InstalledUICulture.TwoLetterISOLanguageName;

		return Available.Any(l => l.Code == systemCode) ? systemCode : FallbackCode;
	}
}
