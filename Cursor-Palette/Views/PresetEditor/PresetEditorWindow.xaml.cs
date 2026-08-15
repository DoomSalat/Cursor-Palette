using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Ellipse = System.Windows.Shapes.Ellipse;
using Rectangle = System.Windows.Shapes.Rectangle;
using CursorPalette.Models;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class PresetEditorWindow : Window
{
	private sealed class Slot
	{
		public required CursorRoleInfo Role { get; init; }
		public required string? DefaultPath { get; init; }
		public string? SourcePath { get; set; }
		public string? RefPresetId { get; set; }
		public string? RefFileName { get; set; }
		public Image PreviewImage { get; init; } = null!;
		public TextBlock FileText { get; init; } = null!;
		public StackPanel FileNameRow { get; init; } = null!;
		public Button FileNameEditButton { get; init; } = null!;
		public Grid FileNameEditContainer { get; init; } = null!;
		public TextBox FileNameEditBox { get; init; } = null!;
		public TextBlock FileNamePlaceholder { get; init; } = null!;
		public Button ClearButton { get; init; } = null!;
		public Button PivotButton { get; init; } = null!;
		public Button PositionButton { get; init; } = null!;
		public Button LockButton { get; init; } = null!;
		public Rectangle LockIcon { get; init; } = null!;
		public Border DownloadButton { get; init; } = null!;
		public StackPanel PrimaryButtons { get; init; } = null!;
		public Rectangle DropIndicator { get; init; } = null!;
		public FrameworkElement PlaceholderBadge { get; init; } = null!;
		public FrameworkElement HotspotDot { get; init; } = null!;
		public FrameworkElement LinkBadge { get; init; } = null!;
		public bool IsLocked { get; set; }
	}

	private const string PixelSuffix = "px";
	private const string CurExtension = ".cur";
	private const string AniExtension = ".ani";
	private const double SlotWidth = 160;
	private const double SlotHeight = 204;
	private const double SlotMargin = 6;
	private const double SlotCornerRadius = 10;
	private const double SlotBorderThickness = 2;
	private const double SlotPreviewSize = 40;
	private const double RoleNameFontSize = 12;
	private const double FileTextFontSize = 11;
	private const double ButtonFontSize = 11;
	private const double PlaceholderBadgeFontSize = 9;
	private const double PlaceholderOpacity = 0.45;
	private const double HotspotDotSize = 8;
	private const string ClearButtonContent = "✕";
	private const string PivotButtonContent = "🎯";
	private const string PositionButtonContent = "🖌";
	private const string PickExistingButtonContent = "🧩";
	private const string LinkIconUri = "pack://application:,,,/Resources/LinkIcon32.png";
	private const double IconButtonSize = 28;
	private const double CornerBadgeSize = 22;
	private const double PreviewTopMargin = 10;
	private const double LinkBadgeIconSize = 16;
	private const string LockIconUri = "pack://application:,,,/Resources/LockIcon26.png";
	private const double LockIconSize = 14;
	private const string DownloadIconUri = "pack://application:,,,/Resources/DownloadIcon32.png";
	private const string ExpandIconUri = "pack://application:,,,/Resources/ExpandIcon32.png";
	private const string StairIconUri = "pack://application:,,,/Resources/StairIcon24.png";
	private const double DownloadIconSize = 16;
	private const double FileNameMaxWidth = 120;
	private const int FileNameEditMaxLength = 80;
	private const string TempRenameDirPrefix = "cursor-palette-rename-";

	private const string BrushAccent = "Brush.Accent";
	private const string BrushBorder = "Brush.Border";
	private const string BrushSurface = "Brush.Surface";
	private const string BrushSurfaceHover = "Brush.SurfaceHover";
	private const string BrushText = "Brush.Text";
	private const string BrushTextDim = "Brush.TextDim";
	private const string StyleAccentButton = "Style.AccentButton";
	private const string StyleButton = "Style.Button";
	private const string StyleDangerButton = "Style.DangerButton";
	private const string StylePencilButton = "Style.PencilButton";
	private const string StylePencilIcon = "Style.PencilIcon";
	private const string StyleTextBox = "Style.TextBox";

	private const string LocInfoTitle = "S.Info.Title";
	private const string LocEditorTitleNew = "S.Editor.TitleNew";
	private const string LocEditorTitleEdit = "S.Editor.TitleEdit";
	private const string LocDefaultPresetName = "S.DefaultPresetName";
	private const string LocEditorPlaceholderBadge = "S.Editor.PlaceholderBadge";
	private const string LocEditorEmptySlot = "S.Editor.EmptySlot";
	private const string LocEditorBrowse = "S.Editor.Browse";
	private const string LocEditorBrowseFolder = "S.Editor.BrowseFolder";
	private const string LocEditorPickExisting = "S.Editor.PickExisting";
	private const string LocEditorPivotTooltip = "S.Editor.Pivot.Tooltip";
	private const string LocEditorPivotDisabledTooltip = "S.Editor.Pivot.Disabled.Tooltip";
	private const string LocEditorPositionTooltip = "S.Editor.Position.Tooltip";
	private const string LocEditorPositionDisabledTooltip = "S.Editor.Position.Disabled.Tooltip";
	private const string LocEditorClearSlot = "S.Editor.ClearSlot";
	private const string LocEditorLockTooltip = "S.Editor.Lock.Tooltip";
	private const string LocEditorUnlockTooltip = "S.Editor.Unlock.Tooltip";
	private const string LocEditorDownloadTooltip = "S.Editor.Download.Tooltip";
	private const string LocEditorFileNameEditTooltip = "S.Editor.FileNameEdit.Tooltip";
	private const string LocEditorFileFilter = "S.Editor.FileFilter";
	private const string LocEditorLinkedRoleTooltip = "S.Editor.LinkedRole.Tooltip";
	private const string LocEditorNoCursorInFolder = "S.Editor.NoCursorInFolder";
	private const string LocEditorNoMatchInFolder = "S.Editor.NoMatchInFolder";
	private const string LocEditorNoFiles = "S.Editor.NoFiles";
	private const string LocEditorEmptyCursorWarning = "S.Editor.EmptyCursorWarning";
	private const string LocEditorEmptySkipped = "S.Editor.EmptySkipped";
	private const string LocToastSizeApplied = "S.Toast.SizeApplied";
	private const string LocToastDownloaded = "S.Toast.Downloaded";
	private const string LocToastPresetDownloaded = "S.Toast.PresetDownloaded";
	private const string LocErrorArchiveExtractFailed = "S.Error.ArchiveExtractFailed";
	private const string LocExportAsLinuxArchive = "S.Export.AsLinuxArchive";
	private const string LocExportAsXcursorTheme = "S.Export.AsXcursorTheme";
	private const string LocExportAsFullPackage = "S.Export.AsFullPackage";
	private const string LocToastExportedLinuxArchive = "S.Toast.ExportedLinuxArchive";
	private const string LocToastExportedXcursorTheme = "S.Toast.ExportedXcursorTheme";
	private const string LocToastExportedFullPackage = "S.Toast.ExportedFullPackage";
	private const string LocDownloadReadme = "S.Export.DownloadReadme";
	private const string LocDownloadReadmeTooltip = "S.Export.DownloadReadme.Tooltip";
	private const string LocToastReadmeDownloaded = "S.Toast.ReadmeDownloaded";

	private readonly List<Slot> _slots = new();

	public PresetDraft? Result { get; private set; }

	private readonly string? _draftId;
	private int _baseSize;
	private bool _useScaling;
	private ScaleMode _scaleMode = ScaleMode.AreaWeighted;
	private bool _sizeSliderReady;

	public PresetEditorWindow(Preset? existing, IReadOnlyList<string> droppedFiles, string? suggestedName = null)
	{
		InitializeComponent();

		Title = Loc.Get(existing == null ? LocEditorTitleNew : LocEditorTitleEdit);
		NameBox.Text = existing?.Name
			?? (string.IsNullOrWhiteSpace(suggestedName) ? null : suggestedName)
			?? Loc.Get(LocDefaultPresetName);
		AuthorDisplayText.Text = existing?.Author ?? "";

		Result = null;
		_draftId = existing?.Id;
		_baseSize = existing?.BaseSize ?? RegistryCursorService.GetBaseSize();
		_useScaling = existing?.UseScaling ?? true;
		_scaleMode = existing?.ScaleMode ?? ScaleMode.AreaWeighted;

		EditorSizeSlider.Value = (_baseSize - RegistryCursorService.SizeStep) / (double)RegistryCursorService.SizeStep;
		EditorSizeValueText.Text = $"{_baseSize} {PixelSuffix}";
		EditorUseScalingCheckBox.IsChecked = _useScaling;
		EditorUseScalingIcon.Source = new System.Windows.Media.Imaging.BitmapImage(
			new Uri(ExpandIconUri));

		UpdateEditorScaleIcon();
		_sizeSliderReady = true;

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
				{
					if (existing.RoleRefs.TryGetValue(role.RegistryName, out var reference))
						SetSlotReference(slot, reference.PresetId, reference.FileName);
					else
						SetSlotSource(slot, path);
				}

				if (existing.LockedRoles.Contains(role.RegistryName))
					SetSlotLocked(slot, true);
			}
		}

		var emptySkipped = 0;

		foreach (var file in droppedFiles)
		{
			var role = CursorRoles.MatchByFileName(file);

			if (role == null)
				continue;

			var cursorPath = ImageToCursorService.ConvertToCursorTempFile(file);
			if (cursorPath == null)
				continue;

			if (ImageToCursorService.IsFullyTransparent(cursorPath))
			{
				emptySkipped++;
				continue;
			}

			var slot = _slots.First(slot => slot.Role.RegistryName == role.RegistryName);

			SetSlotSource(slot, cursorPath);
		}

		if (emptySkipped > 0)
		{
			Dispatcher.BeginInvoke(new Action(() =>
				MessageBox.Show(Loc.Format(LocEditorEmptySkipped, emptySkipped), Title,
					MessageBoxButton.OK, MessageBoxImage.Information)),
				DispatcherPriority.Loaded);
		}
	}

	private void OnInfoButtonClick(object sender, RoutedEventArgs e)
	{
		new InfoHelpWindow(Loc.Get(LocInfoTitle), Services.HelpTextService.Get("Editor")) { Owner = this }.ShowDialog();
	}

	private void OnAuthorEditButtonClick(object sender, RoutedEventArgs e)
	{
		AuthorEditBox.Text = AuthorDisplayText.Text;
		AuthorDisplayPanel.Visibility = Visibility.Collapsed;
		AuthorEditBox.Visibility = Visibility.Visible;
		AuthorEditBox.Focus();
	}

	private void OnAuthorEditBoxKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Enter)
		{
			CommitAuthorEdit();
			e.Handled = true;
		}
		else if (e.Key == Key.Escape)
		{
			AuthorEditBox.Visibility = Visibility.Collapsed;
			AuthorDisplayPanel.Visibility = Visibility.Visible;
			e.Handled = true;
		}
	}

	private void OnAuthorEditBoxLostFocus(object sender, RoutedEventArgs e)
	{
		if (AuthorEditBox.Visibility == Visibility.Visible)
			CommitAuthorEdit();
	}

	private void CommitAuthorEdit()
	{
		AuthorDisplayText.Text = AuthorEditBox.Text;
		AuthorEditBox.Visibility = Visibility.Collapsed;
		AuthorDisplayPanel.Visibility = Visibility.Visible;
	}

	private void OnRootPreviewMouseDown(object sender, MouseButtonEventArgs e)
	{
		if (AuthorEditBox.Visibility == Visibility.Visible)
		{
			var hit = e.OriginalSource as DependencyObject;
			while (hit != null)
			{
				if (ReferenceEquals(hit, AuthorEditBox))
					break;
				hit = VisualTreeHelper.GetParent(hit);
			}

			if (hit == null)
				CommitAuthorEdit();
		}

		var editingSlot = _slots.FirstOrDefault(slot => slot.FileNameEditContainer.Visibility == Visibility.Visible);

		if (editingSlot != null)
		{
			var hit = e.OriginalSource as DependencyObject;
			var inside = false;
			while (hit != null)
			{
				if (ReferenceEquals(hit, editingSlot.FileNameEditBox))
				{
					inside = true;
					break;
				}
				hit = VisualTreeHelper.GetParent(hit);
			}

			if (!inside)
				CommitFileNameEdit(editingSlot);
		}
	}
}
