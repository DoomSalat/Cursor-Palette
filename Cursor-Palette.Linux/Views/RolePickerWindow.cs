using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using CursorPalette.Linux.Services;
using CursorPalette.Models;
using CursorPalette.Services;

namespace CursorPalette.Linux.Views;

public class RolePickerWindow : Window
{
	private const double TileSize = 96;
	private const double TilePreviewSize = 40;
	private const double TileMargin = 6;
	private const double TileCornerRadius = 8;
	private const double TileFontSize = 11;
	private const double DialogWidth = 640;
	private const double DialogHeight = 480;
	private const double DialogMinWidth = 420;
	private const double DialogMinHeight = 320;
	private const double DialogPadding = 16;
	private const double CloseButtonMinWidth = 90;
	private const double InfoButtonFontSize = 15;
	private const double InfoButtonPaddingH = 8;
	private const double InfoButtonPaddingV = 6;
	private const double TopBarPaddingVertical = 8;
	private const double SourceNameMarginBottom = 4;
	private const double CheckBoxMarginBottom = 10;
	private const double ScrollViewerRightPadding = 6;
	private const double BottomBarPaddingVertical = 10;
	private const double TileBorderThickness = 2;
	private const double TileHoverOpacity = 0.3;
	private const string InfoButtonContent = "ⓘ";
	private const string HelpTextRolePickerKey = "RolePicker";

	private const string LocInfoTitle = "S.Info.Title";
	private const string LocInfoTooltip = "S.Info.Tooltip";
	private const string LocRolePickerTitle = "S.RolePicker.Title";
	private const string LocRolePickerOnlyCurrentRole = "S.RolePicker.OnlyCurrentRole";
	private const string LocRolePickerRoleNotAssigned = "S.RolePicker.RoleNotAssigned";
	private const string LocEditorCancel = "S.Editor.Cancel";

	private readonly Preset _source;
	private readonly string _currentRole;
	private bool _ready;

	public string? SelectedRole { get; private set; }

	public RolePickerWindow(Preset source, string currentRole)
	{
		_source = source;
		_currentRole = currentRole;

		Title = Loc.Get(LocRolePickerTitle);
		Width = DialogWidth;
		Height = DialogHeight;
		MinWidth = DialogMinWidth;
		MinHeight = DialogMinHeight;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;

		var root = new Grid();
		root.RowDefinitions.Add(new RowDefinition(0, GridUnitType.Auto));
		root.RowDefinitions.Add(new RowDefinition(0, GridUnitType.Auto));
		root.RowDefinitions.Add(new RowDefinition(0, GridUnitType.Auto));
		root.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
		root.RowDefinitions.Add(new RowDefinition(0, GridUnitType.Auto));

		var infoButton = new Button
		{
			Content = new TextBlock { Text = InfoButtonContent, FontSize = InfoButtonFontSize },
			Padding = new Thickness(InfoButtonPaddingH, InfoButtonPaddingV),
			HorizontalAlignment = HorizontalAlignment.Right,
		};
		ToolTip.SetTip(infoButton, Loc.Get(LocInfoTooltip));
		infoButton.Click += OnInfoButtonClick;

		var topBar = new Border
		{
			BorderThickness = new Thickness(0, 0, 0, 1),
			Padding = new Thickness(DialogPadding, TopBarPaddingVertical),
			Child = infoButton,
		};
		ThemeManager.BindResource(topBar, Border.BackgroundProperty, "SystemControlDefaultChromeMediumBrush");
		ThemeManager.BindResource(topBar, Border.BorderBrushProperty, "SystemControlBorderBrush");
		Grid.SetRow(topBar, 0);
		root.Children.Add(topBar);

		var sourceNameText = new TextBlock
		{
			Text = source.Name,
			FontWeight = FontWeight.SemiBold,
			Margin = new Thickness(DialogPadding, 0, 0, SourceNameMarginBottom),
		};
		Grid.SetRow(sourceNameText, 1);
		root.Children.Add(sourceNameText);

		var onlyCurrentRoleCheck = new CheckBox
		{
			Content = Loc.Get(LocRolePickerOnlyCurrentRole),
			IsChecked = true,
			Margin = new Thickness(DialogPadding, 0, 0, CheckBoxMarginBottom),
		};
		onlyCurrentRoleCheck.IsCheckedChanged += (_, _) =>
		{
			if (_ready)
				Rebuild();
		};
		Grid.SetRow(onlyCurrentRoleCheck, 2);
		root.Children.Add(onlyCurrentRoleCheck);

		var tilesPanel = new WrapPanel();

		var scrollViewer = new ScrollViewer
		{
			Content = tilesPanel,
			Padding = new Thickness(DialogPadding, 0, ScrollViewerRightPadding, 0),
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
		};
		Grid.SetRow(scrollViewer, 3);
		root.Children.Add(scrollViewer);

		var emptyHint = new TextBlock
		{
			Text = Loc.Get(LocRolePickerRoleNotAssigned),
			TextAlignment = TextAlignment.Center,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(DialogPadding),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			IsVisible = false,
		};
		ThemeManager.BindResource(emptyHint, TextBlock.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");
		Grid.SetRow(emptyHint, 3);
		root.Children.Add(emptyHint);

		var cancelButton = new Button
		{
			Content = Loc.Get(LocEditorCancel),
			HorizontalAlignment = HorizontalAlignment.Right,
			MinWidth = CloseButtonMinWidth,
		};
		cancelButton.Click += (_, _) => Close();

		var bottomBar = new Border
		{
			BorderThickness = new Thickness(0, 1, 0, 0),
			Padding = new Thickness(DialogPadding, BottomBarPaddingVertical),
			Child = cancelButton,
		};
		ThemeManager.BindResource(bottomBar, Border.BackgroundProperty, "SystemControlDefaultChromeMediumBrush");
		ThemeManager.BindResource(bottomBar, Border.BorderBrushProperty, "SystemControlBorderBrush");
		Grid.SetRow(bottomBar, 4);
		root.Children.Add(bottomBar);

		Content = root;

		var uiScale = AppState.GetUiScale();
		if (uiScale != 1.0)
		{
			root.RenderTransform = new ScaleTransform(uiScale, uiScale);
			root.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
		}

		_tilesPanel = tilesPanel;
		_emptyHint = emptyHint;
		_onlyCurrentRoleCheck = onlyCurrentRoleCheck;
		_ready = true;

		Rebuild();
	}

	private readonly WrapPanel _tilesPanel;
	private readonly TextBlock _emptyHint;
	private readonly CheckBox _onlyCurrentRoleCheck;

	private void OnInfoButtonClick(object? sender, RoutedEventArgs e)
	{
		new InfoHelpWindow(Loc.Get(LocInfoTitle), HelpTextService.Get(HelpTextRolePickerKey)).ShowDialog(this);
	}

	private void Rebuild()
	{
		_tilesPanel.Children.Clear();

		var onlyCurrent = _onlyCurrentRoleCheck.IsChecked == true;
		var roles = onlyCurrent
			? CursorRoles.All.Where(role => role.RegistryName == _currentRole)
			: CursorRoles.All;

		var any = false;

		foreach (var role in roles)
		{
			var path = PresetStore.GetRoleFilePath(_source, role.RegistryName);
			if (path == null || !File.Exists(path))
				continue;

			any = true;
			_tilesPanel.Children.Add(CreateTile(role, path));
		}

		_emptyHint.IsVisible = !any;
	}

	private Border CreateTile(CursorRoleInfo role, string path)
	{
		var image = new Image
		{
			Width = TilePreviewSize,
			Height = TilePreviewSize,
		};

		var preview = CursorPreviewService.GetPreview(path);
		if (preview != null)
			image.Source = preview;

		var label = new TextBlock
		{
			Text = Loc.Get("S." + role.DisplayKey),
			FontSize = TileFontSize,
			TextAlignment = TextAlignment.Center,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(2, 4, 2, 0),
		};

		var panel = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center,
		};
		panel.Children.Add(image);
		panel.Children.Add(label);

		var isCurrent = role.RegistryName == _currentRole;

		var tile = new Border
		{
			Width = TileSize,
			Height = TileSize,
			Margin = new Thickness(TileMargin),
			CornerRadius = new CornerRadius(TileCornerRadius),
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(TileBorderThickness),
			Child = panel,
			Cursor = new Cursor(StandardCursorType.Hand),
		};
		ThemeManager.BindResource(tile, Border.BorderBrushProperty,
			isCurrent ? "SystemAccentColor" : "SystemControlBorderBrush");

		tile.PointerEntered += (_, _) => tile.Background = new SolidColorBrush(Colors.LightGray, TileHoverOpacity);
		tile.PointerExited += (_, _) => tile.Background = Brushes.Transparent;
		tile.PointerReleased += (_, e) =>
		{
			if (e.InitialPressMouseButton == MouseButton.Left)
			{
				SelectedRole = role.RegistryName;
				Close();
			}
		};

		return tile;
	}
}
