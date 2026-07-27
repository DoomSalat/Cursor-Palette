using System.Text.Json;
using CursorPalette.Models;

namespace CursorPalette.Services;

public static class PresetStore
{
	private const string FilesDirName = "files";
	private const string ManifestFileName = "manifest.json";
	private const string UntitledPresetName = "Без названия";
	private const string GuidFormat = "N";

	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

	public static string GetPresetDir(string id) => Path.Combine(PathProvider.Current.PresetsDir, id);
	public static string GetFilesDir(string id) => Path.Combine(GetPresetDir(id), FilesDirName);

	public static string? GetRoleFilePath(Preset preset, string registryName)
	{
		if (preset.RoleRefs.TryGetValue(registryName, out var reference))
			return Path.Combine(GetFilesDir(reference.PresetId), reference.FileName);

		return preset.Roles.TryGetValue(registryName, out var fileName)
			? Path.Combine(GetFilesDir(preset.Id), fileName)
			: null;
	}

	public static RoleRef? ResolveLeafRef(Preset source, string registryName)
	{
		if (source.RoleRefs.TryGetValue(registryName, out var existingRef))
			return existingRef;

		return source.Roles.TryGetValue(registryName, out var fileName)
			? new RoleRef { PresetId = source.Id, FileName = fileName }
			: null;
	}

	public static List<Preset> LoadAll()
	{
		var result = new List<Preset>();

		if (!Directory.Exists(PathProvider.Current.PresetsDir))
			return result;

		foreach (var directory in Directory.GetDirectories(PathProvider.Current.PresetsDir))
		{
			var manifestPath = Path.Combine(directory, ManifestFileName);

			if (!File.Exists(manifestPath))
				continue;

			try
			{
				var preset = JsonSerializer.Deserialize<Preset>(File.ReadAllText(manifestPath));
				if (preset != null) result.Add(preset);
			}
			catch
			{
			}
		}

		return result.OrderBy(preset => preset.SortOrder).ThenBy(preset => preset.CreatedAt).ToList();
	}

	public static Preset Save(PresetDraft draft)
	{
		var id = draft.Id ?? Guid.NewGuid().ToString(GuidFormat);
		var filesDir = GetFilesDir(id);
		Directory.CreateDirectory(filesDir);

		var allPresets = LoadAll();
		var existing = draft.Id != null ? allPresets.FirstOrDefault(preset => preset.Id == draft.Id) : null;

		var roles = new Dictionary<string, string>();
		var roleRefs = new Dictionary<string, RoleRef>();

		foreach (var (role, source) in draft.RoleSources)
		{
			if (source.Ref != null)
			{
				roleRefs[role] = source.Ref;
				continue;
			}

			if (source.OwnFilePath == null)
				continue;

			var sourcePath = source.OwnFilePath;
			var fileName = Path.GetFileName(sourcePath);
			var targetPath = Path.Combine(filesDir, fileName);

			if (!PathsEqual(sourcePath, targetPath))
			{
				if (File.Exists(targetPath) && !PathsEqual(sourcePath, targetPath) &&
					roles.ContainsValue(fileName))
				{
					fileName = $"{role}_{fileName}";
					targetPath = Path.Combine(filesDir, fileName);
				}

				File.Copy(sourcePath, targetPath, overwrite: true);
			}

			roles[role] = fileName;
		}

		var referenced = roles.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);

		foreach (var protectedFileName in allPresets
					.SelectMany(preset => preset.RoleRefs.Values)
					.Concat(roleRefs.Values)
					.Where(refEntry => refEntry.PresetId == id)
					.Select(refEntry => refEntry.FileName))
		{
			referenced.Add(protectedFileName);
		}

		foreach (var file in Directory.GetFiles(filesDir))
		{
			if (!referenced.Contains(Path.GetFileName(file)))
				TryDelete(file);
		}

		var sortOrder = existing?.SortOrder ?? (allPresets.Count == 0 ? 0 : allPresets.Max(preset => preset.SortOrder) + 1);

		var preset = new Preset
		{
			Id = id,
			Name = string.IsNullOrWhiteSpace(draft.Name) ? UntitledPresetName : draft.Name.Trim(),
			Author = string.IsNullOrWhiteSpace(draft.Author) ? null : draft.Author.Trim(),
			CreatedAt = existing?.CreatedAt ?? DateTime.Now,
			SortOrder = sortOrder,
			BaseSize = draft.BaseSize,
			UseScaling = draft.UseScaling,
			ScaleMode = draft.ScaleMode,
			Roles = roles,
			RoleRefs = roleRefs,
			LockedRoles = new HashSet<string>(draft.LockedRoles),
		};

		File.WriteAllText(Path.Combine(GetPresetDir(id), ManifestFileName),
			JsonSerializer.Serialize(preset, JsonOptions));

		return preset;
	}

	public static void UpdateBaseSize(string presetId, int sizeInPixels)
	{
		var manifestPath = Path.Combine(GetPresetDir(presetId), ManifestFileName);

		if (!File.Exists(manifestPath))
			return;

		try
		{
			var preset = JsonSerializer.Deserialize<Preset>(File.ReadAllText(manifestPath));

			if (preset == null)
				return;

			preset.BaseSize = sizeInPixels;
			File.WriteAllText(manifestPath, JsonSerializer.Serialize(preset, JsonOptions));
		}
		catch
		{
		}
	}

	public static void UpdateUseScaling(string presetId, bool useScaling)
	{
		var manifestPath = Path.Combine(GetPresetDir(presetId), ManifestFileName);

		if (!File.Exists(manifestPath))
			return;

		try
		{
			var preset = JsonSerializer.Deserialize<Preset>(File.ReadAllText(manifestPath));

			if (preset == null)
				return;

			preset.UseScaling = useScaling;
			File.WriteAllText(manifestPath, JsonSerializer.Serialize(preset, JsonOptions));
		}
		catch
		{
		}
	}

	public static void UpdateScaleMode(string presetId, ScaleMode scaleMode)
	{
		var manifestPath = Path.Combine(GetPresetDir(presetId), ManifestFileName);

		if (!File.Exists(manifestPath))
			return;

		try
		{
			var preset = JsonSerializer.Deserialize<Preset>(File.ReadAllText(manifestPath));

			if (preset == null)
				return;

			preset.ScaleMode = scaleMode;
			File.WriteAllText(manifestPath, JsonSerializer.Serialize(preset, JsonOptions));
		}
		catch
		{
		}
	}

	public static void Rename(string presetId, string newName)
	{
		var manifestPath = Path.Combine(GetPresetDir(presetId), ManifestFileName);

		if (!File.Exists(manifestPath))
			return;

		try
		{
			var preset = JsonSerializer.Deserialize<Preset>(File.ReadAllText(manifestPath));

			if (preset == null)
				return;

			preset.Name = string.IsNullOrWhiteSpace(newName) ? UntitledPresetName : newName.Trim();
			File.WriteAllText(manifestPath, JsonSerializer.Serialize(preset, JsonOptions));
		}
		catch
		{
		}
	}

	public static void Reorder(IReadOnlyList<string> orderedPresetIds)
	{
		for (var i = 0; i < orderedPresetIds.Count; i++)
		{
			var manifestPath = Path.Combine(GetPresetDir(orderedPresetIds[i]), ManifestFileName);

			if (!File.Exists(manifestPath))
				continue;

			try
			{
				var preset = JsonSerializer.Deserialize<Preset>(File.ReadAllText(manifestPath));

				if (preset == null)
					continue;

				preset.SortOrder = i;
				File.WriteAllText(manifestPath, JsonSerializer.Serialize(preset, JsonOptions));
			}
			catch
			{
			}
		}
	}

	public static void Delete(string presetId)
	{
		var directory = GetPresetDir(presetId);

		if (Directory.Exists(directory))
			Directory.Delete(directory, recursive: true);
	}

	private static bool PathsEqual(string leftPath, string rightPath) =>
		string.Equals(Path.GetFullPath(leftPath), Path.GetFullPath(rightPath), StringComparison.OrdinalIgnoreCase);

	private static void TryDelete(string path)
	{
		try
		{
			File.Delete(path);
		}
		catch
		{
		}
	}
}
