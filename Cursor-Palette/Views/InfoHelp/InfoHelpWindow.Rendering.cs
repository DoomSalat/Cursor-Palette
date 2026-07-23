using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CursorPalette.Views;

public partial class InfoHelpWindow : Window
{
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

		var stackPanel = new StackPanel();

		var header = para[0];

		if (header.StartsWith("## "))
			header = header[3..];

		if (!string.IsNullOrEmpty(header))
		{
			stackPanel.Children.Add(new TextBlock
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
				stackPanel.Children.Add(BuildArrowGrid(arrowBlock));
				arrowBlock.Clear();
			}

			if (line.StartsWith("- "))
			{
				stackPanel.Children.Add(BuildBulletItem(line));
			}
			else
			{
				var imageRow = TryBuildImageRow(line, BrushTextDim, 0.5);
				if (imageRow != null)
					stackPanel.Children.Add(imageRow);
				else
					stackPanel.Children.Add(new TextBlock
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
			stackPanel.Children.Add(BuildArrowGrid(arrowBlock));

		card.Child = stackPanel;
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

		var stackPanel = new StackPanel();

		for (int i = 0; i < para.Count; i++)
		{
			var line = para[i];

			var imageRow = TryBuildImageRow(line, BrushText, 1.0);
			if (imageRow != null)
			{
				stackPanel.Children.Add(imageRow);
			}
			else
			{
				var (icon, text) = SplitLeadingIcon(line);

				stackPanel.Children.Add(icon is null
					? new TextBlock
					{
						Text = line,
						FontSize = Fs(13),
						TextWrapping = TextWrapping.Wrap,
						Foreground = BrushText,
						Margin = new Thickness(0, 4, 0, 4),
					}
					: BuildIconRow(icon, text!));
			}

			if (i < para.Count - 1)
			{
				stackPanel.Children.Add(new Border
				{
					Height = 1,
					Background = BrushBorder,
					Opacity = 0.6,
					Margin = new Thickness(0, 6, 0, 6),
				});
			}
		}

		card.Child = stackPanel;
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

	private UIElement? TryBuildImageRow(string line, Brush foreground, double imageOpacity)
	{
		var match = ImageIconRegex.Match(line);
		if (!match.Success)
			return null;

		var resourceName = match.Groups[1].Value;
		var text = match.Groups[2].Value;

		if (string.IsNullOrEmpty(text))
			return null;

		return BuildImageIconRow(resourceName, text, foreground, imageOpacity);
	}

	private Grid BuildImageIconRow(string resourceName, string text, Brush foreground, double imageOpacity)
	{
		var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 24 });
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

		var image = new Image
		{
			Source = new BitmapImage(new Uri($"pack://application:,,,/Resources/{resourceName}.png")),
			Width = 16,
			Height = 16,
			Margin = new Thickness(4, 2, 8, 0),
			VerticalAlignment = VerticalAlignment.Top,
			Opacity = imageOpacity,
		};
		RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
		Grid.SetColumn(image, 0);

		var textBlock = BuildTextWithInlineImages(text, foreground, imageOpacity);
		textBlock.VerticalAlignment = VerticalAlignment.Top;
		Grid.SetColumn(textBlock, 1);

		grid.Children.Add(image);
		grid.Children.Add(textBlock);

		return grid;
	}

	private TextBlock BuildTextWithInlineImages(string text, Brush foreground, double imageOpacity)
	{
		var textBlock = new TextBlock
		{
			FontSize = Fs(13),
			TextWrapping = TextWrapping.Wrap,
			Foreground = foreground,
		};

		var matches = ImageIconInlineRegex.Matches(text);
		if (matches.Count == 0)
		{
			textBlock.Text = text;
			return textBlock;
		}

		int lastIndex = 0;
		foreach (Match m in matches)
		{
			if (m.Index > lastIndex)
				textBlock.Inlines.Add(new Run(text[lastIndex..m.Index]));

			var resourceName = m.Groups[1].Value;
			var image = new Image
			{
				Source = new BitmapImage(new Uri($"pack://application:,,,/Resources/{resourceName}.png")),
				Width = 16,
				Height = 16,
				Margin = new Thickness(2, 0, 2, 0),
				VerticalAlignment = VerticalAlignment.Center,
				Opacity = imageOpacity,
			};
			RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
			textBlock.Inlines.Add(new InlineUIContainer(image));

			lastIndex = m.Index + m.Length;
		}

		if (lastIndex < text.Length)
			textBlock.Inlines.Add(new Run(text[lastIndex..]));

		return textBlock;
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

	private Grid BuildBulletItem(string line)
	{
		var grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 16 });
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

		var bullet = new TextBlock
		{
			Text = "•",
			FontSize = 13,
			FontWeight = FontWeights.Bold,
			Foreground = BrushAccent,
			Margin = new Thickness(0, 0, 8, 0),
			VerticalAlignment = VerticalAlignment.Top,
		};
		Grid.SetColumn(bullet, 0);

		var bulletText = line.StartsWith("- ") ? line[2..] : line.TrimStart('•', ' ');
		var text = BuildTextWithInlineImages(bulletText, BrushTextDim, 0.5);
		text.FontSize = 13;
		text.VerticalAlignment = VerticalAlignment.Top;
		Grid.SetColumn(text, 1);

		grid.Children.Add(bullet);
		grid.Children.Add(text);

		return grid;
	}

	private static StackPanel BuildArrowGrid(List<string> arrows)
	{
		var stackPanel = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };

		foreach (var arrow in arrows)
		{
			stackPanel.Children.Add(new TextBlock
			{
				Text = arrow,
				FontSize = 13,
				FontFamily = new FontFamily("Consolas, Courier New, monospace"),
				Foreground = BrushTextDim,
			});
		}

		return stackPanel;
	}

	private static bool IsArrowLine(string text)
	{
		if (string.IsNullOrEmpty(text))
			return false;

		var firstChar = text[0];

		return firstChar == '↖' || firstChar == '↗' || firstChar == '↙' || firstChar == '↘' ||
			   firstChar == '↑' || firstChar == '↓' || firstChar == '←' || firstChar == '→';
	}
}
