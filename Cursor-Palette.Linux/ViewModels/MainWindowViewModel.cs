using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CursorPalette.Linux.Services;
using CursorPalette.Models;
using CursorPalette.Services;

namespace CursorPalette.Linux.ViewModels;

public sealed class BoardItem
{
	public string Key { get; init; } = "";
	public string DisplayName { get; init; } = "";
	public bool IsPreset { get; init; }
	public bool IsGroup { get; init; }
	public bool IsAddCell { get; init; }
	public bool IsDefaultCell { get; init; }
	public string? DefaultThemeName { get; init; }
	public Preset? Preset { get; init; }
	public PresetGroup? Group { get; init; }
	public Bitmap? Preview { get; init; }
	public string? PreviewPath { get; init; }
	public int RoleCount { get; init; }
	public string MembersCountText { get; init; } = "";
	public string CollapsedText { get; init; } = "";
	public string ContextHint { get; init; } = "";
	public int BaseSize { get; init; }
	public bool UseScaling { get; init; }
	public ScaleMode ScaleMode { get; init; } = ScaleMode.AreaWeighted;
	public bool IsActive { get; init; }
	public bool IsSelected { get; init; }
	public bool IsMixed => Preset?.RoleRefs.Count > 0;
	public string? GroupColorHex { get; init; }
	public string? GroupId { get; init; }
	public bool IsCollapsed { get; init; }
}

public sealed class MainWindowViewModel : ViewModelBase
{
	private const string EmptyValue = "";
	private const string FooterFormat = "{0}  ·  v{1}  ·  {2}";
	private const string FileSearchPattern = "*.*";

	private const string LocWindowsDefault = "S.WindowsDefault";
	private const string LocAddPreset = "S.AddPreset";
	private const string LocDefaultPresetName = "S.DefaultPresetName";
	private const string LocErrorApplyFailed = "S.Error.ApplyFailed";
	private const string LocErrorSaveFailed = "S.Error.SaveFailed";
	private const string LocErrorImportVersionUnsupported = "S.Error.ImportVersionUnsupported";
	private const string LocToastImported = "S.Toast.Imported";
	private const string LocGroupMembersCount = "S.Group.MembersCount";
	private const string LocGroupCollapsed = "S.Group.Collapsed";

	private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".cur", ".ani", ".png", ".jpg", ".jpeg", ".bmp", ".gif"
	};

	private string? _activePresetId;
	private int _baselineSizePx;
	private double _cellScale = AppState.GalleryCellScaleDefault;
	private string _footerText = EmptyValue;
	private Dictionary<string, string>? _activeSourceValues;
	private bool _activeUseScaling;
	private ScaleMode _activeScaleMode = ScaleMode.AreaWeighted;
	private HashSet<string>? _selectedPresetIds;

	public Action<string>? ErrorOccurred { get; set; }
	public Action<string>? ToastRequested { get; set; }

	public ObservableCollection<BoardItem> Board { get; } = new();

	public string? ActivePresetId
	{
		get => _activePresetId;
		set => SetProperty(ref _activePresetId, value);
	}

	public int BaselineSizePx
	{
		get => _baselineSizePx;
		set => SetProperty(ref _baselineSizePx, value);
	}

	public double CellScale
	{
		get => _cellScale;
		set => SetProperty(ref _cellScale, value);
	}

	public string FooterText
	{
		get => _footerText;
		set => SetProperty(ref _footerText, value);
	}

	public void ReloadGallery()
	{
		var presets = PresetStore.LoadAll();
		var groups = GroupStore.LoadAll();
		var presetToGroup = groups
			.SelectMany(group => group.MemberPresetIds.Select(presetId => (presetId, group)))
			.GroupBy(entry => entry.presetId)
			.ToDictionary(entry => entry.Key, entry => entry.First().group);

		var boardOrderIds = ReconcileBoardOrder(BoardOrderStore.Load(), presets, groups, presetToGroup);
		BoardOrderStore.Save(boardOrderIds);

		var visibleIds = boardOrderIds.Where(id => IsBoardIdVisible(id, presetToGroup)).ToList();

		Board.Clear();

		if (_activePresetId != null && presets.All(preset => preset.Id != _activePresetId))
		{
			_activePresetId = null;
			AppState.SetActivePresetId(null);
		}

		foreach (var defaultCell in CreateDefaultCells())
			Board.Add(defaultCell);

		var presetsById = presets.ToDictionary(preset => preset.Id);
		var groupsById = groups.ToDictionary(group => group.Id);

		foreach (var id in boardOrderIds)
		{
			if (groupsById.TryGetValue(id, out var group))
			{
				Board.Add(CreateGroupCell(group));
				continue;
			}

			if (!presetsById.TryGetValue(id, out var preset))
				continue;

			if (presetToGroup.TryGetValue(preset.Id, out var owningGroup) && owningGroup.Collapsed)
				continue;

			Board.Add(CreatePresetCell(preset, presetToGroup.GetValueOrDefault(preset.Id)));
		}

		Board.Add(CreateAddCell());
	}

	private static List<string> ReconcileBoardOrder(List<string> persisted, List<Preset> presets,
		List<PresetGroup> groups, Dictionary<string, PresetGroup> presetToGroup)
	{
		var validIds = new HashSet<string>(presets.Select(preset => preset.Id));
		validIds.UnionWith(groups.Select(group => group.Id));

		var result = persisted.Where(validIds.Contains).ToList();
		var known = new HashSet<string>(result);
		var placedGroups = new HashSet<string>();

		foreach (var preset in presets)
		{
			if (presetToGroup.TryGetValue(preset.Id, out var group) && placedGroups.Add(group.Id) && known.Add(group.Id))
				result.Add(group.Id);

			if (known.Add(preset.Id))
				result.Add(preset.Id);
		}

		foreach (var group in groups)
		{
			if (known.Add(group.Id))
				result.Add(group.Id);
		}

		return result;
	}

	private static bool IsBoardIdVisible(string id, Dictionary<string, PresetGroup> presetToGroup)
	{
		if (presetToGroup.TryGetValue(id, out var group))
			return !group.Collapsed;

		return true;
	}

	private IEnumerable<BoardItem> CreateDefaultCells()
	{
		var cursorService = CursorServiceProvider.Current;
		var defaults = cursorService.GetDefaultValues();
		var previewPath = defaults.TryGetValue(CursorRoles.ArrowRoleName, out var arrow) ? arrow : null;
		var preview = CursorPreviewService.GetPreview(previewPath);
		var baseSize = AppState.GetDefaultBaseSize();

		var systemThemeName = (cursorService as LinuxCursorService)?.GetOriginalThemeName()
			?? LinuxCursorService.AdwaitaThemeName;
		var activeDefaultThemeName = (cursorService as LinuxCursorService)?.GetActiveDefaultThemeName();
		var isDefaultActive = _activePresetId == null;

		yield return new BoardItem
		{
			IsDefaultCell = true,
			Key = EmptyValue,
			DefaultThemeName = systemThemeName,
			DisplayName = Loc.Get(LocWindowsDefault),
			Preview = preview,
			PreviewPath = previewPath,
			BaseSize = baseSize,
			IsActive = isDefaultActive && string.Equals(activeDefaultThemeName, systemThemeName, StringComparison.OrdinalIgnoreCase),
		};

		if (!string.Equals(systemThemeName, LinuxCursorService.AdwaitaThemeName, StringComparison.OrdinalIgnoreCase))
		{
			yield return new BoardItem
			{
				IsDefaultCell = true,
				Key = EmptyValue,
				DefaultThemeName = LinuxCursorService.AdwaitaThemeName,
				DisplayName = LinuxCursorService.AdwaitaThemeName,
				Preview = preview,
				PreviewPath = previewPath,
				BaseSize = baseSize,
				IsActive = isDefaultActive && string.Equals(activeDefaultThemeName, LinuxCursorService.AdwaitaThemeName, StringComparison.OrdinalIgnoreCase),
			};
		}
	}

	private BoardItem CreatePresetCell(Preset preset, PresetGroup? group)
	{
		var isActive = preset.Id == _activePresetId;
		var previewPath = PresetStore.GetRoleFilePath(preset, CursorRoles.ArrowRoleName)
							?? preset.Roles.Keys.Concat(preset.RoleRefs.Keys)
								.Select(role => PresetStore.GetRoleFilePath(preset, role))
								.FirstOrDefault(path => path != null);

		return new BoardItem
		{
			IsPreset = true,
			Key = preset.Id,
			DisplayName = preset.Name,
			Preset = preset,
			Preview = CursorPreviewService.GetPreview(previewPath),
			PreviewPath = previewPath,
			RoleCount = preset.Roles.Count + preset.RoleRefs.Count,
			BaseSize = preset.BaseSize,
			UseScaling = preset.UseScaling,
			ScaleMode = preset.ScaleMode,
			IsActive = isActive,
			IsSelected = _selectedPresetIds?.Contains(preset.Id) ?? false,
			GroupColorHex = group != null ? GroupColors.ResolveHex(group.ColorKey) : null,
			GroupId = group?.Id,
			ContextHint = Loc.Get("S.Preset.ContextHint"),
		};
	}

	private BoardItem CreateGroupCell(PresetGroup group)
	{
		return new BoardItem
		{
			IsGroup = true,
			Key = group.Id,
			DisplayName = group.Name,
			Group = group,
			GroupColorHex = GroupColors.ResolveHex(group.ColorKey),
			IsCollapsed = group.Collapsed,
			RoleCount = group.MemberPresetIds.Count,
			MembersCountText = Loc.Format(LocGroupMembersCount, group.MemberPresetIds.Count),
			CollapsedText = Loc.Get(LocGroupCollapsed),
		};
	}

	private BoardItem CreateAddCell()
	{
		return new BoardItem
		{
			IsAddCell = true,
			DisplayName = Loc.Get(LocAddPreset),
		};
	}

	public void Initialize()
	{
		_activePresetId = AppState.GetActivePresetId();
		_baselineSizePx = CursorServiceProvider.Current.GetBaseSize();
		_cellScale = AppState.GetGalleryCellScale();

		var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? AppInfo.DefaultVersion;
		_footerText = string.Format(FooterFormat, AppInfo.Author, version, AppInfo.LicenseName);

		ReloadGallery();
	}

	public async Task ApplyPresetAsync(Preset preset, bool force = false)
	{
		if (!force && preset.Id == _activePresetId)
			return;

		try
		{
			var cursorService = CursorServiceProvider.Current;
			var useScaling = AppState.GetScaleCursorsEnabled() && preset.UseScaling;

			var values = new Dictionary<string, string>();
			foreach (var role in CursorRoles.All)
			{
				var path = PresetStore.GetRoleFilePath(preset, role.RegistryName);
				values[role.RegistryName] = path != null && File.Exists(path) ? path : EmptyValue;
			}

			await Task.Run(() =>
			{
				cursorService.SaveSnapshotToDisk(cursorService.TakeSnapshot());
				var scaledValues = useScaling
					? CursorScalerService.ScaleValues(values, preset.BaseSize, preset.ScaleMode)
					: values;
				cursorService.ApplyValues(scaledValues);
				cursorService.SetBaseSize(preset.BaseSize);
			});

			_activeSourceValues = values;
			_activeUseScaling = useScaling;
			_activeScaleMode = preset.ScaleMode;

			_baselineSizePx = preset.BaseSize;
			_activePresetId = preset.Id;
			AppState.SetActivePresetId(preset.Id);

			ReloadGallery();
		}
		catch (Exception ex)
		{
			ErrorOccurred?.Invoke(Loc.Format(LocErrorApplyFailed, ex.Message));
		}
	}

	public async Task ApplyDefaultAsync(string themeName)
	{
		var linuxCursorService = CursorServiceProvider.Current as LinuxCursorService;

		if (_activePresetId == null
			&& string.Equals(linuxCursorService?.GetActiveDefaultThemeName(), themeName, StringComparison.OrdinalIgnoreCase))
			return;

		try
		{
			var cursorService = CursorServiceProvider.Current;
			var defaultSize = AppState.GetDefaultBaseSize();
			var defaultUseScaling = AppState.GetScaleCursorsEnabled();
			var defaultScaleMode = AppState.GetScaleMode();
			var defaultValues = cursorService.GetDefaultValues();

			await Task.Run(() =>
			{
				cursorService.SaveSnapshotToDisk(cursorService.TakeSnapshot());
				linuxCursorService?.SetDefaultTheme(themeName);
				cursorService.SetBaseSize(defaultSize);
			});

			_activeSourceValues = defaultValues;
			_activeUseScaling = defaultUseScaling;
			_activeScaleMode = defaultScaleMode;

			_activePresetId = null;
			AppState.SetActivePresetId(null);
			linuxCursorService?.SetActiveDefaultThemeName(themeName);

			_baselineSizePx = defaultSize;

			ReloadGallery();
		}
		catch (Exception ex)
		{
			ErrorOccurred?.Invoke(Loc.Format(LocErrorApplyFailed, ex.Message));
		}
	}

	public async Task ImportCursorsAsync(string[] filePaths)
	{
		await HandleDroppedPathsAsync(filePaths);
	}

	public async Task HandleDroppedPathsAsync(string[] paths)
	{
		await Task.Run(() =>
		{
			var packagePath = paths.FirstOrDefault(path => File.Exists(path) && PresetPackageService.IsSupportedPackageFile(path));
			if (packagePath != null)
			{
				try
				{
					var detected = PresetPackageService.TryDetectPackage(packagePath);
					if (detected != null)
					{
						ImportAllFromPackage(detected);
						return;
					}
				}
				catch (PackageVersionUnsupportedException ex)
				{
					ErrorOccurred?.Invoke(Loc.Format(LocErrorImportVersionUnsupported, ex.FoundVersion, ex.MaxSupportedVersion));
					return;
				}
			}

			foreach (var folderPath in paths.Where(Directory.Exists))
			{
				var detectedFolder = PresetPackageService.TryDetectPackageFromFolder(folderPath);
				if (detectedFolder != null)
				{
					ImportAllFromPackage(detectedFolder);
					return;
				}
			}

			var files = ResolveCursorFiles(paths);
			if (files.Count == 0)
				return;

			CreatePresetFromFiles(files, GetSuggestedPresetName(paths));
		});
	}

	public void ImportAllFromPackage(DetectedPackage detected)
	{
		var allEntries = detected.Entries.ToList();
		var allGroups = detected.Groups?.ToList();

		try
		{
			PresetPackageService.ImportSelected(detected, allEntries, allGroups);
		}
		catch (PackageVersionUnsupportedException ex)
		{
			ErrorOccurred?.Invoke(Loc.Format(LocErrorImportVersionUnsupported, ex.FoundVersion, ex.MaxSupportedVersion));
			PresetPackageService.CleanupPackage(detected);
			return;
		}

		PresetPackageService.CleanupPackage(detected);
		ReloadGallery();

		ToastRequested?.Invoke(Loc.Format(LocToastImported, allEntries.Count));
	}

	private static List<string> ResolveCursorFiles(string[] paths)
	{
		var result = new List<string>();

		foreach (var path in paths)
		{
			if (File.Exists(path) && SupportedExtensions.Contains(Path.GetExtension(path)))
				result.Add(path);
			else if (Directory.Exists(path))
			{
				foreach (var file in Directory.GetFiles(path, FileSearchPattern, SearchOption.AllDirectories))
				{
					if (SupportedExtensions.Contains(Path.GetExtension(file)))
						result.Add(file);
				}
			}
			else if (File.Exists(path) && ArchiveImportService.IsArchiveFile(path))
			{
				var extractedDir = ArchiveImportService.ExtractToTempFolder(path);
				foreach (var file in Directory.GetFiles(extractedDir, FileSearchPattern, SearchOption.AllDirectories))
				{
					if (SupportedExtensions.Contains(Path.GetExtension(file)))
						result.Add(file);
				}
			}
		}

		return result;
	}

	private static string? GetSuggestedPresetName(string[] paths)
	{
		var folder = paths.FirstOrDefault(Directory.Exists);
		if (folder != null)
			return Path.GetFileName(folder);

		var archive = paths.FirstOrDefault(ArchiveImportService.IsArchiveFile);
		if (archive != null)
			return Path.GetFileNameWithoutExtension(archive);

		var file = paths.FirstOrDefault(File.Exists);

		return file != null ? Path.GetFileNameWithoutExtension(file) : null;
	}

	private void CreatePresetFromFiles(List<string> files, string? suggestedName)
	{
		var draft = new PresetDraft
		{
			Name = suggestedName ?? Loc.Get(LocDefaultPresetName),
			BaseSize = AppState.GetDefaultBaseSize(),
		};

		foreach (var file in files)
		{
			var role = CursorRoles.MatchByFileName(file);
			if (role == null)
				continue;

			draft.RoleSources[role.RegistryName] = new RoleSourceDraft { OwnFilePath = file };
		}

		if (draft.RoleSources.Count == 0)
			return;

		try
		{
			PresetStore.Save(draft);
			ReloadGallery();
		}
		catch (Exception ex)
		{
			ErrorOccurred?.Invoke(Loc.Format(LocErrorSaveFailed, ex.Message));
		}
	}

	public async Task UndoAsync()
	{
		var cursorService = CursorServiceProvider.Current;
		var snapshot = cursorService.LoadSnapshotFromDisk();

		if (snapshot == null)
			return;

		try
		{
			var undoUseScaling = AppState.GetScaleCursorsEnabled();
			var undoScaleMode = AppState.GetScaleMode();

			_activeSourceValues = new Dictionary<string, string>(snapshot.Values);
			_activePresetId = FindPresetIdByValues(snapshot.Values);

			var undoPreset = _activePresetId != null
				? PresetStore.LoadAll().FirstOrDefault(candidate => candidate.Id == _activePresetId)
				: null;
			var undoScaleModeToUse = undoPreset?.ScaleMode ?? undoScaleMode;

			await Task.Run(() =>
			{
				cursorService.SaveSnapshotToDisk(cursorService.TakeSnapshot());
				var scaledValues = undoUseScaling
					? CursorScalerService.ScaleValues(snapshot.Values, snapshot.BaseSize, undoScaleModeToUse)
					: snapshot.Values;
				cursorService.ApplyValues(scaledValues);
				cursorService.SetBaseSize(snapshot.BaseSize);
			});

			AppState.SetActivePresetId(_activePresetId);
			_baselineSizePx = snapshot.BaseSize;

			var undoEffectiveUseScaling = undoPreset != null ? undoUseScaling && undoPreset.UseScaling : undoUseScaling;
			_activeUseScaling = undoEffectiveUseScaling;
			_activeScaleMode = undoPreset?.ScaleMode ?? undoScaleMode;

			ReloadGallery();
		}
		catch (Exception ex)
		{
			ErrorOccurred?.Invoke(Loc.Format(LocErrorApplyFailed, ex.Message));
		}
	}

	public bool CanUndo => CursorServiceProvider.Current.LoadSnapshotFromDisk() != null;

	private static string? FindPresetIdByValues(IReadOnlyDictionary<string, string> values)
	{
		if (!values.TryGetValue(CursorRoles.ArrowRoleName, out var arrow) || string.IsNullOrEmpty(arrow))
			return null;

		return PresetStore.LoadAll().FirstOrDefault(preset =>
			string.Equals(PresetStore.GetRoleFilePath(preset, CursorRoles.ArrowRoleName), arrow,
				StringComparison.OrdinalIgnoreCase))?.Id;
	}

	public void DeletePreset(Preset preset)
	{
		var presetToGroup = GroupStore.LoadAll()
			.SelectMany(group => group.MemberPresetIds.Select(presetId => (presetId, group)))
			.GroupBy(entry => entry.presetId)
			.ToDictionary(entry => entry.Key, entry => entry.First().group);

		if (presetToGroup.TryGetValue(preset.Id, out var owningGroup))
			GroupStore.RemoveMember(owningGroup.Id, preset.Id);

		PresetStore.Delete(preset.Id);

		if (_activePresetId == preset.Id)
		{
			_activePresetId = null;
			AppState.SetActivePresetId(null);
		}

		ReloadGallery();
	}

	public void RenamePreset(Preset preset, string newName)
	{
		if (string.IsNullOrWhiteSpace(newName) || newName == preset.Name)
			return;

		PresetStore.Rename(preset.Id, newName);
		ReloadGallery();
	}

	public void MovePreset(Preset preset, int direction)
	{
		var boardOrderIds = BoardOrderStore.Load();
		var ownIndex = boardOrderIds.IndexOf(preset.Id);
		if (ownIndex < 0)
			return;

		var targetIndex = ownIndex + direction;
		if (targetIndex < 0 || targetIndex >= boardOrderIds.Count)
			return;

		(boardOrderIds[ownIndex], boardOrderIds[targetIndex]) = (boardOrderIds[targetIndex], boardOrderIds[ownIndex]);
		BoardOrderStore.Save(boardOrderIds);
		ReloadGallery();
	}

	public void AttachPresetToGroup(string presetId, string groupId)
	{
		var groups = GroupStore.LoadAll();
		var group = groups.FirstOrDefault(g => g.Id == groupId);
		if (group == null)
			return;

		var oldGroup = groups.FirstOrDefault(g => g.MemberPresetIds.Contains(presetId));
		if (oldGroup != null && oldGroup.Id != groupId)
			GroupStore.RemoveMember(oldGroup.Id, presetId);

		GroupStore.AddMember(groupId, presetId);

		var boardOrderIds = BoardOrderStore.Load();
		var groupIndex = boardOrderIds.IndexOf(groupId);
		if (groupIndex >= 0)
		{
			var presetIndex = boardOrderIds.IndexOf(presetId);
			if (presetIndex >= 0)
				boardOrderIds.RemoveAt(presetIndex);

			groupIndex = boardOrderIds.IndexOf(groupId);
			boardOrderIds.Insert(groupIndex + 1, presetId);
			BoardOrderStore.Save(boardOrderIds);
		}

		ReloadGallery();
	}

	public void ReorderPresetTo(string draggedId, string targetId)
	{
		if (draggedId == targetId)
			return;

		var boardOrderIds = BoardOrderStore.Load();
		var draggedIndex = boardOrderIds.IndexOf(draggedId);
		var targetIndex = boardOrderIds.IndexOf(targetId);

		if (draggedIndex < 0 || targetIndex < 0)
			return;

		boardOrderIds.RemoveAt(draggedIndex);
		targetIndex = boardOrderIds.IndexOf(targetId);
		boardOrderIds.Insert(targetIndex, draggedId);

		BoardOrderStore.Save(boardOrderIds);
		ReloadGallery();
	}

	public void ToggleGroupCollapse(string groupId)
	{
		var groups = GroupStore.LoadAll();
		var group = groups.FirstOrDefault(group => group.Id == groupId);
		if (group == null)
			return;

		GroupStore.SetCollapsed(groupId, !group.Collapsed);
		ReloadGallery();
	}

	public void DeleteGroup(string groupId)
	{
		GroupStore.Delete(groupId);
		ReloadGallery();
	}

	public void CreateGroup(string name, string colorKey)
	{
		var group = new PresetGroup
		{
			Id = Guid.NewGuid().ToString("N"),
			Name = name,
			ColorKey = colorKey,
			MemberPresetIds = new(),
			Collapsed = false,
		};

		GroupStore.Save(group);
		ReloadGallery();
	}

	public void CreateEmptyGroup(string name, string colorKey)
	{
		var group = new PresetGroup
		{
			Id = Guid.NewGuid().ToString("N"),
			Name = name,
			ColorKey = colorKey,
			MemberPresetIds = new(),
			Collapsed = false,
		};

		GroupStore.Save(group);

		var boardOrderIds = BoardOrderStore.Load();
		if (!boardOrderIds.Contains(group.Id))
			boardOrderIds.Add(group.Id);
		BoardOrderStore.Save(boardOrderIds);

		ReloadGallery();
	}

	public void EditGroup(string groupId, string name, string colorKey)
	{
		var groups = GroupStore.LoadAll();
		var group = groups.FirstOrDefault(group => group.Id == groupId);
		if (group == null)
			return;

		group.Name = name;
		group.ColorKey = colorKey;
		GroupStore.Save(group);
		ReloadGallery();
	}

	public void ConsolidateGroup(string groupId)
	{
		var groups = GroupStore.LoadAll();
		var group = groups.FirstOrDefault(g => g.Id == groupId);
		if (group == null)
			return;

		var boardOrderIds = BoardOrderStore.Load();
		var groupIndex = boardOrderIds.IndexOf(groupId);
		if (groupIndex < 0)
			return;

		var memberIds = boardOrderIds.Where(id => group.MemberPresetIds.Contains(id)).ToList();
		if (memberIds.Count == 0)
			return;

		boardOrderIds.RemoveAll(id => group.MemberPresetIds.Contains(id));
		groupIndex = boardOrderIds.IndexOf(groupId);
		boardOrderIds.InsertRange(groupIndex + 1, memberIds);

		BoardOrderStore.Save(boardOrderIds);
		ReloadGallery();
	}

	public void Ungroup(string groupId)
	{
		var groups = GroupStore.LoadAll();
		var group = groups.FirstOrDefault(g => g.Id == groupId);
		if (group == null)
			return;

		foreach (var presetId in group.MemberPresetIds.ToList())
			GroupStore.RemoveMember(groupId, presetId);

		ReloadGallery();
	}

	public void AssignToGroup(string presetId, string targetGroupId)
	{
		var groups = GroupStore.LoadAll();
		var currentGroup = groups.FirstOrDefault(g => g.MemberPresetIds.Contains(presetId));
		if (currentGroup != null)
			GroupStore.RemoveMember(currentGroup.Id, presetId);

		GroupStore.AddMember(targetGroupId, presetId);
		ReloadGallery();
	}

	public void RemoveFromGroup(string presetId, string groupId)
	{
		GroupStore.RemoveMember(groupId, presetId);
		ReloadGallery();
	}

	public void SetSelectedPresetIds(HashSet<string>? ids)
	{
		_selectedPresetIds = ids;
	}

	public void CreateGroupFromSelection(string name, string colorKey, List<string> memberIds)
	{
		var groups = GroupStore.LoadAll();
		var presetToGroup = groups
			.SelectMany(g => g.MemberPresetIds.Select(presetId => (presetId, g)))
			.GroupBy(entry => entry.presetId)
			.ToDictionary(entry => entry.Key, entry => entry.First().g);

		foreach (var presetId in memberIds)
		{
			if (presetToGroup.TryGetValue(presetId, out var oldGroup))
				GroupStore.RemoveMember(oldGroup.Id, presetId);
		}

		var group = new PresetGroup
		{
			Id = Guid.NewGuid().ToString("N"),
			Name = name,
			ColorKey = colorKey,
			MemberPresetIds = memberIds,
			Collapsed = false,
		};

		GroupStore.Save(group);

		var boardOrderIds = BoardOrderStore.Load();
		if (!boardOrderIds.Contains(group.Id))
			boardOrderIds.Add(group.Id);
		BoardOrderStore.Save(boardOrderIds);

		_selectedPresetIds = null;
		ReloadGallery();
	}

	public async Task ApplySizeAsync(int sizeInPixels, bool useScaling, ScaleMode scaleMode)
	{
		var cursorService = CursorServiceProvider.Current;

		if (_selectedPresetIds is { Count: > 0 })
		{
			foreach (var presetId in _selectedPresetIds)
			{
				PresetStore.UpdateBaseSize(presetId, sizeInPixels);
				PresetStore.UpdateUseScaling(presetId, useScaling);
				PresetStore.UpdateScaleMode(presetId, scaleMode);
			}
		}

		try
		{
			await Task.Run(() =>
			{
				if (_activeSourceValues != null)
				{
					var scaledValues = useScaling
						? CursorScalerService.ScaleValues(_activeSourceValues, sizeInPixels, scaleMode)
						: _activeSourceValues;
					cursorService.ApplyValues(scaledValues);
				}
				cursorService.SetBaseSize(sizeInPixels);
			});

			_baselineSizePx = sizeInPixels;
			_activeUseScaling = useScaling;
			_activeScaleMode = scaleMode;
			RaisePropertyChanged(nameof(BaselineSizePx));

			if (_selectedPresetIds is { Count: > 0 })
				ReloadGallery();
		}
		catch (Exception ex)
		{
			ErrorOccurred?.Invoke(Loc.Format(LocErrorApplyFailed, ex.Message));
		}
	}

	public bool GetActiveUseScaling()
	{
		if (_activePresetId != null)
		{
			var preset = PresetStore.LoadAll().FirstOrDefault(p => p.Id == _activePresetId);
			return preset?.UseScaling ?? AppState.GetScaleCursorsEnabled();
		}

		return AppState.GetScaleCursorsEnabled();
	}

	public ScaleMode GetActiveScaleMode()
	{
		if (_activePresetId != null)
		{
			var preset = PresetStore.LoadAll().FirstOrDefault(p => p.Id == _activePresetId);
			return preset?.ScaleMode ?? AppState.GetScaleMode();
		}

		return AppState.GetScaleMode();
	}

	public void ToggleScaleMode()
	{
		_activeScaleMode = _activeScaleMode == ScaleMode.NearestNeighbor
			? ScaleMode.AreaWeighted
			: ScaleMode.NearestNeighbor;

		if (_selectedPresetIds is { Count: > 0 })
		{
			foreach (var presetId in _selectedPresetIds)
				PresetStore.UpdateScaleMode(presetId, _activeScaleMode);
		}

		if (_activePresetId != null)
		{
			PresetStore.UpdateScaleMode(_activePresetId, _activeScaleMode);

			var preset = PresetStore.LoadAll().FirstOrDefault(p => p.Id == _activePresetId);
			if (preset != null)
				preset.ScaleMode = _activeScaleMode;
		}
		else if (_selectedPresetIds is null || _selectedPresetIds.Count == 0)
		{
			AppState.SetScaleMode(_activeScaleMode);
		}

		if (_selectedPresetIds is { Count: > 0 })
			ReloadGallery();
	}

	private static readonly Random RandomPicker = new();

	public async Task ApplyRandomFromGroupAsync(PresetGroup group)
	{
		var presetsById = PresetStore.LoadAll().ToDictionary(p => p.Id);
		var members = group.MemberPresetIds
			.Where(id => presetsById.ContainsKey(id))
			.Select(id => presetsById[id])
			.Where(p => p.Id != _activePresetId)
			.ToList();

		if (members.Count == 0)
			return;

		var picked = members[RandomPicker.Next(members.Count)];
		await ApplyPresetAsync(picked, force: true);
	}

	public async Task ApplyRandomFromBoardAsync()
	{
		var candidates = PresetStore.LoadAll().Where(p => p.Id != _activePresetId).ToList();

		if (candidates.Count == 0)
			return;

		var picked = candidates[RandomPicker.Next(candidates.Count)];
		await ApplyPresetAsync(picked, force: true);
	}
}
