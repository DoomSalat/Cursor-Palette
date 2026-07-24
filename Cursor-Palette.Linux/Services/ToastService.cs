using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace CursorPalette.Linux.Services;

public static class ToastService
{
	private const double TopMargin = 14;
	private const double CornerRadius = 8;
	private const double BorderThicknessValue = 3;
	private const double MaxToastWidth = 420;
	private const double HiddenOffset = 32;
	private const double FontSizeValue = 13;
	private const int ZIndexValue = 2000;

	private static readonly TimeSpan FadeInDuration = TimeSpan.FromSeconds(0.25);
	private static readonly TimeSpan HoldDuration = TimeSpan.FromSeconds(2.5);
	private static readonly TimeSpan FadeOutDuration = TimeSpan.FromSeconds(0.25);

	private static readonly Dictionary<Panel, (Border Border, DispatcherTimer Timer)> Active = new();

	public static void Show(Panel host, string message)
	{
		if (Active.TryGetValue(host, out var current))
		{
			current.Timer.Stop();
			host.Children.Remove(current.Border);
			Active.Remove(host);
		}

		var border = new Border
		{
			Background = Brushes.DimGray,
			BorderBrush = Brushes.CornflowerBlue,
			BorderThickness = new Avalonia.Thickness(BorderThicknessValue),
			CornerRadius = new Avalonia.CornerRadius(CornerRadius),
			Padding = new Avalonia.Thickness(16, 10),
			Margin = new Avalonia.Thickness(0, TopMargin, 0, 0),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top,
			Opacity = 0,
			Child = new TextBlock
			{
				Text = message,
				Foreground = Brushes.White,
				FontSize = FontSizeValue,
				FontWeight = FontWeight.Bold,
				TextAlignment = TextAlignment.Center,
				TextWrapping = TextWrapping.Wrap,
				MaxWidth = MaxToastWidth,
			},
		};

		border.ZIndex = ZIndexValue;
		host.Children.Add(border);

		AnimateToast(border);

		var timer = new DispatcherTimer { Interval = FadeInDuration + HoldDuration + FadeOutDuration };
		timer.Tick += (_, _) =>
		{
			timer.Stop();
			host.Children.Remove(border);
			Active.Remove(host);
		};

		Active[host] = (border, timer);
		timer.Start();
	}

	private static void AnimateToast(Border border)
	{
		var fadeInEnd = FadeInDuration.TotalMilliseconds;
		var holdEnd = (FadeInDuration + HoldDuration).TotalMilliseconds;
		var fadeOutEnd = (FadeInDuration + HoldDuration + FadeOutDuration).TotalMilliseconds;

		var totalMs = fadeOutEnd;
		var elapsed = 0d;
		var stepMs = 16d;

		var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(stepMs) };

		timer.Tick += (_, _) =>
		{
			elapsed += stepMs;

			double opacity;
			double offsetY;

			if (elapsed < fadeInEnd)
			{
				var progress = elapsed / fadeInEnd;
				opacity = progress;
				offsetY = -HiddenOffset * (1 - progress);
			}
			else if (elapsed < holdEnd)
			{
				opacity = 1;
				offsetY = 0;
			}
			else if (elapsed < fadeOutEnd)
			{
				var progress = (elapsed - holdEnd) / FadeOutDuration.TotalMilliseconds;
				opacity = 1 - progress;
				offsetY = -HiddenOffset * progress;
			}
			else
			{
				timer.Stop();
				return;
			}

			border.Opacity = opacity;
			border.RenderTransform = new TranslateTransform(0, offsetY);
		};

		timer.Start();
	}
}
