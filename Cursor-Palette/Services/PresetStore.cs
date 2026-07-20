using System.Text.Json;
using CursorPalette.Models;

namespace CursorPalette.Services;

public static class PresetStore
{
	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

	public static string GetPresetDir(string id) => Path.Combine(AppPaths.PresetsDir, id);
	public static string GetFilesDir(string id) => Path.Combine(GetPresetDir(id), Constants.Paths.FilesDirName);

	public static string? GetRoleFilePath(Preset preset, string registryName) =>
		preset.Roles.TryGetValue(registryName, out var fileName)
			? Path.Combine(GetFilesDir(preset.Id), fileName)
			: null;

	public static List<Preset> LoadAll()
	{
		var result = new List<Preset>();

		if (!Directory.Exists(AppPaths.PresetsDir))
			return result;

		foreach (var dir in Directory.GetDirectories(AppPaths.PresetsDir))
		{
			var manifestPath = Path.Combine(dir, Constants.Paths.ManifestFileName);

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

		return result.OrderBy(p => p.CreatedAt).ToList();
	}

	public static Preset Save(PresetDraft draft)
	{
		var id = draft.Id ?? Guid.NewGuid().ToString(Constants.Defaults.GuidFormat);
		var filesDir = GetFilesDir(id);
		Directory.CreateDirectory(filesDir);

		var existing = draft.Id != null ? LoadAll().FirstOrDefault(p => p.Id == draft.Id) : null;

		var roles = new Dictionary<string, string>();
		foreach (var (role, sourcePath) in draft.RoleSources)
		{
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
		foreach (var file in Directory.GetFiles(filesDir))
			if (!referenced.Contains(Path.GetFileName(file)))
				TryDelete(file);

		var preset = new Preset
		{
			Id = id,
			Name = string.IsNullOrWhiteSpace(draft.Name) ? Constants.Defaults.UntitledPresetName : draft.Name.Trim(),
			CreatedAt = existing?.CreatedAt ?? DateTime.Now,
			Roles = roles,
		};

		File.WriteAllText(Path.Combine(GetPresetDir(id), Constants.Paths.ManifestFileName),
			JsonSerializer.Serialize(preset, JsonOptions));
		return preset;
	}

	public static void Delete(string id)
	{
		var dir = GetPresetDir(id);
		if (Directory.Exists(dir))
			Directory.Delete(dir, recursive: true);
	}

	private static bool PathsEqual(string a, string b) =>
		string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

	private static void TryDelete(string path)
	{
		try { File.Delete(path); } catch { }
	}
}
