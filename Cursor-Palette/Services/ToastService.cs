using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CursorPalette.Services;

/// <summary>Shows a small toast that fades in near the top of a window and fades back out.</summary>
public static class ToastService
{
	private const double TopMargin = 14;
	private const double CornerRadius = 8;
	private const double BorderThicknessValue = 3;
	private static readonly TimeSpan FadeInDuration = TimeSpan.FromSeconds(0.25);
	private static readonly TimeSpan HoldDuration = TimeSpan.FromSeconds(2.5);
	private const double MaxToastWidth = 420;
	private static readonly TimeSpan FadeOutDuration = TimeSpan.FromSeconds(0.25);

	private static readonly Dictionary<Panel, (Border Border, Storyboard Storyboard)> Active = new();

	public static void Show(Panel host, string message)
	{
		if (Active.TryGetValue(host, out var current))
		{
			current.Storyboard.Stop();
			host.Children.Remove(current.Border);
			Active.Remove(host);
		}

		// NOTE: slide-down/up movement was tried here and didn't render — swap back in once fixed.
		// var transform = new TranslateTransform(0, -HiddenOffset);
		var border = new Border
		{
			Background = (Brush)Application.Current.Resources["Brush.Surface"],
			BorderBrush = (Brush)Application.Current.Resources["Brush.Accent"],
			BorderThickness = new Thickness(BorderThicknessValue),
			CornerRadius = new CornerRadius(CornerRadius),
			Padding = new Thickness(16, 10, 16, 10),
			Margin = new Thickness(0, TopMargin, 0, 0),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top,
			Opacity = 0,
			Child = new TextBlock
			{
				Text = message,
				Foreground = (Brush)Application.Current.Resources["Brush.Text"],
				FontSize = 13,
				FontWeight = FontWeights.Bold,
				TextAlignment = TextAlignment.Center,
				TextWrapping = TextWrapping.Wrap,
				MaxWidth = MaxToastWidth,
			},
		};
		Panel.SetZIndex(border, 2000);
		host.Children.Add(border);

		var appearAt = KeyTime.FromTimeSpan(FadeInDuration);
		var vanishAt = KeyTime.FromTimeSpan(FadeInDuration + HoldDuration);
		var endAt = KeyTime.FromTimeSpan(FadeInDuration + HoldDuration + FadeOutDuration);

		var fade = new DoubleAnimationUsingKeyFrames();
		fade.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
		fade.KeyFrames.Add(new EasingDoubleKeyFrame(1, appearAt));
		fade.KeyFrames.Add(new EasingDoubleKeyFrame(1, vanishAt));
		fade.KeyFrames.Add(new EasingDoubleKeyFrame(0, endAt));
		Storyboard.SetTarget(fade, border);
		Storyboard.SetTargetProperty(fade, new PropertyPath(UIElement.OpacityProperty));

		/*
		var slide = new DoubleAnimationUsingKeyFrames();
		slide.KeyFrames.Add(new EasingDoubleKeyFrame(-HiddenOffset, KeyTime.FromTimeSpan(TimeSpan.Zero)));
		slide.KeyFrames.Add(new EasingDoubleKeyFrame(0, appearAt,
			new BounceEase { Bounces = 2, Bounciness = 2, EasingMode = EasingMode.EaseOut }));
		slide.KeyFrames.Add(new EasingDoubleKeyFrame(0, vanishAt));
		slide.KeyFrames.Add(new EasingDoubleKeyFrame(-HiddenOffset, endAt, new CubicEase { EasingMode = EasingMode.EaseIn }));
		Storyboard.SetTarget(slide, transform);
		Storyboard.SetTargetProperty(slide, new PropertyPath(TranslateTransform.YProperty));
		*/

		var storyboard = new Storyboard();
		storyboard.Children.Add(fade);
		// storyboard.Children.Add(slide);
		storyboard.Completed += (_, _) =>
		{
			host.Children.Remove(border);
			Active.Remove(host);
		};

		Active[host] = (border, storyboard);
		storyboard.Begin();
	}
}
