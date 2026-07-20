using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CursorPalette.Models;
using CursorPalette.Services;
using Microsoft.Win32;

namespace CursorPalette.Views;

public partial class PresetEditorWindow : Window
{
	private sealed class Slot
	{
		public required CursorRoleInfo Role { get; init; }
		public required string? DefaultPath { get; init; }
		public string? SourcePath { get; set; }
		public Image PreviewImage { get; init; } = null!;
		public TextBlock FileText { get; init; } = null!;
		public Button ClearButton { get; init; } = null!;
		public FrameworkElement PlaceholderBadge { get; init; } = null!;
	}

	private const string PixelSuffix = "px";
	private const string CurExtension = ".cur";
	private const string AniExtension = ".ani";
	private const double SlotWidth = 160;
	private const double SlotHeight = 172;
	private const double SlotMargin = 6;
	private const double SlotCornerRadius = 10;
	private const double SlotBorderThickness = 2;
	private const double SlotPreviewSize = 40;
	private const double RoleNameFontSize = 12;
	private const double FileTextFontSize = 11;
	private const double ButtonFontSize = 11;
	private const double PlaceholderBadgeFontSize = 9;
	private const double PlaceholderOpacity = 0.45;
	private const string ClearButtonContent = "✕";

	private readonly List<Slot> _slots = new();

	public PresetDraft? Result { get; private set; }

	public PresetEditorWindow(Preset? existing, IReadOnlyList<string> droppedFiles)
	{
		InitializeComponent();

		Title = Loc.Get(existing == null ? "S.Editor.TitleNew" : "S.Editor.TitleEdit");
		NameBox.Text = existing?.Name ?? Loc.Get("S.DefaultPresetName");

		Result = null;
		_draftId = existing?.Id;
		_appliedPreviewSizePx = RegistryCursorService.GetBaseSize();
		_baseSize = existing?.BaseSize ?? _appliedPreviewSizePx;

		var uiScale = AppState.GetUiScale();
		UiScaleTransform.ScaleX = uiScale;
		UiScaleTransform.ScaleY = uiScale;

		EditorSizeSlider.Value = (_baseSize - RegistryCursorService.SizeStep) / (double)RegistryCursorService.SizeStep;
		EditorSizeValueText.Text = $"{_baseSize} {PixelSuffix}";
		_sizeSliderReady = true;
		UpdateApplySizeButtonHighlight();

		var systemDefaults = RegistryCursorService.GetWindowsDefaultValues();

		foreach (var role in CursorRoles.All)
		{
			var defaultPath = systemDefaults.GetValueOrDefault(role.RegistryName);
			if (string.IsNullOrWhiteSpace(defaultPath))
				defaultPath = PlaceholderCursorDefaults.GetPath(role.RegistryName);

			var slot = CreateSlot(role, defaultPath);
			_slots.Add(slot);

			if (existing != null)
			{
				var path = PresetStore.GetRoleFilePath(existing, role.RegistryName);
				if (path != null && File.Exists(path))
					SetSlotSource(slot, path);
			}
		}

		foreach (var file in droppedFiles)
		{
			var role = CursorRoles.MatchByFileName(file);

			if (role == null)
				continue;

			var slot = _slots.First(s => s.Role.RegistryName == role.RegistryName);
			SetSlotSource(slot, file);
		}
	}

	private readonly string? _draftId;
	private int _baseSize;
	private int _appliedPreviewSizePx;
	private bool _sizeSliderReady;

	private void OnEditorSizeSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (EditorSizeValueText == null)
			return;

		var sizePx = RegistryCursorService.SizeStep + (int)e.NewValue * RegistryCursorService.SizeStep;
		EditorSizeValueText.Text = $"{sizePx} {PixelSuffix}";

		if (!_sizeSliderReady)
			return;

		_baseSize = sizePx;

		(Owner as MainWindow)?.SyncSizeSlider(sizePx);
		UpdateApplySizeButtonHighlight();
	}

	private void UpdateApplySizeButtonHighlight() =>
		EditorApplySizeButton.Style = (Style)Application.Current.Resources[
			_baseSize != _appliedPreviewSizePx ? "Style.AccentButton" : "Style.Button"];

	private void OnEditorApplySizeClick(object sender, RoutedEventArgs e)
	{
		RegistryCursorService.SetBaseSize(_baseSize);
		_appliedPreviewSizePx = _baseSize;
		UpdateApplySizeButtonHighlight();
	}

	private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

	private Slot CreateSlot(CursorRoleInfo role, string? defaultPath)
	{
		var preview = new Image { Width = SlotPreviewSize, Height = SlotPreviewSize, SnapsToDevicePixels = true };
		RenderOptions.SetBitmapScalingMode(preview, BitmapScalingMode.NearestNeighbor);

		var placeholderBadge = new Border
		{
			Background = Brush("Brush.SurfaceHover"),
			BorderBrush = Brush("Brush.Border"),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(4),
			Padding = new Thickness(4, 1, 4, 1),
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0, 0, 0, 3),
			Child = new TextBlock
			{
				Text = Loc.Get("S.Editor.PlaceholderBadge"),
				FontSize = PlaceholderBadgeFontSize,
				Foreground = Brush("Brush.TextDim"),
			},
		};

		var roleName = new TextBlock
		{
			Text = Loc.Get("S." + role.DisplayKey),
			FontWeight = FontWeights.SemiBold,
			TextAlignment = TextAlignment.Center,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(4, 6, 4, 0),
			FontSize = RoleNameFontSize,
		};

		var fileText = new TextBlock
		{
			Text = Loc.Get("S.Editor.EmptySlot"),
			Foreground = Brush("Brush.TextDim"),
			FontSize = FileTextFontSize,
			TextAlignment = TextAlignment.Center,
			TextTrimming = TextTrimming.CharacterEllipsis,
			Margin = new Thickness(4, 2, 4, 0),
		};

		var browseButton = new Button
		{
			Content = Loc.Get("S.Editor.Browse"),
			Style = (Style)Application.Current.Resources["Style.Button"],
			FontSize = ButtonFontSize,
			Padding = new Thickness(8, 3, 8, 3),
			Margin = new Thickness(0, 6, 4, 0),
		};
		var clearButton = new Button
		{
			Content = ClearButtonContent,
			Style = (Style)Application.Current.Resources["Style.DangerButton"],
			FontSize = ButtonFontSize,
			Padding = new Thickness(8, 3, 8, 3),
			Margin = new Thickness(0, 6, 0, 0),
			ToolTip = Loc.Get("S.Editor.ClearSlot"),
			Visibility = Visibility.Hidden,
		};

		var buttons = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		buttons.Children.Add(browseButton);
		buttons.Children.Add(clearButton);

		var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
		panel.Children.Add(placeholderBadge);
		panel.Children.Add(preview);
		panel.Children.Add(roleName);
		panel.Children.Add(fileText);
		panel.Children.Add(buttons);

		var border = new Border
		{
			Width = SlotWidth,
			Height = SlotHeight,
			Margin = new Thickness(SlotMargin),
			CornerRadius = new CornerRadius(SlotCornerRadius),
			Background = Brush("Brush.Surface"),
			BorderThickness = new Thickness(SlotBorderThickness),
			BorderBrush = Brush("Brush.Border"),
			Child = panel,
			AllowDrop = true,
		};

		var slot = new Slot
		{
			Role = role,
			DefaultPath = defaultPath,
			PreviewImage = preview,
			FileText = fileText,
			ClearButton = clearButton,
			PlaceholderBadge = placeholderBadge,
		};

		browseButton.Click += (_, _) => BrowseForSlot(slot);
		clearButton.Click += (_, _) => ClearSlot(slot);

		border.DragOver += (_, e) =>
		{
			e.Effects = GetSingleCursorFile(e) != null ? DragDropEffects.Copy : DragDropEffects.None;
			e.Handled = true;
		};
		border.DragEnter += (_, _) => border.BorderBrush = Brush("Brush.Accent");
		border.DragLeave += (_, _) => border.BorderBrush = Brush("Brush.Border");
		border.Drop += (_, e) =>
		{
			border.BorderBrush = Brush("Brush.Border");
			var file = GetSingleCursorFile(e);
			if (file != null)
			{
				SetSlotSource(slot, file);
				e.Handled = true;
			}
		};

		Slots.Items.Add(border);

		SetSlotPlaceholder(slot);

		return slot;
	}

	private void BrowseForSlot(Slot slot)
	{
		var dialog = new OpenFileDialog
		{
			Filter = Loc.Get("S.Editor.FileFilter"),
			CheckFileExists = true,
		};
		if (dialog.ShowDialog(this) == true)
			SetSlotSource(slot, dialog.FileName);
	}

	private void SetSlotSource(Slot slot, string path)
	{
		slot.SourcePath = path;
		CursorPreviewService.ApplyPreview(slot.PreviewImage, path);
		slot.PreviewImage.Opacity = 1;
		slot.PlaceholderBadge.Visibility = Visibility.Collapsed;
		slot.FileText.Text = Path.GetFileName(path);
		slot.FileText.Foreground = Brush("Brush.Text");
		slot.ClearButton.Visibility = Visibility.Visible;
	}

	private void SetSlotPlaceholder(Slot slot)
	{
		slot.SourcePath = null;
		CursorPreviewService.ApplyPreview(slot.PreviewImage, slot.DefaultPath);
		slot.PreviewImage.Opacity = PlaceholderOpacity;
		slot.PlaceholderBadge.Visibility = string.IsNullOrWhiteSpace(slot.DefaultPath)
			? Visibility.Collapsed
			: Visibility.Visible;
		slot.FileText.Text = Loc.Get("S.Editor.EmptySlot");
		slot.FileText.Foreground = Brush("Brush.TextDim");
		slot.ClearButton.Visibility = Visibility.Hidden;
	}

	private void ClearSlot(Slot slot) => SetSlotPlaceholder(slot);

	private void OnBrowseFolderClick(object sender, RoutedEventArgs e)
	{
		var dialog = new OpenFolderDialog
		{
			Title = Loc.Get("S.Editor.BrowseFolder"),
		};

		if (dialog.ShowDialog(this) != true)
			return;

		var folder = dialog.FolderName;
		if (!Directory.Exists(folder))
			return;

		var folderName = Path.GetFileName(folder);
		if (!string.IsNullOrWhiteSpace(folderName) && string.IsNullOrWhiteSpace(_draftId))
			NameBox.Text = folderName;

		var cursorFiles = Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
			.Where(IsCursorFile)
			.ToList();

		if (cursorFiles.Count == 0)
		{
			MessageBox.Show(Loc.Get("S.Editor.NoCursorInFolder"), Title,
				MessageBoxButton.OK, MessageBoxImage.Information);
			return;
		}

		var matched = 0;
		foreach (var file in cursorFiles)
		{
			var role = CursorRoles.MatchByFileName(file);
			if (role == null)
				continue;

			var slot = _slots.First(s => s.Role.RegistryName == role.RegistryName);
			SetSlotSource(slot, file);
			matched++;
		}

		if (matched == 0)
		{
			MessageBox.Show(Loc.Format("S.Editor.NoMatchInFolder", cursorFiles.Count), Title,
				MessageBoxButton.OK, MessageBoxImage.Information);
		}
	}

	private static bool IsCursorFile(string path)
	{
		var ext = Path.GetExtension(path).ToLowerInvariant();

		return ext is CurExtension or AniExtension;
	}

	private void OnSaveButtonClick(object sender, RoutedEventArgs e)
	{
		if (_slots.All(s => s.SourcePath == null))
		{
			MessageBox.Show(Loc.Get("S.Editor.NoFiles"), Title,
				MessageBoxButton.OK, MessageBoxImage.Information);
			return;
		}

		var draft = new PresetDraft { Id = _draftId, Name = NameBox.Text, BaseSize = _baseSize };
		foreach (var slot in _slots.Where(s => s.SourcePath != null))
			draft.RoleSources[slot.Role.RegistryName] = slot.SourcePath!;

		Result = draft;
		DialogResult = true;
	}

	private static string? GetSingleCursorFile(DragEventArgs e)
	{
		if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
			return null;

		var file = paths.FirstOrDefault(File.Exists);

		if (file == null)
			return null;

		var ext = Path.GetExtension(file).ToLowerInvariant();

		return ext is CurExtension or AniExtension ? file : null;
	}
}
