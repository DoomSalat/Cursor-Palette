using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CursorPalette.Models;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class GroupEditWindow : Window
{
	private const double SwatchSize = 24;
	private const double SwatchRingThickness = 2.5;

	private string? _selectedColorKey;

	public string GroupName => NameBox.Text.Trim();
	public string ColorKey => _selectedColorKey ?? GroupColors.Palette.First().Key;

	public GroupEditWindow(PresetGroup? group = null)
	{
		InitializeComponent();

		var uiScale = AppState.GetUiScale();
		UiScaleTransform.ScaleX = uiScale;
		UiScaleTransform.ScaleY = uiScale;

		if (group != null)
		{
			NameBox.Text = group.Name;
			_selectedColorKey = group.ColorKey;
		}
		else
		{
			_selectedColorKey = GroupColors.Palette.First().Key;
		}

		BuildColorSwatches();

		NameBox.Focus();
		NameBox.SelectAll();
	}

	private void BuildColorSwatches()
	{
		foreach (var (key, hex) in GroupColors.Palette)
		{
			var colorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);

			var swatch = new Border
			{
				Width = SwatchSize,
				Height = SwatchSize,
				CornerRadius = new CornerRadius(SwatchSize),
				Background = colorBrush,
				BorderThickness = key == _selectedColorKey
					? new Thickness(SwatchRingThickness)
					: new Thickness(0),
				BorderBrush = (Brush)Application.Current.Resources["Brush.Text"],
				Margin = new Thickness(3, 3, 3, 3),
				Cursor = Cursors.Hand,
			};

			var capturedKey = key;
			swatch.MouseLeftButtonUp += (_, _) =>
			{
				_selectedColorKey = capturedKey;

				foreach (var child in ColorSwatches.Children.OfType<Border>())
					child.BorderThickness = new Thickness(0);

				swatch.BorderThickness = new Thickness(SwatchRingThickness);
			};

			ColorSwatches.Children.Add(swatch);
		}
	}

	private void OnSaveClick(object sender, RoutedEventArgs e)
	{
		if (string.IsNullOrWhiteSpace(GroupName))
			NameBox.Text = Loc.Get("S.Group.DefaultName");

		DialogResult = true;
	}
}
