namespace CursorPalette;

public static class Constants
{
	public static class App
	{
		public const string AppDataFolderName = "Cursor-Palette";
	}

	public static class Paths
	{
		public const string PresetsDirName = "presets";
		public const string StateDirName = "state";
		public const string FilesDirName = "files";
		public const string ManifestFileName = "manifest.json";
		public const string ActiveStateFileName = "active.json";
		public const string SettingsFileName = "settings.json";
		public const string PreviousSnapshotFileName = "previous.json";
	}

	public static class Registry
	{
		public const string SchemeSourceName = "Scheme Source";
		public const int SchemeSourceUserDefined = 2;
		public const string CursorBaseSizeName = "CursorBaseSize";
		public const string CursorSizeName = "CursorSize";
		public const string WindowsDefaultSchemeName = "Windows Default";
		public const char SchemePathSeparator = ',';
	}

	public static class Cursor
	{
		public const int SizeStep = 16;
		public const int TotalRoles = 17;
		public const string ArrowRoleName = "Arrow";
		public const string SystemRootVar = "%SystemRoot%";
		public const string CursorsSubdir = "cursors";
	}

	public static class Resources
	{
		public const string BrushBg = "Brush.Bg";
		public const string BrushSurface = "Brush.Surface";
		public const string BrushSurfaceHover = "Brush.SurfaceHover";
		public const string BrushBorder = "Brush.Border";
		public const string BrushAccent = "Brush.Accent";
		public const string BrushAccentHover = "Brush.AccentHover";
		public const string BrushText = "Brush.Text";
		public const string BrushTextDim = "Brush.TextDim";
		public const string BrushDanger = "Brush.Danger";
		public const string StyleButton = "Style.Button";
		public const string StyleAccentButton = "Style.AccentButton";
		public const string StyleTextBox = "Style.TextBox";
	}

	public static class Strings
	{
		public const string Prefix = "S.";
		public const string AppTitle = "S.AppTitle";
		public const string ResetDefault = "S.ResetDefault";
		public const string ResetDefaultTooltip = "S.ResetDefault.Tooltip";
		public const string Undo = "S.Undo";
		public const string UndoTooltip = "S.Undo.Tooltip";
		public const string CursorSize = "S.CursorSize";
		public const string ApplySize = "S.ApplySize";
		public const string ApplySizeTooltip = "S.ApplySize.Tooltip";
		public const string AddPreset = "S.AddPreset";
		public const string AddPresetHint = "S.AddPreset.Hint";
		public const string EmptyGallery = "S.EmptyGallery";
		public const string MenuEdit = "S.Menu.Edit";
		public const string MenuDelete = "S.Menu.Delete";
		public const string ConfirmDeleteTitle = "S.ConfirmDelete.Title";
		public const string ConfirmDeleteText = "S.ConfirmDelete.Text";
		public const string EditorTitleNew = "S.Editor.TitleNew";
		public const string EditorTitleEdit = "S.Editor.TitleEdit";
		public const string EditorPresetName = "S.Editor.PresetName";
		public const string EditorSave = "S.Editor.Save";
		public const string EditorCancel = "S.Editor.Cancel";
		public const string EditorBrowse = "S.Editor.Browse";
		public const string EditorClearSlot = "S.Editor.ClearSlot";
		public const string EditorEmptySlot = "S.Editor.EmptySlot";
		public const string EditorSlotHint = "S.Editor.SlotHint";
		public const string EditorNoFiles = "S.Editor.NoFiles";
		public const string EditorFileFilter = "S.Editor.FileFilter";
		public const string EditorBrowseFolder = "S.Editor.BrowseFolder";
		public const string EditorNoCursorInFolder = "S.Editor.NoCursorInFolder";
		public const string EditorNoMatchInFolder = "S.Editor.NoMatchInFolder";
		public const string DefaultPresetName = "S.DefaultPresetName";
		public const string WindowsDefault = "S.WindowsDefault";
		public const string ErrorTitle = "S.Error.Title";
		public const string ErrorApplyFailed = "S.Error.ApplyFailed";
		public const string RolePrefix = "S.Role.";
	}

	public static class UI
	{
		public const string PixelSuffix = "px";

		public static class Cell
		{
			public const double Size = 148;
			public const double Margin = 6;
			public const double CornerRadius = 10;
			public const double BorderThickness = 2;
			public const double PreviewSize = 48;
			public const double CountFontSize = 11;
			public const double SizeFontSize = 11;
		}

		public static class AddCell
		{
			public const double PlusFontSize = 34;
		}

		public static class Editor
		{
			public const double SlotWidth = 160;
			public const double SlotHeight = 156;
			public const double SlotMargin = 6;
			public const double SlotCornerRadius = 10;
			public const double SlotBorderThickness = 2;
			public const double SlotPreviewSize = 40;
			public const double RoleNameFontSize = 12;
			public const double FileTextFontSize = 11;
			public const double ButtonFontSize = 11;
			public const int MaxNameLength = 60;
			public const string ClearButtonContent = "✕";
		}
	}

	public static class Files
	{
		public const string CurExtension = ".cur";
		public const string AniExtension = ".ani";
		public const string User32Dll = "user32.dll";
	}

	public static class Defaults
	{
		public const string UntitledPresetName = "Без названия";
		public const string GuidFormat = "N";
	}
}
