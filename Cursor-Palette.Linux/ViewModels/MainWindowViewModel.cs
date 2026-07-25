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
	public Preset? Preset { get; init; }
	public PresetGroup? Group { get; init; }
	public Bitmap? Preview { get; init; }
	public int RoleCount { get; init; }
	public string MembersCountText { get; init; } = "";
	public string CollapsedText { get; init; } = "";
	public int BaseSize { get; init; }
	public bool UseScaling { get; init; }
	public bool IsActive { get; init; }
	public bool IsSelected { get; init; }
	public bool IsMixed => Preset?.RoleRefs.Count > 0;
	public string? GroupColorHex { get; init; }
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

		Board.Add(CreateDefaultCell());

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

	private BoardItem CreateDefaultCell()
	{
		var isActive = _activePresetId == null;
		var cursorService = CursorServiceProvider.Current;
		var defaults = cursorService.GetDefaultValues();
		var previewPath = defaults.TryGetValue(CursorRoles.ArrowRoleName, out var arrow) ? arrow : null;

		return new BoardItem
		{
			IsDefaultCell = true,
			Key = EmptyValue,
			DisplayName = Loc.Get(LocWindowsDefault),
			Preview = CursorPreviewService.GetPreview(previewPath),
			BaseSize = AppState.GetDefaultBaseSize(),
			IsActive = isActive,
		};
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
			RoleCount = preset.Roles.Count + preset.RoleRefs.Count,
			BaseSize = preset.BaseSize,
			UseScaling = preset.UseScaling,
			IsActive = isActive,
			GroupColorHex = group != null ? GroupColors.ResolveHex(group.ColorKey) : null,
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
					? CursorScalerService.ScaleValues(values, preset.BaseSize)
					: values;
				cursorService.ApplyValues(scaledValues);
				cursorService.SetBaseSize(preset.BaseSize);
			});

			_activeSourceValues = values;
			_activeUseScaling = useScaling;

			_baselineSizePx = preset.BaseSize;
			_activePresetId = preset.Id;
			AppState.SetActivePresetId(preset.Id);

			ReloadGallery();
		}
		catch (Exception)
		{
			// TODO: Show error dialog
		}
	}

	public async Task ApplyDefaultAsync()
	{
		if (_activePresetId == null)
			return;

		try
		{
			var cursorService = CursorServiceProvider.Current;
			var defaultSize = AppState.GetDefaultBaseSize();
			var defaultUseScaling = AppState.GetScaleCursorsEnabled();
			var defaultValues = cursorService.GetDefaultValues();

			await Task.Run(() =>
			{
				cursorService.SaveSnapshotToDisk(cursorService.TakeSnapshot());
				var scaledValues = defaultUseScaling
					? CursorScalerService.ScaleValues(defaultValues, defaultSize)
					: defaultValues;
				if (scaledValues.Count > 0)
					cursorService.ApplyValues(scaledValues);
				else
					cursorService.ResetToDefault();
				cursorService.SetBaseSize(defaultSize);
			});

			_activeSourceValues = defaultValues;
			_activeUseScaling = defaultUseScaling;

			_activePresetId = null;
			AppState.SetActivePresetId(null);

			_baselineSizePx = defaultSize;

			ReloadGallery();
		}
		catch (Exception)
		{
			// TODO: Show error dialog
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
				catch (PackageVersionUnsupportedException)
				{
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

		PresetPackageService.ImportSelected(detected, allEntries, allGroups);
		PresetPackageService.CleanupPackage(detected);
		ReloadGallery();
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
		}

		return result;
	}

	private static string? GetSuggestedPresetName(string[] paths)
	{
		if (paths.Length == 1)
		{
			if (File.Exists(paths[0]))
				return Path.GetFileNameWithoutExtension(paths[0]);

			if (Directory.Exists(paths[0]))
				return Path.GetFileName(paths[0]);
		}

		return null;
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
		catch
		{
			// TODO: Show error dialog
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

			await Task.Run(() =>
			{
				cursorService.SaveSnapshotToDisk(cursorService.TakeSnapshot());
				var scaledValues = undoUseScaling
					? CursorScalerService.ScaleValues(snapshot.Values, snapshot.BaseSize)
					: snapshot.Values;
				cursorService.ApplyValues(scaledValues);
				cursorService.SetBaseSize(snapshot.BaseSize);
			});

			_activeSourceValues = new Dictionary<string, string>(snapshot.Values);

			_activePresetId = FindPresetIdByValues(snapshot.Values);
			AppState.SetActivePresetId(_activePresetId);
			_baselineSizePx = snapshot.BaseSize;

			var undoPreset = _activePresetId != null
				? PresetStore.LoadAll().FirstOrDefault(candidate => candidate.Id == _activePresetId)
				: null;
			var undoEffectiveUseScaling = undoPreset != null ? undoUseScaling && undoPreset.UseScaling : undoUseScaling;
			_activeUseScaling = undoEffectiveUseScaling;

			ReloadGallery();
		}
		catch
		{
			// TODO: Show error dialog
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

	public async Task ApplySizeAsync(int sizeInPixels, bool useScaling)
	{
		var cursorService = CursorServiceProvider.Current;

		try
		{
			await Task.Run(() =>
			{
				if (_activeSourceValues != null)
				{
					var scaledValues = useScaling
						? CursorScalerService.ScaleValues(_activeSourceValues, sizeInPixels)
						: _activeSourceValues;
					cursorService.ApplyValues(scaledValues);
				}
				cursorService.SetBaseSize(sizeInPixels);
			});

			_baselineSizePx = sizeInPixels;
			_activeUseScaling = useScaling;
			RaisePropertyChanged(nameof(BaselineSizePx));
		}
		catch
		{
			// TODO: Show error dialog
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
}
