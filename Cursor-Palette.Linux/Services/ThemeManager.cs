using Avalonia;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Styling;
using CursorPalette.Services;

namespace CursorPalette.Linux.Services;

public static class ThemeManager
{
	public static void BindResource(AvaloniaObject target, AvaloniaProperty property, string resourceKey) =>
		target.Bind(property, new DynamicResourceExtension(resourceKey));

	public const string Dark = "Dark";
	public const string Light = "Light";

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

	public static void Toggle()
	{
		SetTheme(Current == Dark ? Light : Dark);
	}

	private static void Apply(string mode)
	{
		if (Application.Current == null)
			return;

		Application.Current.RequestedThemeVariant = mode == Dark
			? ThemeVariant.Dark
			: ThemeVariant.Light;
	}

	private static string DetectSystemTheme()
	{
		return Dark;
	}
}
