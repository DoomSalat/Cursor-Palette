using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CursorPalette.Linux.Services;
using CursorPalette.Models;
using CursorPalette.Services;

namespace CursorPalette.Linux.Views;

public class ExistingPresetPickerWindow : Window
{
	private const double CellSize = 120;
	private const double CellMargin = 6;
	private const double CellPreviewSize = 40;
	private const double CellCornerRadius = 10;
	private const double CellBorderThickness = 2;
	private const double CellNameFontSize = 12;
	private const double CellCountFontSize = 10;
	private const double MixedBadgeFontSize = 13;
	private const double DialogWidth = 640;
	private const double DialogHeight = 480;
	private const double DialogMinWidth = 420;
	private const double DialogMinHeight = 320;
	private const double DialogPadding = 16;
	private const double CloseButtonMinWidth = 90;
	private const double InfoButtonFontSize = 15;
	private const string MixedBadgeText = "🧩";

	private const string LocInfoTitle = "S.Info.Title";
	private const string LocInfoTooltip = "S.Info.Tooltip";
	private const string LocPresetPickerTitle = "S.PresetPicker.Title";
	private const string LocPresetPickerEmpty = "S.PresetPicker.Empty";
	private const string LocEditorCancel = "S.Editor.Cancel";

	public Preset? SelectedPreset { get; private set; }

	public ExistingPresetPickerWindow(IReadOnlyList<Preset> presets)
	{
		Title = Loc.Get(LocPresetPickerTitle);
		Width = DialogWidth;
		Height = DialogHeight;
		MinWidth = DialogMinWidth;
		MinHeight = DialogMinHeight;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;

		var root = new Grid();
		root.RowDefinitions.Add(new RowDefinition(0, GridUnitType.Auto));
		root.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
		root.RowDefinitions.Add(new RowDefinition(0, GridUnitType.Auto));

		var infoButton = new Button
		{
			Content = new TextBlock { Text = "ⓘ", FontSize = InfoButtonFontSize },
			Padding = new Thickness(8, 6),
			HorizontalAlignment = HorizontalAlignment.Right,
		};
		ToolTip.SetTip(infoButton, Loc.Get(LocInfoTooltip));
		infoButton.Click += OnInfoButtonClick;

		var topBar = new Border
		{
			BorderThickness = new Thickness(0, 0, 0, 1),
			Padding = new Thickness(DialogPadding, 8),
			Child = infoButton,
		};
		ThemeManager.BindResource(topBar, Border.BackgroundProperty, "SystemControlDefaultChromeMediumBrush");
		ThemeManager.BindResource(topBar, Border.BorderBrushProperty, "SystemControlBorderBrush");
		Grid.SetRow(topBar, 0);
		root.Children.Add(topBar);

		var galleryPanel = new WrapPanel();

		var scrollViewer = new ScrollViewer
		{
			Content = galleryPanel,
			Padding = new Thickness(DialogPadding, 0, 6, 0),
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
		};
		Grid.SetRow(scrollViewer, 1);
		root.Children.Add(scrollViewer);

		var emptyHint = new TextBlock
		{
			Text = Loc.Get(LocPresetPickerEmpty),
			TextAlignment = TextAlignment.Center,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(DialogPadding),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			IsVisible = presets.Count == 0,
		};
		ThemeManager.BindResource(emptyHint, TextBlock.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");
		Grid.SetRow(emptyHint, 1);
		root.Children.Add(emptyHint);

		var bottomBar = new Border
		{
			BorderThickness = new Thickness(0, 1, 0, 0),
			Padding = new Thickness(DialogPadding, 10),
			Child = new Button
			{
				Content = Loc.Get(LocEditorCancel),
				HorizontalAlignment = HorizontalAlignment.Right,
				MinWidth = CloseButtonMinWidth,
			},
		};
		ThemeManager.BindResource(bottomBar, Border.BackgroundProperty, "SystemControlDefaultChromeMediumBrush");
		ThemeManager.BindResource(bottomBar, Border.BorderBrushProperty, "SystemControlBorderBrush");
		((Button)bottomBar.Child!).Click += (_, _) => Close();
		Grid.SetRow(bottomBar, 2);
		root.Children.Add(bottomBar);

		Content = root;

		var uiScale = AppState.GetUiScale();
		if (uiScale != 1.0)
		{
			root.RenderTransform = new ScaleTransform(uiScale, uiScale);
			root.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
		}

		foreach (var preset in presets)
			galleryPanel.Children.Add(CreateCell(preset));
	}

	private void OnInfoButtonClick(object? sender, RoutedEventArgs e)
	{
		new InfoHelpWindow(Loc.Get(LocInfoTitle), HelpTextService.Get("PresetPicker")).ShowDialog(this);
	}

	private Border CreateCell(Preset preset)
	{
		var previewPath = PresetStore.GetRoleFilePath(preset, CursorRoles.ArrowRoleName)
							?? preset.Roles.Keys.Concat(preset.RoleRefs.Keys)
								.Select(role => PresetStore.GetRoleFilePath(preset, role))
								.FirstOrDefault(path => path != null);

		var image = new Image
		{
			Width = CellPreviewSize,
			Height = CellPreviewSize,
		};

		var preview = CursorPreviewService.GetPreview(previewPath);
		if (preview != null)
			image.Source = preview;

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
			FontSize = CellCountFontSize,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(0, 2, 0, 0),
		};
		ThemeManager.BindResource(countText, TextBlock.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");

		var panel = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center,
		};
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
			BorderThickness = new Thickness(CellBorderThickness),
			Background = Brushes.Transparent,
			Child = cellContent,
			Cursor = new Cursor(StandardCursorType.Hand),
		};
		ThemeManager.BindResource(cell, Border.BorderBrushProperty, "SystemControlBorderBrush");

		cell.PointerEntered += (_, _) => ThemeManager.BindResource(cell, Border.BorderBrushProperty, "SystemAccentColor");
		cell.PointerExited += (_, _) => ThemeManager.BindResource(cell, Border.BorderBrushProperty, "SystemControlBorderBrush");
		cell.PointerReleased += (_, e) =>
		{
			if (e.InitialPressMouseButton == MouseButton.Left)
			{
				SelectedPreset = preset;
				Close();
			}
		};

		return cell;
	}
}
