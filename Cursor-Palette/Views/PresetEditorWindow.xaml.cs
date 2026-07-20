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
		public string? SourcePath { get; set; }
		public Image PreviewImage { get; init; } = null!;
		public TextBlock FileText { get; init; } = null!;
		public Button ClearButton { get; init; } = null!;
	}

	private readonly List<Slot> _slots = new();

	public PresetDraft? Result { get; private set; }

	public PresetEditorWindow(Preset? existing, IReadOnlyList<string> droppedFiles)
	{
		InitializeComponent();

		Title = Loc.Get(existing == null ? Constants.Strings.EditorTitleNew : Constants.Strings.EditorTitleEdit);
		NameBox.Text = existing?.Name ?? Loc.Get(Constants.Strings.DefaultPresetName);

		Result = null;
		_draftId = existing?.Id;
		// Новый пресет наследует текущий системный размер, существующий — свой.
		_baseSize = existing?.BaseSize ?? RegistryCursorService.GetBaseSize();

		EditorSizeSlider.Value = (_baseSize - Constants.Cursor.SizeStep) / (double)Constants.Cursor.SizeStep;
		EditorSizeValueText.Text = $"{_baseSize} {Constants.UI.PixelSuffix}";
		_sizeSliderReady = true;

		foreach (var role in CursorRoles.All)
		{
			var slot = CreateSlot(role);
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
			if (role == null) continue;
			var slot = _slots.First(s => s.Role.RegistryName == role.RegistryName);
			SetSlotSource(slot, file);
		}
	}

	private readonly string? _draftId;
	private int _baseSize;
	private bool _sizeSliderReady;

	private void OnEditorSizeSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (EditorSizeValueText == null)
			return;

		var sizePx = Constants.Cursor.SizeStep + (int)e.NewValue * Constants.Cursor.SizeStep;
		EditorSizeValueText.Text = $"{sizePx} {Constants.UI.PixelSuffix}";

		if (!_sizeSliderReady)
			return;

		_baseSize = sizePx;

		// Синхронизация с верхним ползунком главного окна.
		(Owner as MainWindow)?.SyncSizeSlider(sizePx);
	}

	private void OnEditorApplySizeClick(object sender, RoutedEventArgs e)
	{
		// Применяем к системе сразу (живой предпросмотр размера);
		// в манифест значение попадёт при "Сохранить".
		RegistryCursorService.SetBaseSize(_baseSize);
	}

	private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

	private Slot CreateSlot(CursorRoleInfo role)
	{
		var preview = new Image { Width = Constants.UI.Editor.SlotPreviewSize, Height = Constants.UI.Editor.SlotPreviewSize, SnapsToDevicePixels = true };
		RenderOptions.SetBitmapScalingMode(preview, BitmapScalingMode.NearestNeighbor);

		var roleName = new TextBlock
		{
			Text = Loc.Get(Constants.Strings.Prefix + role.DisplayKey),
			FontWeight = FontWeights.SemiBold,
			TextAlignment = TextAlignment.Center,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(4, 6, 4, 0),
			FontSize = Constants.UI.Editor.RoleNameFontSize,
		};

		var fileText = new TextBlock
		{
			Text = Loc.Get(Constants.Strings.EditorEmptySlot),
			Foreground = Brush(Constants.Resources.BrushTextDim),
			FontSize = Constants.UI.Editor.FileTextFontSize,
			TextAlignment = TextAlignment.Center,
			TextTrimming = TextTrimming.CharacterEllipsis,
			Margin = new Thickness(4, 2, 4, 0),
		};

		var browseButton = new Button
		{
			Content = Loc.Get(Constants.Strings.EditorBrowse),
			Style = (Style)Application.Current.Resources[Constants.Resources.StyleButton],
			FontSize = Constants.UI.Editor.ButtonFontSize,
			Padding = new Thickness(8, 3, 8, 3),
			Margin = new Thickness(0, 6, 4, 0),
		};
		var clearButton = new Button
		{
			Content = Constants.UI.Editor.ClearButtonContent,
			Style = (Style)Application.Current.Resources[Constants.Resources.StyleButton],
			FontSize = Constants.UI.Editor.ButtonFontSize,
			Padding = new Thickness(8, 3, 8, 3),
			Margin = new Thickness(0, 6, 0, 0),
			ToolTip = Loc.Get(Constants.Strings.EditorClearSlot),
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
		panel.Children.Add(preview);
		panel.Children.Add(roleName);
		panel.Children.Add(fileText);
		panel.Children.Add(buttons);

		var border = new Border
		{
			Width = Constants.UI.Editor.SlotWidth,
			Height = Constants.UI.Editor.SlotHeight,
			Margin = new Thickness(Constants.UI.Editor.SlotMargin),
			CornerRadius = new CornerRadius(Constants.UI.Editor.SlotCornerRadius),
			Background = Brush(Constants.Resources.BrushSurface),
			BorderThickness = new Thickness(Constants.UI.Editor.SlotBorderThickness),
			BorderBrush = Brush(Constants.Resources.BrushBorder),
			Child = panel,
			AllowDrop = true,
		};

		var slot = new Slot
		{
			Role = role,
			PreviewImage = preview,
			FileText = fileText,
			ClearButton = clearButton,
		};

		browseButton.Click += (_, _) => BrowseForSlot(slot);
		clearButton.Click += (_, _) => ClearSlot(slot);

		border.DragOver += (_, e) =>
		{
			e.Effects = GetSingleCursorFile(e) != null ? DragDropEffects.Copy : DragDropEffects.None;
			e.Handled = true;
		};
		border.DragEnter += (_, _) => border.BorderBrush = Brush(Constants.Resources.BrushAccent);
		border.DragLeave += (_, _) => border.BorderBrush = Brush(Constants.Resources.BrushBorder);
		border.Drop += (_, e) =>
		{
			border.BorderBrush = Brush(Constants.Resources.BrushBorder);
			var file = GetSingleCursorFile(e);
			if (file != null)
			{
				SetSlotSource(slot, file);
				e.Handled = true;
			}
		};

		Slots.Items.Add(border);
		return slot;
	}

	private void BrowseForSlot(Slot slot)
	{
		var dialog = new OpenFileDialog
		{
			Filter = Loc.Get(Constants.Strings.EditorFileFilter),
			CheckFileExists = true,
		};
		if (dialog.ShowDialog(this) == true)
			SetSlotSource(slot, dialog.FileName);
	}

	private void SetSlotSource(Slot slot, string path)
	{
		slot.SourcePath = path;
		slot.PreviewImage.Source = CursorPreviewService.GetPreview(path);
		slot.FileText.Text = Path.GetFileName(path);
		slot.FileText.Foreground = Brush(Constants.Resources.BrushText);
		slot.ClearButton.Visibility = Visibility.Visible;
	}

	private void ClearSlot(Slot slot)
	{
		slot.SourcePath = null;
		slot.PreviewImage.Source = null;
		slot.FileText.Text = Loc.Get(Constants.Strings.EditorEmptySlot);
		slot.FileText.Foreground = Brush(Constants.Resources.BrushTextDim);
		slot.ClearButton.Visibility = Visibility.Hidden;
	}

	private void OnBrowseFolderClick(object sender, RoutedEventArgs e)
	{
		var dialog = new OpenFolderDialog
		{
			Title = Loc.Get(Constants.Strings.EditorBrowseFolder),
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
			MessageBox.Show(Loc.Get(Constants.Strings.EditorNoCursorInFolder), Title,
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
			MessageBox.Show(Loc.Format(Constants.Strings.EditorNoMatchInFolder, cursorFiles.Count), Title,
				MessageBoxButton.OK, MessageBoxImage.Information);
		}
	}

	private static bool IsCursorFile(string path)
	{
		var ext = Path.GetExtension(path).ToLowerInvariant();
		return ext is Constants.Files.CurExtension or Constants.Files.AniExtension;
	}

	private void OnSaveButtonClick(object sender, RoutedEventArgs e)
	{
		if (_slots.All(s => s.SourcePath == null))
		{
			MessageBox.Show(Loc.Get(Constants.Strings.EditorNoFiles), Title,
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

		return ext is Constants.Files.CurExtension or Constants.Files.AniExtension ? file : null;
	}
}
