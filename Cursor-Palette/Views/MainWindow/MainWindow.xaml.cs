using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CursorPalette.Models;
using CursorPalette.Services;
using Microsoft.Win32;

namespace CursorPalette.Views;

public partial class MainWindow : Window
{
	private const string PixelSuffix = "px";
	private const string CurExtension = ".cur";
	private const string AniExtension = ".ani";
	private const string FooterFormat = "{0}  ·  v{1}  ·  {2}";
	private const string AddCellPlusText = "+";
	private const string EmptyValue = "";
	private const string FileSearchPattern = "*.*";
	private const string PresetDragFormatName = "CursorPalette.PresetId";
	private const string GroupDragFormatName = "CursorPalette.GroupId";
	private const double ReorderIndicatorWidth = 4;
	private const double ReorderRowGroupingTolerance = 4;

	private const string BrushTextDim = "Brush.TextDim";
	private const string BrushBg = "Brush.Bg";
	private const string BrushAccent = "Brush.Accent";
	private const string BrushBorder = "Brush.Border";
	private const string BrushSurface = "Brush.Surface";
	private const string BrushSurfaceHover = "Brush.SurfaceHover";

	private const string LocWindowsDefault = "S.WindowsDefault";
	private const string LocMenuEdit = "S.Menu.Edit";
	private const string LocMenuRename = "S.Menu.Rename";
	private const string LocMenuMoveLeft = "S.Menu.MoveLeft";
	private const string LocMenuMoveRight = "S.Menu.MoveRight";
	private const string LocMenuDownload = "S.Menu.Download";
	private const string LocMenuDelete = "S.Menu.Delete";
	private const string LocPresetContextHint = "S.Preset.ContextHint";
	private const string LocAddPreset = "S.AddPreset";
	private const string LocAddPresetHint = "S.AddPreset.Hint";
	private const string LocErrorApplyFailed = "S.Error.ApplyFailed";
	private const string LocErrorTitle = "S.Error.Title";
	private const string LocErrorSaveFailed = "S.Error.SaveFailed";
	private const string LocConfirmDeleteText = "S.ConfirmDelete.Text";
	private const string LocConfirmDeleteTitle = "S.ConfirmDelete.Title";
	private const string LocToastSaved = "S.Toast.Saved";
	private const string LocToastSizeApplied = "S.Toast.SizeApplied";
	private const string LocToastPresetDownloaded = "S.Toast.PresetDownloaded";
	private const string LocDefaultPresetName = "S.DefaultPresetName";
	private const string LocToastUpdateAvailable = "S.Toast.UpdateAvailable";
	private const string LocToastImported = "S.Toast.Imported";
	private const string LocImportFileFilter = "S.Import.FileFilter";
	private const string LocErrorImportUnrecognized = "S.Error.ImportUnrecognized";
	private const string LocErrorImportVersionUnsupported = "S.Error.ImportVersionUnsupported";

	private const string SpinnerStoryboardKey = "SpinnerStoryboard";
	private const string UpdateSpinnerStoryboardKey = "UpdateSpinnerStoryboard";

	private const string StyleAccentButton = "Style.AccentButton";
	private const string StyleButton = "Style.Button";
	private const string StyleTextBox = "Style.TextBox";

	private const double CellSize = 148;
	private const double CellMargin = 6;
	private const double CellCornerRadius = 10;
	private const double CellBorderThickness = 2;
	private const double CellPreviewSize = 48;
	private const double CellNameFontSize = 13;
	private const double CellCountFontSize = 11;
	private const double CellSizeFontSize = 11;
	private const double AddCellPlusFontSize = 34;
	private const string MixedBadgeText = "🧩";
	private const double MixedBadgeFontSize = 15;
	private const string LocMixedBadgeTooltip = "S.Gallery.MixedBadgeTooltip";

	private const double GroupOutlineThickness = 1.5;
	private const double GroupOutlinePadding = 7;
	private const double GroupOutlineOpacity = 0.65;
	private const double SelectionBorderThickness = 4;
	private const string SelectionBadgeText = "✓";
	private const double SelectionBadgeSize = 20;
	private const double SelectionBadgeFontSize = 12;
	private const double GroupSwatchSize = 22;
	private const double GroupSwatchRingThickness = 2.5;
	private const double GroupDeckPeekOffsetX = 9;
	private const double GroupDeckPeekOffsetY = 6;
	private const int GroupDeckMaxPeek = 3;
	private const double GroupAttachZoneMargin = 0.25;
	private const string LocMenuRemoveFromGroup = "S.Menu.RemoveFromGroup";
	private const string LocMenuAssignToGroup = "S.Menu.AssignToGroup";
	private const string LocMenuEditGroup = "S.Menu.EditGroup";
	private const string LocMenuCreateGroup = "S.Menu.CreateGroup";
	private const string LocMenuUngroup = "S.Menu.Ungroup";
	private const string LocMenuDeleteGroup = "S.Menu.DeleteGroup";
	private const string LocMenuConsolidateGroup = "S.Menu.ConsolidateGroup";
	private const string LocGroupDefaultName = "S.Group.DefaultName";
	private const string LocGroupSelectedCount = "S.Group.SelectedCount";
	private const string LocGroupMembersCount = "S.Group.MembersCount";
	private const string LocGroupCollapsedTooltip = "S.Group.CollapsedTooltip";
	private const string LocGroupExpandedTooltip = "S.Group.ExpandedTooltip";
	private const string LocGroupToastCreated = "S.Group.Toast.Created";
	private const string LocGroupToastConsolidated = "S.Group.Toast.Consolidated";
	private const string LocGroupToastUngrouped = "S.Group.Toast.Ungrouped";
	private const string LocGroupToastDeleted = "S.Group.Toast.Deleted";
	private const string BrushText = "Brush.Text";

	private const string LocInfoTitle = "S.Info.Title";
	private const string LocInfoMain = "S.Info.Main";
	private const string LocErrorArchiveExtractFailed = "S.Error.ArchiveExtractFailed";

	private const double UiZoomStep = 0.1;
	private const string ThemeIconDark = "🌙";
	private const string ThemeIconLight = "☀";

	private sealed record BoardEntry(Preset? Preset, PresetGroup? Group, int BoardIndex);

	private List<Preset> _presets = new();
	private List<PresetGroup> _groups = new();
	private Dictionary<string, PresetGroup> _presetToGroup = new();
	private List<string> _boardOrderIds = new();
	private List<string> _visibleBoardIds = new();
	private readonly List<BoardEntry> _boardOrder = new();
	private readonly HashSet<string> _selectedPresetIds = new();
	private readonly List<Border> _groupColorSwatches = new();
	private string? _pendingGroupColorKey;
	private string? _activePresetId;
	private TextBlock? _activeCellSizeText;
	private double _cellScale = AppState.GalleryCellScaleDefault;
	private double _uiScale = AppState.UiScaleDefault;
	private bool _cellScaleReady;
	private int _baselineSizePx;
	private Point? _presetDragStartPoint;
	private bool _justDraggedPreset;
	private int? _pendingInsertIndex;
	private string? _pendingGroupAttachId;
	private string? _draggedPresetId;
	private string? _draggedGroupId;
	private bool _justDraggedGroup;

	public MainWindow()
	{
		InitializeComponent();

		Width = AppState.GetMainWindowWidth();
		Height = AppState.GetMainWindowHeight();

		_activePresetId = AppState.GetActivePresetId();

		_baselineSizePx = RegistryCursorService.GetBaseSize();
		SetSliderSilently(_baselineSizePx);

		_uiScale = AppState.GetUiScale();
		ApplyUiScale(_uiScale);

		_cellScale = AppState.GetGalleryCellScale();
		SetCellScaleSliderSilently(_cellScale);

		UpdateThemeToggleIcon();
		UpdateLanguageButtonText();

		UpdateOpenFolderToggleIcon();

		BuildGroupColorSwatches();
		ReloadGallery();
		UpdateUndoButton();

		var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? AppInfo.DefaultVersion;
		FooterRun.Text = string.Format(FooterFormat, AppInfo.Author, version, AppInfo.LicenseName);

		_ = CheckForUpdatesAsync(version);
	}

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		SingleInstanceService.ListenForActivation(this);
	}

	private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

	private double CellFontScale => Math.Sqrt(_cellScale);

	protected override void OnClosed(EventArgs e)
	{
		AppState.SetMainWindowSize(Width, Height);
		base.OnClosed(e);
	}
}

public sealed class RelayUiCommand(Action execute) : ICommand
{
	public event EventHandler? CanExecuteChanged { add { } remove { } }
	public bool CanExecute(object? parameter) => true;
	public void Execute(object? parameter) => execute();
}
