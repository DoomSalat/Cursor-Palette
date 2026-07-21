using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CursorPalette.Views;

public partial class InfoHelpWindow : Window
{
	private static readonly Brush BrushTextDim = (Brush)Application.Current.Resources["Brush.TextDim"];
	private static readonly Brush BrushAccent = (Brush)Application.Current.Resources["Brush.Accent"];
	private static readonly Brush BrushText = (Brush)Application.Current.Resources["Brush.Text"];
	private static readonly Brush BrushSurface = (Brush)Application.Current.Resources["Brush.Surface"];
	private static readonly Brush BrushBorder = (Brush)Application.Current.Resources["Brush.Border"];

	private const double TextZoomStep = 0.1;
	private const int IconTokenMaxLength = 8;

	private readonly string _body;
	private double _textScale = Services.AppState.InfoTextScaleDefault;

	public InfoHelpWindow(string title, string body)
	{
		InitializeComponent();

		var uiScale = Services.AppState.GetUiScale();
		LayoutTransform = new ScaleTransform(uiScale, uiScale);

		_body = body;
		_textScale = Services.AppState.GetInfoTextScale();

		Title = title;
		TitleText.Text = title;

		ApplyTextZoom();
	}

	private double Fs(double baseSize) => Math.Round(baseSize * _textScale, 1);

	private void OnTextZoomOutClick(object sender, RoutedEventArgs e) => AdjustTextZoom(-TextZoomStep);
	private void OnTextZoomInClick(object sender, RoutedEventArgs e) => AdjustTextZoom(TextZoomStep);

	private void AdjustTextZoom(double delta)
	{
		_textScale = Math.Clamp(Math.Round(_textScale + delta, 2),
			Services.AppState.InfoTextScaleMin, Services.AppState.InfoTextScaleMax);
		Services.AppState.SetInfoTextScale(_textScale);
		ApplyTextZoom();
	}

	private void ApplyTextZoom()
	{
		TextZoomText.Text = $"{(int)Math.Round(_textScale * 100)}%";
		BodyPanel.Children.Clear();
		BuildBody(_body);
	}

	private void BuildBody(string body)
	{
		var lines = body.Split('\n');
		var paragraphs = new List<List<string>>();
		var current = new List<string>();

		foreach (var rawLine in lines)
		{
			var line = rawLine.TrimEnd();
			if (string.IsNullOrWhiteSpace(line))
			{
				if (current.Count > 0)
				{
					paragraphs.Add(current);
					current = new List<string>();
				}
			}
			else
			{
				current.Add(line.TrimStart());
			}
		}
		if (current.Count > 0)
			paragraphs.Add(current);

		for (int i = 0; i < paragraphs.Count; i++)
		{
			var para = paragraphs[i];

			if (i == 0)
			{
				RenderTitle(para[0]);
				continue;
			}

			var hasBullets = para.Any(l => l.StartsWith("•"));
			var hasArrows = para.Any(l => IsArrowLine(l));

			if (hasBullets || hasArrows)
				RenderSectionCard(para);
			else if (para.Count == 1)
				RenderStandalone(para[0]);
			else
				RenderTipsCard(para);
		}
	}

	private void RenderTitle(string text)
	{
		BodyPanel.Children.Add(new TextBlock
		{
			Text = text,
			FontSize = Fs(16),
			FontWeight = FontWeights.SemiBold,
			TextWrapping = TextWrapping.Wrap,
			Foreground = BrushAccent,
			Margin = new Thickness(0, 0, 0, 4),
		});

		BodyPanel.Children.Add(new Border
		{
			Height = 1,
			Background = BrushBorder,
			Margin = new Thickness(0, 0, 0, 14),
		});
	}

	private void RenderStandalone(string text)
	{
		BodyPanel.Children.Add(new TextBlock
		{
			Text = text,
			FontSize = Fs(13),
			TextWrapping = TextWrapping.Wrap,
			Foreground = BrushText,
			Margin = new Thickness(0, 4, 0, 4),
		});
	}

	private void RenderSectionCard(List<string> para)
	{
		var card = new Border
		{
			Background = BrushSurface,
			BorderBrush = BrushBorder,
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(8),
			Padding = new Thickness(14, 10, 14, 10),
			Margin = new Thickness(0, 0, 0, 10),
		};

		var sp = new StackPanel();

		var header = para[0];
		if (!string.IsNullOrEmpty(header))
		{
			sp.Children.Add(new TextBlock
			{
				Text = header,
				FontSize = Fs(14),
				FontWeight = FontWeights.SemiBold,
				TextWrapping = TextWrapping.Wrap,
				Foreground = BrushText,
				Margin = new Thickness(0, 0, 0, 6),
			});
		}

		var arrowBlock = new List<string>();

		foreach (var line in para.Skip(1))
		{
			if (IsArrowLine(line))
			{
				arrowBlock.Add(line);
				continue;
			}

			if (arrowBlock.Count > 0)
			{
				sp.Children.Add(BuildArrowGrid(arrowBlock));
				arrowBlock.Clear();
			}

			if (line.StartsWith("•"))
			{
				sp.Children.Add(BuildBulletItem(line));
			}
			else
			{
				sp.Children.Add(new TextBlock
				{
					Text = line,
					FontSize = Fs(13),
					TextWrapping = TextWrapping.Wrap,
					Foreground = BrushTextDim,
					Margin = new Thickness(0, 2, 0, 2),
				});
			}
		}

		if (arrowBlock.Count > 0)
			sp.Children.Add(BuildArrowGrid(arrowBlock));

		card.Child = sp;
		BodyPanel.Children.Add(card);
	}

	private void RenderTipsCard(List<string> para)
	{
		var card = new Border
		{
			Background = BrushSurface,
			BorderBrush = BrushBorder,
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(8),
			Padding = new Thickness(14, 10, 14, 10),
			Margin = new Thickness(0, 0, 0, 10),
		};

		var sp = new StackPanel();

		for (int i = 0; i < para.Count; i++)
		{
			var line = para[i];
			var (icon, text) = SplitLeadingIcon(line);

			sp.Children.Add(icon is null
				? new TextBlock
				{
					Text = line,
					FontSize = Fs(13),
					TextWrapping = TextWrapping.Wrap,
					Foreground = BrushText,
					Margin = new Thickness(0, 4, 0, 4),
				}
				: BuildIconRow(icon, text!));

			if (i < para.Count - 1)
			{
				sp.Children.Add(new Border
				{
					Height = 1,
					Background = BrushBorder,
					Opacity = 0.6,
					Margin = new Thickness(0, 6, 0, 6),
				});
			}
		}

		card.Child = sp;
		BodyPanel.Children.Add(card);
	}

	private Grid BuildIconRow(string icon, string text)
	{
		var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 24 });
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

		var iconBlock = new TextBlock
		{
			Text = icon,
			FontSize = Fs(13),
			Foreground = BrushText,
			Margin = new Thickness(0, 0, 8, 0),
			VerticalAlignment = VerticalAlignment.Top,
		};
		Grid.SetColumn(iconBlock, 0);

		var textBlock = new TextBlock
		{
			Text = text,
			FontSize = Fs(13),
			TextWrapping = TextWrapping.Wrap,
			Foreground = BrushText,
			VerticalAlignment = VerticalAlignment.Top,
		};
		Grid.SetColumn(textBlock, 1);

		grid.Children.Add(iconBlock);
		grid.Children.Add(textBlock);
		return grid;
	}

	private static (string? icon, string? rest) SplitLeadingIcon(string line)
	{
		var spaceIndex = line.IndexOf(' ');
		if (spaceIndex <= 0 || spaceIndex > IconTokenMaxLength)
			return (null, null);

		var icon = line[..spaceIndex];
		var rest = line[(spaceIndex + 1)..].TrimStart();
		if (string.IsNullOrEmpty(rest) || char.IsLetterOrDigit(icon[0]))
			return (null, null);

		return (icon, rest);
	}

	private static StackPanel BuildBulletItem(string line)
	{
		var row = new StackPanel { Orientation = Orientation.Horizontal };
		row.Children.Add(new TextBlock
		{
			Text = "•",
			FontSize = 13,
			FontWeight = FontWeights.Bold,
			Foreground = BrushAccent,
			Margin = new Thickness(0, 0, 8, 0),
			VerticalAlignment = VerticalAlignment.Top,
		});
		row.Children.Add(new TextBlock
		{
			Text = line.TrimStart('•', ' '),
			FontSize = 13,
			TextWrapping = TextWrapping.Wrap,
			Foreground = BrushTextDim,
			VerticalAlignment = VerticalAlignment.Top,
		});
		return row;
	}

	private static StackPanel BuildArrowGrid(List<string> arrows)
	{
		var sp = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
		foreach (var arrow in arrows)
		{
			sp.Children.Add(new TextBlock
			{
				Text = arrow,
				FontSize = 13,
				FontFamily = new FontFamily("Consolas, Courier New, monospace"),
				Foreground = BrushTextDim,
			});
		}
		return sp;
	}

	private static bool IsArrowLine(string text)
	{
		if (string.IsNullOrEmpty(text))
			return false;
		var c = text[0];
		return c == '↖' || c == '↗' || c == '↙' || c == '↘' ||
			   c == '↑' || c == '↓' || c == '←' || c == '→';
	}
}
