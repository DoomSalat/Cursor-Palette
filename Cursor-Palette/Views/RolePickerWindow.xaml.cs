using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CursorPalette.Models;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class RolePickerWindow : Window
{
	private const double TileSize = 96;
	private const double TilePreviewSize = 40;
	private const double TileMargin = 6;
	private const double TileCornerRadius = 8;
	private const double TileFontSize = 11;

	private const string LocInfoTitle = "S.Info.Title";
	private const string LocInfoRolePicker = "S.Info.RolePicker";
	private const string BrushAccent = "Brush.Accent";
	private const string BrushBorder = "Brush.Border";
	private const string BrushSurface = "Brush.Surface";
	private const string BrushSurfaceHover = "Brush.SurfaceHover";

	private readonly Preset _source;
	private readonly string _currentRole;
	private bool _ready;

	public string? SelectedRole { get; private set; }

	public RolePickerWindow(Preset source, string currentRole)
	{
		InitializeComponent();

		var uiScale = AppState.GetUiScale();
		UiScaleTransform.ScaleX = uiScale;
		UiScaleTransform.ScaleY = uiScale;

		_source = source;
		_currentRole = currentRole;
		SourceNameText.Text = source.Name;
		_ready = true;

		Rebuild();
	}

	private void OnOnlyCurrentRoleChanged(object sender, RoutedEventArgs e)
	{
		if (_ready)
			Rebuild();
	}

	private void OnInfoButtonClick(object sender, RoutedEventArgs e)
	{
		new InfoHelpWindow(Loc.Get(LocInfoTitle), Loc.Get(LocInfoRolePicker)) { Owner = this }.ShowDialog();
	}

	private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

	private void Rebuild()
	{
		Tiles.Items.Clear();

		var onlyCurrent = OnlyCurrentRoleCheck.IsChecked == true;
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
			Tiles.Items.Add(CreateTile(role, path));
		}

		EmptyHint.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
	}

	private Border CreateTile(CursorRoleInfo role, string path)
	{
		var preview = new Image
		{
			Width = TilePreviewSize,
			Height = TilePreviewSize,
			SnapsToDevicePixels = true,
		};

		RenderOptions.SetBitmapScalingMode(preview, BitmapScalingMode.NearestNeighbor);
		CursorPreviewService.ApplyPreview(preview, path);

		var label = new TextBlock
		{
			Text = Loc.Get("S." + role.DisplayKey),
			FontSize = TileFontSize,
			TextAlignment = TextAlignment.Center,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(2, 4, 2, 0),
		};

		var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

		panel.Children.Add(preview);
		panel.Children.Add(label);

		var isCurrent = role.RegistryName == _currentRole;

		var tile = new Border
		{
			Width = TileSize,
			Height = TileSize,
			Margin = new Thickness(TileMargin),
			CornerRadius = new CornerRadius(TileCornerRadius),
			Background = Brush(BrushSurface),
			BorderThickness = new Thickness(2),
			BorderBrush = isCurrent ? Brush(BrushAccent) : Brush(BrushBorder),
			Child = panel,
			Cursor = Cursors.Hand,
		};

		tile.MouseEnter += (_, _) => tile.Background = Brush(BrushSurfaceHover);
		tile.MouseLeave += (_, _) => tile.Background = Brush(BrushSurface);
		tile.MouseLeftButtonUp += (_, _) =>
		{
			SelectedRole = role.RegistryName;
			DialogResult = true;
		};

		return tile;
	}
}
