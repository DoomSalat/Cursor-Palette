using System.IO.Compression;
using System.Text.Json;
using System.Windows;
using CursorPalette.Models;

namespace CursorPalette.Services;

public static class PresetPackageService
{
	public const string BundleExtension = ".cursorpalette";

	private const string BundleFormatId = "CursorPalette.Bundle";
	private const string ArchiveFormatId = "CursorPalette.Archive";
	private const int FormatVersion = 1;

	private const string BundleMarkerFileName = "bundle.json";
	private const string ArchiveMarkerFileName = "cursor-palette-archive.json";
	private const string PackageManifestFileName = "cursor-palette.json";
	private const string GroupsFileName = "groups.json";
	private const string PresetsFolderName = "presets";
	private const string FilesFolderName = "files";
	private const string ManifestFileName = "manifest.json";

	private const string DefaultBundleName = "Cursor Palette Presets";
	private const string DefaultArchiveName = "Cursor Palette Presets";
	private const string TempFolderPrefix = "cursor-palette-package-";

	private const string CurExtension = ".cur";
	private const string AniExtension = ".ani";
	private const string ReadmeFileName = "README.txt";
	private const string ReadmeResourceName = "ArchiveReadme.md";

	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

	public static bool IsSupportedPackageFile(string path) =>
		ArchiveImportService.IsArchiveFile(path) ||
		string.Equals(Path.GetExtension(path), BundleExtension, StringComparison.OrdinalIgnoreCase);

	public static (string Path, int Count) ExportBundle(IReadOnlyList<Preset> presets, string? customName = null)
	{
		var stagingDir = CreateTempDir();
		try
		{
			var presetsRoot = Path.Combine(stagingDir, PresetsFolderName);
			Directory.CreateDirectory(presetsRoot);

			var manifestPresets = new List<ArchiveManifestPreset>();

			foreach (var preset in presets)
			{
				var presetDir = Path.Combine(presetsRoot, preset.Id);
				var filesDir = Path.Combine(presetDir, FilesFolderName);
				Directory.CreateDirectory(filesDir);

				var roles = new Dictionary<string, string>();

				foreach (var role in CursorRoles.All)
				{
					var sourcePath = PresetStore.GetRoleFilePath(preset, role.RegistryName);

					if (sourcePath == null || !File.Exists(sourcePath))
						continue;

					var fileName = $"{role.RegistryName}{Path.GetExtension(sourcePath)}";
					File.Copy(sourcePath, Path.Combine(filesDir, fileName), overwrite: true);
					roles[role.RegistryName] = fileName;
				}

				if (roles.Count == 0)
				{
					Directory.Delete(presetDir, recursive: true);
					continue;
				}

				manifestPresets.Add(new ArchiveManifestPreset
				{
					Id = preset.Id,
					Name = preset.Name,
					Folder = preset.Id,
					CreatedAt = preset.CreatedAt,
					SortOrder = preset.SortOrder,
					BaseSize = preset.BaseSize,
					Roles = roles,
					LockedRoles = new HashSet<string>(preset.LockedRoles),
				});
			}

			var manifest = new ArchiveManifest
			{
				Format = BundleFormatId,
				Version = FormatVersion,
				Presets = manifestPresets,
				Groups = BuildExportedGroups(presets),
			};

			File.WriteAllText(Path.Combine(stagingDir, PackageManifestFileName),
				JsonSerializer.Serialize(manifest, JsonOptions));

			var destPath = GetUniqueDownloadPath(ResolveExportName(customName, DefaultBundleName), BundleExtension);

			CreateZipFromDirectory(stagingDir, destPath);

			return (destPath, manifestPresets.Count);
		}
		finally
		{
			TryDeleteDir(stagingDir);
		}
	}

	public static (string Path, int Count) ExportArchive(IReadOnlyList<Preset> presets, string? customName = null)
	{
		var stagingDir = CreateTempDir();

		try
		{
			var manifestPresets = new List<ArchiveManifestPreset>();
			var usedFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var presetsRoot = Path.Combine(stagingDir, PresetsFolderName);
			Directory.CreateDirectory(presetsRoot);

			foreach (var preset in presets)
			{
				var folderName = MakeUniqueFolderName(SanitizeName(preset.Name), usedFolderNames);
				var folderPath = Path.Combine(stagingDir, folderName);

				var roles = new Dictionary<string, string>();
				var count = 0;

				foreach (var role in CursorRoles.All)
				{
					var sourcePath = PresetStore.GetRoleFilePath(preset, role.RegistryName);

					if (sourcePath == null || !File.Exists(sourcePath))
						continue;

					Directory.CreateDirectory(folderPath);
					var fileName = $"{role.RegistryName}{Path.GetExtension(sourcePath)}";
					File.Copy(sourcePath, Path.Combine(folderPath, fileName), overwrite: true);
					roles[role.RegistryName] = fileName;
					count++;
				}

				if (count == 0)
					continue;

				var presetDir = Path.Combine(presetsRoot, preset.Id);
				var filesDir = Path.Combine(presetDir, FilesFolderName);
				Directory.CreateDirectory(filesDir);

				foreach (var (role, fileName) in roles)
					File.Copy(Path.Combine(folderPath, fileName), Path.Combine(filesDir, fileName), overwrite: true);

				manifestPresets.Add(new ArchiveManifestPreset
				{
					Id = preset.Id,
					Name = preset.Name,
					Folder = folderName,
					CreatedAt = preset.CreatedAt,
					SortOrder = preset.SortOrder,
					BaseSize = preset.BaseSize,
					Roles = roles,
					LockedRoles = new HashSet<string>(preset.LockedRoles),
				});
			}

			var manifest = new ArchiveManifest
			{
				Format = BundleFormatId,
				Version = FormatVersion,
				Presets = manifestPresets,
				Groups = BuildExportedGroups(presets),
			};

			File.WriteAllText(Path.Combine(stagingDir, PackageManifestFileName),
				JsonSerializer.Serialize(manifest, JsonOptions));

			WriteArchiveReadme(stagingDir);

			var destPath = GetUniqueDownloadPath(ResolveExportName(customName, DefaultArchiveName), ".zip");

			CreateZipFromDirectory(stagingDir, destPath);

			return (destPath, manifestPresets.Count);
		}
		finally
		{
			TryDeleteDir(stagingDir);
		}
	}

	public static DetectedPackage? TryDetectPackage(string filePath)
	{
		if (!IsSupportedPackageFile(filePath))
			return null;

		string extractedDir;
		try
		{
			extractedDir = ArchiveImportService.ExtractToTempFolder(filePath);
		}
		catch
		{
			return null;
		}

		var manifestPath = Path.Combine(extractedDir, PackageManifestFileName);
		var manifest = File.Exists(manifestPath) ? TryReadJson<ArchiveManifest>(manifestPath) : null;

		if (manifest?.Format == BundleFormatId)
		{
			if (manifest.Version > FormatVersion)
			{
				TryDeleteDir(extractedDir);

				throw new PackageVersionUnsupportedException(manifest.Version, FormatVersion);
			}

			var entries = new List<PackageEntry>();
			var presetsRoot = Path.Combine(extractedDir, PresetsFolderName);

			foreach (var preset in manifest.Presets)
			{
				var presetDir = Path.Combine(presetsRoot, preset.Id);
				var filesDir = Path.Combine(presetDir, FilesFolderName);

				if (!Directory.Exists(filesDir))
					continue;

				var previewFileName = preset.Roles.TryGetValue(CursorRoles.ArrowRoleName, out var arrowFile)
					? arrowFile
					: preset.Roles.Values.FirstOrDefault();

				entries.Add(new PackageEntry
				{
					Key = preset.Id,
					DisplayName = preset.Name,
					RoleCount = preset.Roles.Count,
					BaseSize = preset.BaseSize,
					PreviewPath = previewFileName != null
						? Path.Combine(filesDir, previewFileName)
						: null,
				});
			}

			if (entries.Count == 0)
			{
				TryDeleteDir(extractedDir);

				return null;
			}

			return new DetectedPackage
			{
				Kind = PackageKind.Manifest,
				ExtractedDir = extractedDir,
				Entries = entries,
				Groups = manifest.Groups,
			};
		}

		var bundleMarkerPath = Path.Combine(extractedDir, BundleMarkerFileName);
		var bundleMarker = File.Exists(bundleMarkerPath) ? TryReadJson<PackageMarker>(bundleMarkerPath) : null;

		if (bundleMarker?.Format == BundleFormatId)
		{
			if (bundleMarker.Version > FormatVersion)
			{
				TryDeleteDir(extractedDir);

				throw new PackageVersionUnsupportedException(bundleMarker.Version, FormatVersion);
			}

			var entries = new List<PackageEntry>();
			var presetsRoot = Path.Combine(extractedDir, PresetsFolderName);

			if (Directory.Exists(presetsRoot))
			{
				foreach (var presetDir in Directory.GetDirectories(presetsRoot))
				{
					var perPresetManifestPath = Path.Combine(presetDir, ManifestFileName);
					var preset = File.Exists(perPresetManifestPath) ? TryReadJson<Preset>(perPresetManifestPath) : null;

					if (preset == null)
						continue;

					var previewFileName = preset.Roles.TryGetValue(CursorRoles.ArrowRoleName, out var arrowFile)
						? arrowFile
						: preset.Roles.Values.FirstOrDefault();

					entries.Add(new PackageEntry
					{
						Key = Path.GetFileName(presetDir),
						DisplayName = preset.Name,
						RoleCount = preset.Roles.Count,
						BaseSize = preset.BaseSize,
						PreviewPath = previewFileName != null
							? Path.Combine(presetDir, FilesFolderName, previewFileName)
							: null,
					});
				}
			}

			if (entries.Count == 0)
			{
				TryDeleteDir(extractedDir);

				return null;
			}

			var groups = ReadExportedGroups(extractedDir);

			return new DetectedPackage
			{
				Kind = PackageKind.Bundle,
				ExtractedDir = extractedDir,
				Entries = entries,
				Groups = groups,
			};
		}

		var archiveMarkerPath = Path.Combine(extractedDir, ArchiveMarkerFileName);
		var archiveMarker = File.Exists(archiveMarkerPath) ? TryReadJson<ArchiveMarker>(archiveMarkerPath) : null;

		if (archiveMarker?.Format == ArchiveFormatId)
		{
			if (archiveMarker.Version > FormatVersion)
			{
				TryDeleteDir(extractedDir);

				throw new PackageVersionUnsupportedException(archiveMarker.Version, FormatVersion);
			}

			var entries = archiveMarker.Presets
				.Where(entry => Directory.Exists(Path.Combine(extractedDir, entry.Folder)))
				.Select(entry =>
				{
					var folderPath = Path.Combine(extractedDir, entry.Folder);
					var cursorFiles = Directory.EnumerateFiles(folderPath).Where(IsCursorFile).ToList();
					var previewPath = cursorFiles.FirstOrDefault(file =>
						string.Equals(Path.GetFileNameWithoutExtension(file), CursorRoles.ArrowRoleName,
							StringComparison.OrdinalIgnoreCase)) ?? cursorFiles.FirstOrDefault();

					return new PackageEntry
					{
						Key = entry.Folder,
						DisplayName = entry.Name,
						RoleCount = cursorFiles.Count,
						BaseSize = RegistryCursorService.DefaultBaseSize,
						PreviewPath = previewPath,
					};
				})
				.Where(entry => entry.RoleCount > 0)
				.ToList();

			if (entries.Count == 0)
			{
				TryDeleteDir(extractedDir);

				return null;
			}

			return new DetectedPackage { Kind = PackageKind.Archive, ExtractedDir = extractedDir, Entries = entries };
		}

		TryDeleteDir(extractedDir);

		return null;
	}

	public static int ImportSelected(DetectedPackage package, IReadOnlyList<PackageEntry> selectedEntries,
		IReadOnlyList<PackageGroupEntry>? selectedGroups = null,
		bool ignoreIndividualSizes = false, int uniformSize = RegistryCursorService.DefaultBaseSize)
	{
		var imported = 0;
		var keyToNewId = new Dictionary<string, string>();

		foreach (var entry in selectedEntries)
		{
			var draft = package.Kind switch
			{
				PackageKind.Manifest => BuildManifestDraft(package.ExtractedDir, entry),
				PackageKind.Bundle => BuildBundleDraft(package.ExtractedDir, entry),
				_ => BuildArchiveDraft(package.ExtractedDir, entry),
			};

			if (draft == null || draft.RoleSources.Count == 0)
				continue;

			if (ignoreIndividualSizes)
				draft.BaseSize = uniformSize;

			var saved = PresetStore.Save(draft);
			keyToNewId[entry.Key] = saved.Id;
			imported++;
		}

		foreach (var group in selectedGroups ?? Array.Empty<PackageGroupEntry>())
		{
			var memberIds = group.MemberKeys
				.Where(keyToNewId.ContainsKey)
				.Select(key => keyToNewId[key])
				.ToList();

			if (memberIds.Count == 0)
				continue;

			GroupStore.Save(new PresetGroup
			{
				Id = Guid.NewGuid().ToString("N"),
				Name = group.Name,
				ColorKey = group.ColorKey,
				Collapsed = group.Collapsed,
				MemberPresetIds = memberIds,
			});
		}

		return imported;
	}

	public static void CleanupPackage(DetectedPackage package) => TryDeleteDir(package.ExtractedDir);

	private static void WriteArchiveReadme(string stagingDir)
	{
		var uri = new Uri($"pack://application:,,,/Resources/{ReadmeResourceName}", UriKind.Absolute);

		using var stream = Application.GetResourceStream(uri)?.Stream;

		if (stream == null)
			return;

		using var reader = new StreamReader(stream);
		var content = reader.ReadToEnd()
			.Replace("{{AppName}}", AppInfo.Name)
			.Replace("{{AppUrl}}", AppInfo.GitHubUrl)
			.Replace("{{AppCopyright}}", AppInfo.CopyrightLine);

		File.WriteAllText(Path.Combine(stagingDir, ReadmeFileName), content);
	}

	private static List<PackageGroupEntry> BuildExportedGroups(IReadOnlyList<Preset> exportedPresets)
	{
		var exportedIds = exportedPresets.Select(preset => preset.Id).ToHashSet();

		return GroupStore.LoadAll()
			.Where(group => group.MemberPresetIds.Count > 0 && group.MemberPresetIds.All(exportedIds.Contains))
			.Select(group => new PackageGroupEntry
			{
				Id = group.Id,
				Name = group.Name,
				ColorKey = group.ColorKey,
				Collapsed = group.Collapsed,
				MemberKeys = group.MemberPresetIds.ToList(),
			})
			.ToList();
	}

	private static void WriteExportedGroups(string stagingDir, IReadOnlyList<Preset> exportedPresets)
	{
		var fullyIncludedGroups = BuildExportedGroups(exportedPresets);

		if (fullyIncludedGroups.Count == 0)
			return;

		File.WriteAllText(Path.Combine(stagingDir, GroupsFileName),
			JsonSerializer.Serialize(fullyIncludedGroups, JsonOptions));
	}

	private static List<PackageGroupEntry> ReadExportedGroups(string extractedDir)
	{
		var groupsPath = Path.Combine(extractedDir, GroupsFileName);

		if (!File.Exists(groupsPath))
			return new();

		return TryReadJson<List<PackageGroupEntry>>(groupsPath) ?? new();
	}

	private static PresetDraft? BuildManifestDraft(string extractedDir, PackageEntry entry)
	{
		var manifestPath = Path.Combine(extractedDir, PackageManifestFileName);
		var manifest = File.Exists(manifestPath) ? TryReadJson<ArchiveManifest>(manifestPath) : null;

		if (manifest == null)
			return null;

		var preset = manifest.Presets.FirstOrDefault(p => p.Id == entry.Key);
		if (preset == null)
			return null;

		var filesDir = Path.Combine(extractedDir, PresetsFolderName, preset.Id, FilesFolderName);
		var draft = new PresetDraft { Name = preset.Name, BaseSize = preset.BaseSize };

		foreach (var (role, fileName) in preset.Roles)
		{
			var filePath = Path.Combine(filesDir, fileName);
			if (File.Exists(filePath))
				draft.RoleSources[role] = new RoleSourceDraft { OwnFilePath = filePath };
		}

		foreach (var role in preset.LockedRoles)
			draft.LockedRoles.Add(role);

		return draft;
	}

	private static PresetDraft? BuildBundleDraft(string extractedDir, PackageEntry entry)
	{
		var presetDir = Path.Combine(extractedDir, PresetsFolderName, entry.Key);
		var manifestPath = Path.Combine(presetDir, ManifestFileName);
		var preset = File.Exists(manifestPath) ? TryReadJson<Preset>(manifestPath) : null;

		if (preset == null)
			return null;

		var filesDir = Path.Combine(presetDir, FilesFolderName);
		var draft = new PresetDraft { Name = preset.Name, BaseSize = preset.BaseSize };

		foreach (var (role, fileName) in preset.Roles)
		{
			var filePath = Path.Combine(filesDir, fileName);
			if (File.Exists(filePath))
				draft.RoleSources[role] = new RoleSourceDraft { OwnFilePath = filePath };
		}

		foreach (var role in preset.LockedRoles)
			draft.LockedRoles.Add(role);

		return draft;
	}

	private static PresetDraft? BuildArchiveDraft(string extractedDir, PackageEntry entry)
	{
		var folderPath = Path.Combine(extractedDir, entry.Key);

		if (!Directory.Exists(folderPath))
			return null;

		var draft = new PresetDraft { Name = entry.DisplayName };

		foreach (var file in Directory.EnumerateFiles(folderPath).Where(IsCursorFile))
		{
			var role = CursorRoles.MatchByFileName(file);

			if (role != null)
				draft.RoleSources[role.RegistryName] = new RoleSourceDraft { OwnFilePath = file };
		}

		return draft;
	}

	private static void CreateZipFromDirectory(string sourceDir, string destPath)
	{
		if (File.Exists(destPath))
			File.Delete(destPath);

		ZipFile.CreateFromDirectory(sourceDir, destPath, CompressionLevel.Optimal, includeBaseDirectory: false);
	}

	private static string GetUniqueDownloadPath(string baseName, string extension)
	{
		var path = Path.Combine(AppPaths.DownloadsDir, $"{baseName}{extension}");
		var attempt = 1;

		while (File.Exists(path))
			path = Path.Combine(AppPaths.DownloadsDir, $"{baseName} ({attempt++}){extension}");

		return path;
	}

	private static string ResolveExportName(string? customName, string fallback) =>
		string.IsNullOrWhiteSpace(customName) ? fallback : SanitizeName(customName);

	private static string SanitizeName(string name)
	{
		var invalid = Path.GetInvalidFileNameChars();
		var sanitized = string.Join("", name.Where(character => !invalid.Contains(character))).Trim();

		return string.IsNullOrWhiteSpace(sanitized) ? "Preset" : sanitized;
	}

	private static string MakeUniqueFolderName(string baseName, HashSet<string> usedNames)
	{
		var name = baseName;
		var attempt = 1;

		while (!usedNames.Add(name))
			name = $"{baseName} ({attempt++})";

		return name;
	}

	private static string CreateTempDir()
	{
		var dir = Path.Combine(Path.GetTempPath(), $"{TempFolderPrefix}{Guid.NewGuid():N}");
		Directory.CreateDirectory(dir);
		return dir;
	}

	private static void TryDeleteDir(string dir)
	{
		try
		{
			if (Directory.Exists(dir))
				Directory.Delete(dir, recursive: true);
		}
		catch
		{
		}
	}

	private static T? TryReadJson<T>(string path)
	{
		try
		{
			return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
		}
		catch
		{
			return default;
		}
	}

	private static bool IsCursorFile(string path)
	{
		var extension = Path.GetExtension(path).ToLowerInvariant();

		return extension is CurExtension or AniExtension;
	}
}
