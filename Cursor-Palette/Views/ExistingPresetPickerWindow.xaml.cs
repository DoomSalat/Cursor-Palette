using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CursorPalette.Models;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class ExistingPresetPickerWindow : Window
{
	private const double CellSize = 120;
	private const double CellMargin = 6;
	private const double CellPreviewSize = 40;
	private const double CellCornerRadius = 10;
	private const double CellBorderThickness = 2;
	private const double CellNameFontSize = 12;
	private const double CellCountFontSize = 10;
	private const string MixedBadgeText = "🧩";
	private const double MixedBadgeFontSize = 13;

	public Preset? SelectedPreset { get; private set; }

	public ExistingPresetPickerWindow(IReadOnlyList<Preset> presets)
	{
		InitializeComponent();

		var uiScale = AppState.GetUiScale();
		UiScaleTransform.ScaleX = uiScale;
		UiScaleTransform.ScaleY = uiScale;

		foreach (var preset in presets)
			Gallery.Items.Add(CreateCell(preset));

		EmptyHint.Visibility = presets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
	}

	private void OnInfoButtonClick(object sender, RoutedEventArgs e)
	{
		new InfoHelpWindow(Loc.Get("S.Info.Title"), Loc.Get("S.Info.PresetPicker")) { Owner = this }.ShowDialog();
	}

	private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

	private Border CreateCell(Preset preset)
	{
		var previewPath = PresetStore.GetRoleFilePath(preset, CursorRoles.ArrowRoleName)
							?? preset.Roles.Keys.Concat(preset.RoleRefs.Keys)
								.Select(r => PresetStore.GetRoleFilePath(preset, r))
								.FirstOrDefault(p => p != null);

		var image = new Image { Width = CellPreviewSize, Height = CellPreviewSize, SnapsToDevicePixels = true };
		RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
		CursorPreviewService.ApplyPreview(image, previewPath);

		var nameText = new TextBlock
		{
			Text = preset.Name,
			FontSize = CellNameFontSize,
			TextTrimming = TextTrimming.CharacterEllipsis,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(4, 6, 4, 0),
		};

		var countText = new TextBlock
		{
			Text = $"{preset.Roles.Count + preset.RoleRefs.Count}/{CursorRoles.All.Length}",
			Foreground = Brush("Brush.TextDim"),
			FontSize = CellCountFontSize,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(0, 2, 0, 0),
		};

		var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
		panel.Children.Add(image);
		panel.Children.Add(nameText);
		panel.Children.Add(countText);

		var cellContent = new Grid();
		cellContent.Children.Add(panel);

		if (preset.RoleRefs.Count > 0)
		{
			cellContent.Children.Add(new TextBlock
			{
				Text = MixedBadgeText,
				FontSize = MixedBadgeFontSize,
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Top,
				Margin = new Thickness(0, 4, 6, 0),
				IsHitTestVisible = false,
			});
		}

		var cell = new Border
		{
			Width = CellSize,
			Height = CellSize,
			Margin = new Thickness(CellMargin),
			CornerRadius = new CornerRadius(CellCornerRadius),
			Background = Brush("Brush.Surface"),
			BorderThickness = new Thickness(CellBorderThickness),
			BorderBrush = Brush("Brush.Border"),
			Child = cellContent,
			Cursor = Cursors.Hand,
		};

		cell.MouseEnter += (_, _) => cell.BorderBrush = Brush("Brush.Accent");
		cell.MouseLeave += (_, _) => cell.BorderBrush = Brush("Brush.Border");
		cell.MouseLeftButtonUp += (_, _) =>
		{
			SelectedPreset = preset;
			DialogResult = true;
		};

		return cell;
	}
}
