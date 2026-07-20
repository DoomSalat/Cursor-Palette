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

	public static string GetPresetDir(string id) => Path.Combine(AppPaths.PresetsDir, id);
	public static string GetFilesDir(string id) => Path.Combine(GetPresetDir(id), FilesDirName);

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
			var manifestPath = Path.Combine(dir, ManifestFileName);

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
		var id = draft.Id ?? Guid.NewGuid().ToString(GuidFormat);
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
		{
			if (!referenced.Contains(Path.GetFileName(file)))
				TryDelete(file);
		}

		var preset = new Preset
		{
			Id = id,
			Name = string.IsNullOrWhiteSpace(draft.Name) ? UntitledPresetName : draft.Name.Trim(),
			CreatedAt = existing?.CreatedAt ?? DateTime.Now,
			BaseSize = draft.BaseSize,
			Roles = roles,
		};

		File.WriteAllText(Path.Combine(GetPresetDir(id), ManifestFileName),
			JsonSerializer.Serialize(preset, JsonOptions));

		return preset;
	}

	public static void UpdateBaseSize(string id, int sizePx)
	{
		var manifestPath = Path.Combine(GetPresetDir(id), ManifestFileName);

		if (!File.Exists(manifestPath))
			return;

		try
		{
			var preset = JsonSerializer.Deserialize<Preset>(File.ReadAllText(manifestPath));

			if (preset == null)
				return;

			preset.BaseSize = sizePx;
			File.WriteAllText(manifestPath, JsonSerializer.Serialize(preset, JsonOptions));
		}
		catch
		{
		}
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
		try
		{
			File.Delete(path);
		}
		catch
		{
		}
	}
}
