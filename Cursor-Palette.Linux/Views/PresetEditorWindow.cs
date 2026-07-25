using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CursorPalette.Linux.Services;
using CursorPalette.Models;
using CursorPalette.Services;

namespace CursorPalette.Linux.Views;

public class PresetEditorWindow : Window
{
	private const string PixelSuffix = "px";
	private const double SlotWidth = 160;
	private const double SlotHeight = 180;
	private const double SlotMargin = 6;
	private const double SlotCornerRadius = 10;
	private const double SlotPreviewSize = 40;
	private const double DialogMargin = 16;
	private const double DialogSpacing = 12;
	private const double ButtonSpacing = 8;

	private const string LocEditorTitleNew = "S.Editor.TitleNew";
	private const string LocEditorTitleEdit = "S.Editor.TitleEdit";
	private const string LocEditorSave = "S.Editor.Save";
	private const string LocEditorCancel = "S.Editor.Cancel";
	private const string LocEditorBrowse = "S.Editor.Browse";
	private const string LocEditorBrowseFolder = "S.Editor.BrowseFolder";
	private const string LocEditorEmptySlot = "S.Editor.EmptySlot";
	private const string LocEditorPlaceholderBadge = "S.Editor.PlaceholderBadge";
	private const string LocEditorPresetName = "S.Editor.PresetName";
	private const string LocEditorNoFiles = "S.Editor.NoFiles";
	private const string LocDefaultPresetName = "S.DefaultPresetName";
	private const string LocCursorSize = "S.CursorSize";
	private const string LocApplySize = "S.ApplySize";

	private const string CursorFileFilterName = "Cursors";
	private const string EmptyValue = "";

	private readonly List<Slot> _slots = new();
	private readonly string? _draftId;
	private int _baseSize;

	public PresetDraft? Result { get; private set; }

	private readonly TextBox _nameBox;
	private readonly Slider _sizeSlider;
	private readonly TextBlock _sizeValueText;
	private readonly ItemsControl _slotsControl;

	public PresetEditorWindow(Preset? existing, IReadOnlyList<string> droppedFiles, string? suggestedName = null)
	{
		Title = Loc.Get(existing == null ? LocEditorTitleNew : LocEditorTitleEdit);
		Width = 760;
		Height = 640;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;

		_nameBox = new TextBox
		{
			Text = existing?.Name
				?? (string.IsNullOrWhiteSpace(suggestedName) ? null : suggestedName)
				?? Loc.Get(LocDefaultPresetName),
			MaxLength = 60,
			Watermark = Loc.Get(LocEditorPresetName),
		};

		_baseSize = existing?.BaseSize ?? AppState.GetDefaultBaseSize();

		_sizeSlider = new Slider
		{
			Minimum = 1,
			Maximum = 15,
			Value = (_baseSize - CursorConstants.SizeStep) / (double)CursorConstants.SizeStep,
			TickFrequency = 1,
			IsSnapToTickEnabled = true,
			MinWidth = 90,
			Margin = new Avalonia.Thickness(8, 0),
		};

		_sizeValueText = new TextBlock
		{
			Width = 46,
			VerticalAlignment = VerticalAlignment.Center,
			Text = $"{_baseSize} {PixelSuffix}",
		};

		_sizeSlider.PropertyChanged += (_, e) =>
		{
			if (e.Property == Slider.ValueProperty)
			{
				var sizePx = CursorConstants.SizeStep + (int)_sizeSlider.Value * CursorConstants.SizeStep;
				_sizeValueText.Text = $"{sizePx} {PixelSuffix}";
				_baseSize = sizePx;
			}
		};

		_slotsControl = new ItemsControl();

		var saveButton = new Button
		{
			Content = Loc.Get(LocEditorSave),
			MinWidth = 110,
		};

		var cancelButton = new Button
		{
			Content = Loc.Get(LocEditorCancel),
			MinWidth = 90,
			Margin = new Avalonia.Thickness(12, 0, 8, 0),
		};

		saveButton.Click += OnSaveClick;
		cancelButton.Click += (_, _) => Close();

		var bottomBar = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Spacing = ButtonSpacing,
			Margin = new Avalonia.Thickness(DialogMargin),
		};
		bottomBar.Children.Add(new TextBlock
		{
			Text = Loc.Get(LocEditorPresetName),
			Foreground = Brushes.Gray,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Avalonia.Thickness(0, 0, 8, 0),
		});
		bottomBar.Children.Add(_nameBox);
		bottomBar.Children.Add(cancelButton);
		bottomBar.Children.Add(saveButton);

		var sizeBar = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Left,
			Margin = new Avalonia.Thickness(DialogMargin),
			Spacing = DialogSpacing,
		};
		sizeBar.Children.Add(new TextBlock
		{
			Text = Loc.Get(LocCursorSize),
			Foreground = Brushes.Gray,
			VerticalAlignment = VerticalAlignment.Center,
		});
		sizeBar.Children.Add(_sizeSlider);
		sizeBar.Children.Add(_sizeValueText);

		var scrollViewer = new ScrollViewer
		{
			Content = _slotsControl,
			Padding = new Avalonia.Thickness(10, 4),
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
		};

		var rootPanel = new Grid();
		rootPanel.RowDefinitions.Add(new RowDefinition(0, GridUnitType.Auto));
		rootPanel.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
		rootPanel.RowDefinitions.Add(new RowDefinition(0, GridUnitType.Auto));
		rootPanel.RowDefinitions.Add(new RowDefinition(0, GridUnitType.Auto));

		Grid.SetRow(sizeBar, 0);
		Grid.SetRow(scrollViewer, 1);
		Grid.SetRow(bottomBar, 3);
		rootPanel.Children.Add(sizeBar);
		rootPanel.Children.Add(scrollViewer);
		rootPanel.Children.Add(bottomBar);

		Content = rootPanel;

		_draftId = existing?.Id;

		BuildSlots(existing, droppedFiles);
	}

	private sealed class Slot
	{
		public required CursorRoleInfo Role { get; init; }
		public string? SourcePath { get; set; }
		public required Image PreviewImage { get; init; }
		public required TextBlock FileText { get; init; }
		public required Button BrowseButton { get; init; }
		public required Button PaintButton { get; init; }
		public required Button ClearButton { get; init; }
		public required Border Container { get; init; }
	}

	private void BuildSlots(Preset? existing, IReadOnlyList<string> droppedFiles)
	{
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

			_slotsControl.Items.Add(slot.Container);
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

	private Slot CreateSlot(CursorRoleInfo role)
	{
		var preview = new Image
		{
			Width = SlotPreviewSize,
			Height = SlotPreviewSize,
		};

		var roleName = new TextBlock
		{
			Text = Loc.Get("S." + role.DisplayKey),
			FontWeight = FontWeight.SemiBold,
			TextAlignment = TextAlignment.Center,
			TextWrapping = TextWrapping.Wrap,
			FontSize = 12,
			Margin = new Avalonia.Thickness(4, 6, 4, 0),
		};

		var fileText = new TextBlock
		{
			Text = Loc.Get(LocEditorEmptySlot),
			Foreground = Brushes.Gray,
			FontSize = 11,
			TextAlignment = TextAlignment.Center,
			TextTrimming = TextTrimming.CharacterEllipsis,
			Margin = new Avalonia.Thickness(4, 2, 4, 0),
			MaxWidth = SlotWidth - 20,
		};

		var browseButton = new Button
		{
			Content = Loc.Get(LocEditorBrowse),
			FontSize = 11,
			Padding = new Avalonia.Thickness(6, 3),
			Margin = new Avalonia.Thickness(0, 6, 0, 0),
			HorizontalAlignment = HorizontalAlignment.Center,
		};

		var paintButton = new Button
		{
			Content = "Paint",
			FontSize = 11,
			Padding = new Avalonia.Thickness(6, 3),
			Margin = new Avalonia.Thickness(0, 4, 0, 0),
			HorizontalAlignment = HorizontalAlignment.Center,
		};

		var clearButton = new Button
		{
			Content = "✕",
			FontSize = 11,
			Width = 22,
			Height = 22,
			Padding = new Avalonia.Thickness(0),
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Avalonia.Thickness(0, 6, 6, 0),
			IsVisible = false,
		};

		var panel = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		panel.Children.Add(preview);
		panel.Children.Add(roleName);
		panel.Children.Add(fileText);
		panel.Children.Add(browseButton);
		panel.Children.Add(paintButton);

		var slotContent = new Grid();
		slotContent.Children.Add(panel);
		slotContent.Children.Add(clearButton);

		var border = new Border
		{
			Width = SlotWidth,
			Height = SlotHeight,
			Margin = new Avalonia.Thickness(SlotMargin),
			CornerRadius = new Avalonia.CornerRadius(SlotCornerRadius),
			BorderThickness = new Avalonia.Thickness(2),
			BorderBrush = Brushes.DarkGray,
			Background = Brushes.Transparent,
			Child = slotContent,
		};

		var slot = new Slot
		{
			Role = role,
			PreviewImage = preview,
			FileText = fileText,
			BrowseButton = browseButton,
			PaintButton = paintButton,
			ClearButton = clearButton,
			Container = border,
		};

		browseButton.Click += async (_, _) => await BrowseForSlot(slot);
		paintButton.Click += (_, _) => OpenPaintEditor(slot);
		clearButton.Click += (_, _) => ClearSlot(slot);

		return slot;
	}

	private void OpenPaintEditor(Slot slot)
	{
		var source = slot.SourcePath != null ? CursorCanvasService.TryRead(slot.SourcePath) : null;
		var editor = new PaintEditorWindow(source);
		editor.ShowDialog(this);
		editor.Closed += (_, _) =>
		{
			if (editor.Result == null)
				return;

			var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cursor-palette-paint");
			System.IO.Directory.CreateDirectory(tempDir);
			var fileName = $"{slot.Role.RegistryName}_{DateTime.Now:yyyyMMddHHmmss}.cur";
			var tempPath = System.IO.Path.Combine(tempDir, fileName);

			try
			{
				CursorCanvasService.Write(tempPath, editor.Result);
				SetSlotSource(slot, tempPath);
			}
			catch
			{
			}
		};
	}

	private async Task BrowseForSlot(Slot slot)
	{
		var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
		{
			Title = Loc.Get(LocEditorBrowse),
			AllowMultiple = false,
			FileTypeFilter = new[]
			{
				new FilePickerFileType(CursorFileFilterName)
				{
					Patterns = new[] { "*.cur", "*.ani", "*.png", "*.jpg", "*.bmp", "*.gif" },
				},
			},
		});

		if (files.Count == 0)
			return;

		SetSlotSource(slot, files[0].Path.LocalPath);
	}

	private void SetSlotSource(Slot slot, string path)
	{
		slot.SourcePath = path;
		slot.FileText.Text = Path.GetFileName(path);
		slot.ClearButton.IsVisible = true;

		try
		{
			var preview = CursorPreviewService.GetPreview(path);
			if (preview != null)
				slot.PreviewImage.Source = preview;
		}
		catch
		{
		}
	}

	private void ClearSlot(Slot slot)
	{
		slot.SourcePath = null;
		slot.FileText.Text = Loc.Get(LocEditorEmptySlot);
		slot.ClearButton.IsVisible = false;
		slot.PreviewImage.Source = null;
	}

	private void OnSaveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		if (_slots.All(s => s.SourcePath == null))
		{
			// No files selected
			return;
		}

		var draft = new PresetDraft
		{
			Id = _draftId,
			Name = _nameBox.Text ?? EmptyValue,
			BaseSize = _baseSize,
		};

		foreach (var slot in _slots.Where(s => s.SourcePath != null))
			draft.RoleSources[slot.Role.RegistryName] = new RoleSourceDraft { OwnFilePath = slot.SourcePath };

		Result = draft;
		Close();
	}
}
