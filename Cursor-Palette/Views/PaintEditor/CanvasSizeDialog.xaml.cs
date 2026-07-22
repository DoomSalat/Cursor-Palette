using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CursorPalette.Views;

public partial class CanvasSizeDialog : Window
{
	private const int MinDimension = 1;
	private const int MaxDimension = 256;
	private const string StyleAccentButton = "Style.AccentButton";
	private const string StyleButton = "Style.Button";

	private readonly int _originalWidth;
	private readonly int _originalHeight;
	private int _anchorX = 1;
	private int _anchorY = 1;
	private bool _suppressPresetSelection;

	private static readonly (int W, int H)[] CanvasPresets =
	{
		(16, 16),
		(24, 24),
		(32, 32),
		(48, 48),
		(64, 64),
		(96, 96),
		(128, 128),
		(256, 256),
	};

	public int ResultWidth { get; private set; }
	public int ResultHeight { get; private set; }
	public int ResultAnchorX => _anchorX;
	public int ResultAnchorY => _anchorY;

	public CanvasSizeDialog(int currentWidth, int currentHeight)
	{
		InitializeComponent();

		_originalWidth = currentWidth;
		_originalHeight = currentHeight;

		WidthBox.Text = currentWidth.ToString(CultureInfo.InvariantCulture);
		HeightBox.Text = currentHeight.ToString(CultureInfo.InvariantCulture);

		ResultWidth = currentWidth;
		ResultHeight = currentHeight;

		PopulatePresets();

		UpdateAnchorHighlight();
		UpdatePreview();
	}

	private void PopulatePresets()
	{
		_suppressPresetSelection = true;

		PresetCombo.Items.Add(new ComboBoxItem { Content = "—", Tag = null, IsSelected = true });

		foreach (var (w, h) in CanvasPresets)
			PresetCombo.Items.Add(new ComboBoxItem { Content = $"{w}×{h}", Tag = (w, h) });

		PresetCombo.SelectedIndex = 0;
		_suppressPresetSelection = false;
	}

	private void OnPresetSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_suppressPresetSelection || PresetCombo.SelectedItem is not ComboBoxItem item)
			return;

		if (item.Tag is not (int w, int h))
			return;

		WidthBox.Text = w.ToString(CultureInfo.InvariantCulture);
		HeightBox.Text = h.ToString(CultureInfo.InvariantCulture);

		UpdatePreview();
	}

	private void OnTextInputDigits(object sender, TextCompositionEventArgs e) =>
		e.Handled = !int.TryParse(e.Text, out _);

	private void OnAnchorClick(object sender, RoutedEventArgs e)
	{
		if (sender is not Button button || button.Tag is not string tag)
			return;

		var parts = tag.Split(',');
		_anchorX = int.Parse(parts[0], CultureInfo.InvariantCulture);
		_anchorY = int.Parse(parts[1], CultureInfo.InvariantCulture);

		UpdateAnchorHighlight();
		UpdatePreview();
	}

	private void UpdateAnchorHighlight()
	{
		foreach (var child in AnchorGrid.Children)
		{
			if (child is not Button button || button.Tag is not string tag)
				continue;

			var parts = tag.Split(',');
			var ax = int.Parse(parts[0], CultureInfo.InvariantCulture);
			var ay = int.Parse(parts[1], CultureInfo.InvariantCulture);
			var isCurrent = ax == _anchorX && ay == _anchorY;

			button.Style = (Style)Application.Current.Resources[isCurrent ? StyleAccentButton : StyleButton];
		}
	}

	private int ParseDimension(string text, int fallback) =>
		int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
			? Math.Clamp(value, MinDimension, MaxDimension)
			: fallback;

	private void UpdatePreview()
	{
		var newW = ParseDimension(WidthBox.Text, _originalWidth);
		var newH = ParseDimension(HeightBox.Text, _originalHeight);

		ResultWidth = newW;
		ResultHeight = newH;

		const double maxPreviewW = 232;
		const double maxPreviewH = 140;
		var scale = Math.Min(maxPreviewW / Math.Max(newW, _originalWidth), maxPreviewH / Math.Max(newH, _originalHeight));

		var curW = _originalWidth * scale;
		var curH = _originalHeight * scale;
		var newWd = newW * scale;
		var newHd = newH * scale;

		var offsetX = _anchorX * (newWd - curW) / 2.0;
		var offsetY = _anchorY * (newHd - curH) / 2.0;

		var curX = (maxPreviewW - newWd) / 2.0 + offsetX;
		var curY = (maxPreviewH - newHd) / 2.0 + offsetY;

		Canvas.SetLeft(CurrentRect, curX);
		Canvas.SetTop(CurrentRect, curY);
		CurrentRect.Width = curW;
		CurrentRect.Height = curH;

		var newX = (maxPreviewW - newWd) / 2.0;
		var newY = (maxPreviewH - newHd) / 2.0;

		Canvas.SetLeft(NewRect, newX);
		Canvas.SetTop(NewRect, newY);
		NewRect.Width = newWd;
		NewRect.Height = newHd;
	}

	private void OnApplyClick(object sender, RoutedEventArgs e)
	{
		UpdatePreview();
		DialogResult = true;
	}
}
