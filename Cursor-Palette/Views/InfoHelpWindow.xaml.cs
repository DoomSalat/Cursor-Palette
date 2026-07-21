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

	public InfoHelpWindow(string title, string body)
	{
		InitializeComponent();

		var uiScale = Services.AppState.GetUiScale();
		LayoutTransform = new ScaleTransform(uiScale, uiScale);

		Title = title;
		TitleText.Text = title;
		BuildBody(body);
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

			if (i == 0 && HasEmoji(para[0]))
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
			FontSize = 16,
			FontWeight = FontWeights.SemiBold,
			TextWrapping = TextWrapping.Wrap,
			Foreground = BrushAccent,
			Margin = new Thickness(0, 0, 0, 4),
		});

		BodyPanel.Children.Add(new Border
		{
			Height = 1,
			Background = BrushBorder,
			Margin = new Thickness(0, 0, 0, 12),
		});
	}

	private void RenderStandalone(string text)
	{
		BodyPanel.Children.Add(new TextBlock
		{
			Text = text,
			FontSize = 13,
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
			Margin = new Thickness(0, 0, 0, 8),
		};

		var sp = new StackPanel();

		var header = para[0];
		if (!string.IsNullOrEmpty(header))
		{
			sp.Children.Add(new TextBlock
			{
				Text = header,
				FontSize = 14,
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
					FontSize = 13,
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
			Padding = new Thickness(14, 8, 14, 8),
			Margin = new Thickness(0, 0, 0, 8),
		};

		var sp = new StackPanel();

		foreach (var line in para)
		{
			sp.Children.Add(new TextBlock
			{
				Text = line,
				FontSize = 13,
				TextWrapping = TextWrapping.Wrap,
				Foreground = BrushText,
				Margin = new Thickness(0, 3, 0, 3),
			});
		}

		card.Child = sp;
		BodyPanel.Children.Add(card);
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
		return c == '\u2196' || c == '\u2197' || c == '\u2199' || c == '\u2198' ||
			   c == '\u2191' || c == '\u2193' || c == '\u2190' || c == '\u2192';
	}

	private static bool HasEmoji(string text)
	{
		if (string.IsNullOrEmpty(text))
			return false;

		var c = text[0];
		return c >= 0x1F000 || c == '\u2139' || c == '\u2611' || c == '\u21A9' ||
			   c == '\u2196' || c == '\u2197' || c == '\u2199' || c == '\u2198' ||
			   c == '\u2191' || c == '\u2193' || c == '\u2190' || c == '\u2192' ||
			   c == '\u2022' || c == '\u2713' || c == '\u2715' || c == '\u26A1' ||
			   text.StartsWith("\U0001F3AF");
	}
}
