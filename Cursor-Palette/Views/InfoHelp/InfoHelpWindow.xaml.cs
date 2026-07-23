using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

	private static readonly Regex ImageIconRegex = new(@"^\{img:([^}]+)\}\s*(.*)", RegexOptions.Compiled);
	private static readonly Regex ImageIconInlineRegex = new(@"\{img:([^}]+)\}", RegexOptions.Compiled);

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
		var imageRow = TryBuildImageRow(text, BrushText, 1.0);
		if (imageRow != null)
		{
			BodyPanel.Children.Add(imageRow);
			return;
		}

		BodyPanel.Children.Add(new TextBlock
		{
			Text = text,
			FontSize = Fs(13),
			TextWrapping = TextWrapping.Wrap,
			Foreground = BrushText,
			Margin = new Thickness(0, 4, 0, 4),
		});
	}
}
