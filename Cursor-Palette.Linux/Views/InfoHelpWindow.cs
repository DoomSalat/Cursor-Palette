using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using CursorPalette.Linux.Services;
using CursorPalette.Services;

namespace CursorPalette.Linux.Views;

public class InfoHelpWindow : Window
{
	private const double TextZoomStep = 0.1;
	private const int IconTokenMaxLength = 8;
	private const double DialogWidth = 600;
	private const double DialogHeight = 600;
	private const double DialogMinWidth = 380;
	private const double DialogMinHeight = 300;
	private const double DialogPadding = 16;
	private const double CloseButtonMinWidth = 90;

	private const string LocInfoTitle = "S.Info.Title";
	private const string LocInfoClose = "S.Info.Close";

	private static readonly Regex ImageIconRegex = new(@"^\{img:([^}]+)\}\s*(.*)", RegexOptions.Compiled);
	private static readonly Regex ImageIconInlineRegex = new(@"\{img:([^}]+)\}", RegexOptions.Compiled);

	private readonly string _body;
	private double _textScale = AppState.InfoTextScaleDefault;
	private StackPanel? _bodyPanel;
	private TextBlock? _zoomLabel;

	public InfoHelpWindow(string title, string body)
	{
		_body = body;
		_textScale = AppState.GetInfoTextScale();

		Title = title;
		Width = DialogWidth;
		Height = DialogHeight;
		MinWidth = DialogMinWidth;
		MinHeight = DialogMinHeight;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;

		BuildContent(title);
		ApplyTextZoom();
	}

	private double FontSizeScaled(double baseSize) => Math.Round(baseSize * _textScale, 1);

	private void BuildContent(string title)
	{
		var root = new Grid
		{
			RowDefinitions = new RowDefinitions("Auto,*,Auto"),
		};

		var headerBorder = new Border
		{
			Padding = new Thickness(DialogPadding, 10),
			BorderThickness = new Thickness(0, 0, 0, 1),
		};
		ThemeManager.BindResource(headerBorder, Border.BackgroundProperty, "SystemControlDefaultChromeMediumBrush");
		ThemeManager.BindResource(headerBorder, Border.BorderBrushProperty, "SystemControlBorderBrush");

		var headerGrid = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
		};

		var titleText = new TextBlock
		{
			Text = title,
			FontSize = 16,
			FontWeight = FontWeight.SemiBold,
			VerticalAlignment = VerticalAlignment.Center,
		};
		Grid.SetColumn(titleText, 0);
		headerGrid.Children.Add(titleText);

		var zoomOutButton = new Button
		{
			Content = "−",
			Padding = new Thickness(8, 2),
			VerticalAlignment = VerticalAlignment.Center,
		};
		Grid.SetColumn(zoomOutButton, 1);
		zoomOutButton.Click += (_, _) => AdjustTextZoom(-TextZoomStep);
		headerGrid.Children.Add(zoomOutButton);

		_zoomLabel = new TextBlock
		{
			Text = "100%",
			FontSize = 11,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(4, 0),
		};

		var zoomInButton = new Button
		{
			Content = "+",
			Padding = new Thickness(8, 2),
			VerticalAlignment = VerticalAlignment.Center,
		};
		Grid.SetColumn(zoomInButton, 2);
		zoomInButton.Click += (_, _) => AdjustTextZoom(TextZoomStep);
		headerGrid.Children.Add(zoomInButton);

		headerBorder.Child = headerGrid;
		Grid.SetRow(headerBorder, 0);
		root.Children.Add(headerBorder);

		var scrollViewer = new ScrollViewer
		{
			Margin = new Thickness(DialogPadding, 8, DialogPadding, 4),
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
		};

		_bodyPanel = new StackPanel
		{
			Spacing = 6,
		};

		scrollViewer.Content = _bodyPanel;
		Grid.SetRow(scrollViewer, 1);
		root.Children.Add(scrollViewer);

		var closeButton = new Button
		{
			Content = Loc.Get(LocInfoClose),
			MinWidth = CloseButtonMinWidth,
			HorizontalAlignment = HorizontalAlignment.Right,
			Classes = { "accent" },
		};
		closeButton.Click += (_, _) => Close();

		var footerBorder = new Border
		{
			Padding = new Thickness(DialogPadding, 10),
			BorderThickness = new Thickness(0, 1, 0, 0),
			Child = closeButton,
		};
		ThemeManager.BindResource(footerBorder, Border.BackgroundProperty, "SystemControlDefaultChromeMediumBrush");
		ThemeManager.BindResource(footerBorder, Border.BorderBrushProperty, "SystemControlBorderBrush");
		Grid.SetRow(footerBorder, 2);
		root.Children.Add(footerBorder);

		Content = root;

		var uiScale = AppState.GetUiScale();
		if (uiScale != 1.0)
		{
			root.RenderTransform = new ScaleTransform(uiScale, uiScale);
			root.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
		}
	}

	private void AdjustTextZoom(double delta)
	{
		_textScale = Math.Clamp(Math.Round(_textScale + delta, 2),
			AppState.InfoTextScaleMin, AppState.InfoTextScaleMax);
		AppState.SetInfoTextScale(_textScale);
		ApplyTextZoom();
	}

	private void ApplyTextZoom()
	{
		if (_zoomLabel != null)
			_zoomLabel.Text = $"{(int)Math.Round(_textScale * 100)}%";

		if (_bodyPanel == null)
			return;

		_bodyPanel.Children.Clear();
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

		for (int index = 0; index < paragraphs.Count; index++)
		{
			var paragraph = paragraphs[index];

			if (index == 0)
			{
				var titleText = paragraph[0];
				if (titleText.StartsWith("# "))
					titleText = titleText[2..];

				RenderTitle(titleText);
				continue;
			}

			var hasBullets = paragraph.Any(line => line.StartsWith("- "));
			var hasArrows = paragraph.Any(line => IsArrowLine(line));
			var hasSectionHeader = paragraph[0].StartsWith("## ");

			if (hasSectionHeader || hasBullets || hasArrows)
			{
				RenderSectionCard(paragraph);
				continue;
			}

			if (index == 1 && paragraph.Count == 1)
			{
				RenderStandalone(paragraph[0]);
				continue;
			}

			RenderTipsCard(paragraph);
		}
	}

	private void RenderTitle(string text)
	{
		if (_bodyPanel == null)
			return;

		var titleBlock = new TextBlock
		{
			Text = text,
			FontSize = FontSizeScaled(16),
			FontWeight = FontWeight.SemiBold,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0, 0, 0, 4),
		};
		ThemeManager.BindResource(titleBlock, TextBlock.ForegroundProperty, "SystemAccentColor");
		_bodyPanel.Children.Add(titleBlock);

		var divider = new Border
		{
			Height = 1,
			Margin = new Thickness(0, 0, 0, 8),
		};
		ThemeManager.BindResource(divider, Border.BackgroundProperty, "SystemControlBorderBrush");
		_bodyPanel.Children.Add(divider);
	}

	private void RenderStandalone(string text)
	{
		if (_bodyPanel == null)
			return;

		var cleanText = StripImageTokens(text);
		_bodyPanel.Children.Add(new TextBlock
		{
			Text = cleanText,
			FontSize = FontSizeScaled(13),
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0, 4, 0, 4),
		});
	}

	private void RenderSectionCard(List<string> paragraph)
	{
		if (_bodyPanel == null)
			return;

		var card = new Border
		{
			Background = new SolidColorBrush(0x10FFFFFF),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(8),
			Padding = new Thickness(14, 10, 14, 10),
			Margin = new Thickness(0, 0, 0, 8),
		};
		ThemeManager.BindResource(card, Border.BorderBrushProperty, "SystemControlBorderBrush");

		var stackPanel = new StackPanel();

		var header = paragraph[0];
		if (header.StartsWith("## "))
			header = header[3..];

		if (!string.IsNullOrEmpty(header))
		{
			stackPanel.Children.Add(new TextBlock
			{
				Text = header,
				FontSize = FontSizeScaled(14),
				FontWeight = FontWeight.SemiBold,
				TextWrapping = TextWrapping.Wrap,
				Margin = new Thickness(0, 0, 0, 6),
			});
		}

		foreach (var line in paragraph.Skip(1))
		{
			if (line.StartsWith("- "))
			{
				stackPanel.Children.Add(new TextBlock
				{
					Text = "• " + StripImageTokens(line[2..]),
					FontSize = FontSizeScaled(13),
					TextWrapping = TextWrapping.Wrap,
					Foreground = Brushes.Gray,
					Margin = new Thickness(0, 2, 0, 2),
				});
			}
			else if (IsArrowLine(line))
			{
				stackPanel.Children.Add(new TextBlock
				{
					Text = line,
					FontSize = FontSizeScaled(13),
					FontFamily = new FontFamily("Consolas, Courier New, monospace"),
					Foreground = Brushes.Gray,
				});
			}
			else
			{
				stackPanel.Children.Add(new TextBlock
				{
					Text = StripImageTokens(line),
					FontSize = FontSizeScaled(13),
					TextWrapping = TextWrapping.Wrap,
					Foreground = Brushes.Gray,
					Margin = new Thickness(0, 2, 0, 2),
				});
			}
		}

		card.Child = stackPanel;
		_bodyPanel.Children.Add(card);
	}

	private void RenderTipsCard(List<string> paragraph)
	{
		if (_bodyPanel == null)
			return;

		var card = new Border
		{
			Background = new SolidColorBrush(0x10FFFFFF),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(8),
			Padding = new Thickness(14, 10, 14, 10),
			Margin = new Thickness(0, 0, 0, 8),
		};
		ThemeManager.BindResource(card, Border.BorderBrushProperty, "SystemControlBorderBrush");

		var stackPanel = new StackPanel();

		for (int index = 0; index < paragraph.Count; index++)
		{
			var line = paragraph[index];
			var (icon, rest) = SplitLeadingIcon(line);

			stackPanel.Children.Add(new TextBlock
			{
				Text = icon is null ? StripImageTokens(line) : $"{icon} {StripImageTokens(rest!)}",
				FontSize = FontSizeScaled(13),
				TextWrapping = TextWrapping.Wrap,
				Margin = new Thickness(0, 4, 0, 4),
			});

			if (index < paragraph.Count - 1)
			{
				stackPanel.Children.Add(new Border
				{
					Height = 1,
					Background = Brushes.Gray,
					Opacity = 0.6,
					Margin = new Thickness(0, 4, 0, 4),
				});
			}
		}

		card.Child = stackPanel;
		_bodyPanel.Children.Add(card);
	}

	private static string StripImageTokens(string text)
	{
		return ImageIconInlineRegex.Replace(text, "");
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

	private static bool IsArrowLine(string text)
	{
		if (string.IsNullOrEmpty(text))
			return false;

		var firstChar = text[0];

		return firstChar == '↖' || firstChar == '↗' || firstChar == '↙' || firstChar == '↘' ||
			   firstChar == '↑' || firstChar == '↓' || firstChar == '←' || firstChar == '→';
	}
}
