using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using CursorPalette.Services;

namespace CursorPalette.Linux.Views;

public partial class PaintEditorWindow
{
	private int HotspotAbsoluteX => _offsetX + _hotspotOffsetX;
	private int HotspotAbsoluteY => _offsetY + _hotspotOffsetY;

	private void BuildHotspotToolPanel()
	{
		var presetGrid = new Grid
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			RowDefinitions = new RowDefinitions("*,*,*"),
			ColumnDefinitions = new ColumnDefinitions("*,*,*"),
			Width = SnapGridSize,
			Height = SnapGridSize,
		};

		var fractions = new[]
		{
			("0,0", "↖"), ("0.5,0", "↑"), ("1,0", "↗"),
			("0,0.5", "←"), ("0.5,0.5", "●"), ("1,0.5", "→"),
			("0,1", "↙"), ("0.5,1", "↓"), ("1,1", "↘"),
		};

		for (var index = 0; index < fractions.Length; index++)
		{
			var (tag, icon) = fractions[index];
			var row = index / 3;
			var col = index % 3;
			var presetButton = new Button
			{
				Content = icon,
				Tag = tag,
				Padding = new Thickness(SnapGridButtonPadding),
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Stretch,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				VerticalContentAlignment = VerticalAlignment.Center,
			};
			presetButton.Click += OnHotspotPresetClick;
			Grid.SetRow(presetButton, row);
			Grid.SetColumn(presetButton, col);
			presetGrid.Children.Add(presetButton);
		}

		_hotspotToolPanel.Children.Add(new TextBlock { Text = "Hotspot", FontWeight = FontWeight.SemiBold });
		_hotspotToolPanel.Children.Add(new TextBlock { Text = "Click on canvas to set hotspot.", FontSize = ToolPanelLabelFontSize, TextWrapping = TextWrapping.Wrap });
		_hotspotToolPanel.Children.Add(presetGrid);
	}

	private void UpdateHotspotMarker()
	{
		if (!_hotspotMarker.IsVisible)
			return;

		var markerSize = HotspotMarkerScreenPx / _zoom;
		var glowSize = HotspotGlowScreenPx / _zoom;
		var strokeThickness = HotspotMarkerStrokeScreenPx / _zoom;

		_hotspotMarker.Width = markerSize;
		_hotspotMarker.Height = markerSize;
		_hotspotMarker.StrokeThickness = strokeThickness;

		_hotspotMarkerGlow.Width = glowSize;
		_hotspotMarkerGlow.Height = glowSize;

		var centerX = HotspotAbsoluteX + 0.5;
		var centerY = HotspotAbsoluteY + 0.5;

		Avalonia.Controls.Canvas.SetLeft(_hotspotMarker, centerX - markerSize / 2);
		Avalonia.Controls.Canvas.SetTop(_hotspotMarker, centerY - markerSize / 2);
		Avalonia.Controls.Canvas.SetLeft(_hotspotMarkerGlow, centerX - glowSize / 2);
		Avalonia.Controls.Canvas.SetTop(_hotspotMarkerGlow, centerY - glowSize / 2);
	}

	private void UpdateHotspotCoords() =>
		_hotspotCoordsText.Text = string.Format(HotspotCoordsFormat, HotspotAbsoluteX, HotspotAbsoluteY);

	private void SetHotspotFromCanvasPosition(Point position)
	{
		var canvasX = Math.Clamp((int)Math.Floor(position.X), 0, _canvasWidth - 1);
		var canvasY = Math.Clamp((int)Math.Floor(position.Y), 0, _canvasHeight - 1);
		_hotspotOffsetX = canvasX - _offsetX;
		_hotspotOffsetY = canvasY - _offsetY;
		UpdateHotspotMarker();
		UpdateHotspotCoords();
	}

	private void OnHotspotPresetClick(object? sender, RoutedEventArgs e)
	{
		if (sender is not Button presetButton || presetButton.Tag is not string tag)
			return;
		PushHistory();
		var (fractionX, fractionY) = ParseFraction(tag);
		var canvasX = Math.Clamp((int)Math.Round(fractionX * (_canvasWidth - 1)), 0, _canvasWidth - 1);
		var canvasY = Math.Clamp((int)Math.Round(fractionY * (_canvasHeight - 1)), 0, _canvasHeight - 1);
		_hotspotOffsetX = canvasX - _offsetX;
		_hotspotOffsetY = canvasY - _offsetY;
		UpdateHotspotMarker();
		UpdateHotspotCoords();
	}
}
