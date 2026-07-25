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
	private const string SinglePresetFormatId = "CursorPalette.SinglePreset";
	private const string SinglePresetMarkerFileName = "cursor-palette-preset.json";
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
	private const string DefaultLinuxArchiveName = "Cursor Palette Presets (Linux)";
	private const string DefaultXcursorThemeName = "Cursor Palette Presets";
	private const string XcursorArchiveNameSuffix = " (Xcursor)";
	private const string TempFolderPrefix = "cursor-palette-package-";

	private const string XcursorCursorsFolderName = "cursors";
	private const string XcursorIndexThemeFileName = "index.theme";
	private const string XcursorInheritsTheme = "default";
	private const string XcursorReconstructedFolderName = "_reconstructed";
	private const string XcursorPreviewBaseName = "preview";

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
					UseScaling = preset.UseScaling,
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
					UseScaling = preset.UseScaling,
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

	public static (string Path, int Count) ExportLinuxArchive(IReadOnlyList<Preset> presets, string? customName = null)
	{
		var stagingDir = CreateTempDir();

		try
		{
			var usedFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var exportedCount = 0;

			foreach (var preset in presets)
			{
				var folderName = MakeUniqueFolderName(SanitizeName(preset.Name), usedFolderNames);
				var folderPath = Path.Combine(stagingDir, folderName);

				if (WriteLinuxArchiveFolder(folderPath, role => PresetStore.GetRoleFilePath(preset, role)))
					exportedCount++;
			}

			var destPath = GetUniqueDownloadPath(ResolveExportName(customName, DefaultLinuxArchiveName), ".zip");

			CreateZipFromDirectory(stagingDir, destPath);

			return (destPath, exportedCount);
		}
		finally
		{
			TryDeleteDir(stagingDir);
		}
	}

	public static (string Path, int Count) ExportXcursorTheme(IReadOnlyList<Preset> presets, string? customName = null)
	{
		var stagingDir = CreateTempDir();

		try
		{
			var usedFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var exportedCount = 0;

			foreach (var preset in presets)
			{
				var folderName = MakeUniqueFolderName(SanitizeName(preset.Name), usedFolderNames);
				var themeDir = Path.Combine(stagingDir, folderName);

				if (WriteXcursorThemeFolder(themeDir, preset.Name, role => PresetStore.GetRoleFilePath(preset, role)))
					exportedCount++;
			}

			var destPath = GetUniqueDownloadPath(
				ResolveExportName(customName, DefaultXcursorThemeName) + XcursorArchiveNameSuffix, ".zip");

			CreateZipFromDirectory(stagingDir, destPath);

			return (destPath, exportedCount);
		}
		finally
		{
			TryDeleteDir(stagingDir);
		}
	}

	public static string? DownloadPresetAsFolder(string presetName, IReadOnlyDictionary<string, string> roleFiles,
		int baseSize, bool useScaling = false, IReadOnlySet<string>? lockedRoles = null)
	{
		var destDir = GetUniqueDownloadFolderPath(SanitizeName(presetName));
		Directory.CreateDirectory(destDir);

		var count = 0;

		foreach (var role in CursorRoles.All)
		{
			if (!roleFiles.TryGetValue(role.RegistryName, out var sourcePath) || !File.Exists(sourcePath))
				continue;

			var fileName = $"{role.RegistryName}{Path.GetExtension(sourcePath)}";
			var destPath = Path.Combine(destDir, fileName);
			File.Copy(sourcePath, destPath, overwrite: true);
			var now = DateTime.Now;
			File.SetCreationTime(destPath, now);
			File.SetLastWriteTime(destPath, now);
			count++;
		}

		if (count == 0)
		{
			Directory.Delete(destDir);
			return null;
		}

		var marker = new SinglePresetMarker
		{
			Format = SinglePresetFormatId,
			Version = FormatVersion,
			Name = presetName,
			BaseSize = baseSize,
			UseScaling = useScaling,
			LockedRoles = lockedRoles != null ? new HashSet<string>(lockedRoles) : new HashSet<string>(),
		};

		File.WriteAllText(Path.Combine(destDir, SinglePresetMarkerFileName),
			JsonSerializer.Serialize(marker, JsonOptions));

		WriteArchiveReadme(destDir);

		return destDir;
	}

	private static string GetUniqueDownloadFolderPath(string baseName)
	{
		var path = Path.Combine(AppPaths.DownloadsDir, baseName);
		var attempt = 1;

		while (Directory.Exists(path))
			path = Path.Combine(AppPaths.DownloadsDir, $"{baseName} ({attempt++})");

		return path;
	}

	public static string? ExportLinuxArchiveForFiles(string presetName, IReadOnlyDictionary<string, string> roleFiles)
	{
		var stagingDir = CreateTempDir();

		try
		{
			var folderName = SanitizeName(presetName);
			var folderPath = Path.Combine(stagingDir, folderName);

			if (!WriteLinuxArchiveFolder(folderPath, role => roleFiles.GetValueOrDefault(role)))
				return null;

			var destPath = GetUniqueDownloadPath(folderName, ".zip");
			CreateZipFromDirectory(stagingDir, destPath);

			return destPath;
		}
		finally
		{
			TryDeleteDir(stagingDir);
		}
	}

	public static string? ExportXcursorThemeForFiles(string presetName, IReadOnlyDictionary<string, string> roleFiles)
	{
		var stagingDir = CreateTempDir();

		try
		{
			var folderName = SanitizeName(presetName);
			var themeDir = Path.Combine(stagingDir, folderName);

			if (!WriteXcursorThemeFolder(themeDir, presetName, role => roleFiles.GetValueOrDefault(role)))
				return null;

			var destPath = GetUniqueDownloadPath(folderName + XcursorArchiveNameSuffix, ".zip");
			CreateZipFromDirectory(stagingDir, destPath);

			return destPath;
		}
		finally
		{
			TryDeleteDir(stagingDir);
		}
	}

	private static bool WriteLinuxArchiveFolder(string folderPath, Func<string, string?> resolveRolePath)
	{
		var written = false;

		foreach (var role in CursorRoles.All)
		{
			var sourcePath = resolveRolePath(role.RegistryName);

			if (sourcePath == null || !File.Exists(sourcePath))
				continue;

			Directory.CreateDirectory(folderPath);
			var fileName = $"{role.RegistryName}{Path.GetExtension(sourcePath)}";
			File.Copy(sourcePath, Path.Combine(folderPath, fileName), overwrite: true);
			written = true;
		}

		return written;
	}

	private static bool WriteXcursorThemeFolder(string themeDir, string presetName, Func<string, string?> resolveRolePath)
	{
		var cursorsDir = Path.Combine(themeDir, XcursorCursorsFolderName);
		var writtenAny = false;

		foreach (var role in CursorRoles.All)
		{
			var sourcePath = resolveRolePath(role.RegistryName);

			if (sourcePath == null || !File.Exists(sourcePath))
				continue;

			var frames = XcursorWriter.LoadFrames(sourcePath);

			if (frames == null || frames.Count == 0)
				continue;

			Directory.CreateDirectory(cursorsDir);
			var bytes = XcursorWriter.Build(frames);

			var aliases = XcursorWriter.RoleAliases.TryGetValue(role.RegistryName, out var names)
				? names
				: new[] { role.RegistryName.ToLowerInvariant() };

			foreach (var alias in aliases)
				File.WriteAllBytes(Path.Combine(cursorsDir, alias), bytes);

			writtenAny = true;
		}

		if (writtenAny)
		{
			File.WriteAllText(Path.Combine(themeDir, XcursorIndexThemeFileName),
				$"[Icon Theme]\nName={SanitizeName(presetName)}\nInherits={XcursorInheritsTheme}\n");
		}

		return writtenAny;
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

		return DetectFromExtractedDir(extractedDir);
	}

	public static DetectedPackage? TryDetectPackageFromFolder(string folderPath)
	{
		if (!Directory.Exists(folderPath))
			return null;

		string extractedDir;
		try
		{
			extractedDir = CopyDirectoryToTempDir(folderPath);
		}
		catch
		{
			return null;
		}

		return DetectFromExtractedDir(extractedDir);
	}

	private static string CopyDirectoryToTempDir(string sourceDir)
	{
		var destDir = CreateTempDir();
		CopyDirectoryContents(sourceDir, destDir);

		return destDir;
	}

	private static void CopyDirectoryContents(string sourceDir, string destDir)
	{
		Directory.CreateDirectory(destDir);

		foreach (var file in Directory.GetFiles(sourceDir))
			File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);

		foreach (var subDir in Directory.GetDirectories(sourceDir))
			CopyDirectoryContents(subDir, Path.Combine(destDir, Path.GetFileName(subDir)));
	}

	private static DetectedPackage? DetectFromExtractedDir(string extractedDir)
	{
		var singlePresetEntries = BuildSinglePresetEntries(extractedDir);

		if (singlePresetEntries.Count > 0)
			return new DetectedPackage { Kind = PackageKind.SinglePreset, ExtractedDir = extractedDir, Entries = singlePresetEntries };

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
					UseScaling = preset.UseScaling,
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
						UseScaling = preset.UseScaling,
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

		var xcursorThemeEntries = BuildXcursorThemeEntries(extractedDir);

		if (xcursorThemeEntries.Count > 0)
			return new DetectedPackage { Kind = PackageKind.XcursorTheme, ExtractedDir = extractedDir, Entries = xcursorThemeEntries };

		var plainFolderEntries = BuildPlainFolderEntries(extractedDir);

		if (plainFolderEntries.Count > 0)
			return new DetectedPackage { Kind = PackageKind.Archive, ExtractedDir = extractedDir, Entries = plainFolderEntries };

		TryDeleteDir(extractedDir);

		return null;
	}

	private static List<PackageEntry> BuildXcursorThemeEntries(string extractedDir)
	{
		var entries = new List<PackageEntry>();

		foreach (var themeDir in Directory.GetDirectories(extractedDir))
		{
			var cursorsDir = Path.Combine(themeDir, XcursorCursorsFolderName);

			if (!Directory.Exists(cursorsDir))
				continue;

			var cursorFiles = Directory.GetFiles(cursorsDir);
			var roleCount = cursorFiles
				.Select(file => XcursorWriter.AliasToRole.TryGetValue(Path.GetFileName(file), out var role) ? role : null)
				.Where(role => role != null)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Count();

			if (roleCount == 0)
				continue;

			var previewSourceFile = cursorFiles.FirstOrDefault(file =>
					XcursorWriter.AliasToRole.TryGetValue(Path.GetFileName(file), out var role) &&
					string.Equals(role, CursorRoles.ArrowRoleName, StringComparison.OrdinalIgnoreCase)) ??
				cursorFiles.FirstOrDefault(file => XcursorWriter.AliasToRole.ContainsKey(Path.GetFileName(file)));

			var previewPath = previewSourceFile != null
				? TryReconstructCursorFile(previewSourceFile,
					Path.Combine(themeDir, XcursorReconstructedFolderName), XcursorPreviewBaseName)
				: null;

			entries.Add(new PackageEntry
			{
				Key = Path.GetFileName(themeDir),
				DisplayName = ReadXcursorThemeName(themeDir) ?? Path.GetFileName(themeDir),
				RoleCount = roleCount,
				BaseSize = RegistryCursorService.DefaultBaseSize,
				PreviewPath = previewPath,
			});
		}

		return entries;
	}

	public static string? ReadXcursorThemeName(string themeDir)
	{
		var indexPath = Path.Combine(themeDir, XcursorIndexThemeFileName);

		if (!File.Exists(indexPath))
			return null;

		try
		{
			foreach (var line in File.ReadLines(indexPath))
			{
				if (line.StartsWith("Name=", StringComparison.OrdinalIgnoreCase))
					return line["Name=".Length..].Trim();
			}
		}
		catch
		{
		}

		return null;
	}

	private static string? TryReconstructCursorFile(string xcursorFilePath, string destDir, string destBaseName)
	{
		byte[] bytes;

		try
		{
			bytes = File.ReadAllBytes(xcursorFilePath);
		}
		catch
		{
			return null;
		}

		var frames = XcursorWriter.TryParse(bytes);

		if (frames == null || frames.Count == 0)
			return null;

		Directory.CreateDirectory(destDir);

		var images = frames
			.Select(frame => new CursorCanvasImage(frame.Width, frame.Height, frame.HotspotX, frame.HotspotY, frame.Bgra))
			.ToList();

		if (images.Count == 1)
		{
			var curPath = Path.Combine(destDir, destBaseName + CurExtension);
			CursorCanvasService.Write(curPath, images[0]);

			return curPath;
		}

		var aniPath = Path.Combine(destDir, destBaseName + AniExtension);
		AniCursorWriter.Save(aniPath, images, frames.Select(frame => frame.DelayMs).ToList());

		return aniPath;
	}

	private static PresetDraft? BuildXcursorThemeDraft(string extractedDir, PackageEntry entry)
	{
		var themeDir = Path.Combine(extractedDir, entry.Key);
		var reconstructedDir = Path.Combine(themeDir, XcursorReconstructedFolderName);
		var roleFiles = ReconstructXcursorThemeRoles(themeDir, reconstructedDir);

		if (roleFiles.Count == 0)
			return null;

		var draft = new PresetDraft { Name = entry.DisplayName };

		foreach (var (roleName, filePath) in roleFiles)
			draft.RoleSources[roleName] = new RoleSourceDraft { OwnFilePath = filePath };

		return draft;
	}

	public static Dictionary<string, string> ReconstructXcursorThemeRoles(string themeDir, string reconstructedDir)
	{
		var result = new Dictionary<string, string>();
		var cursorsDir = Path.Combine(themeDir, XcursorCursorsFolderName);

		if (!Directory.Exists(cursorsDir))
			return result;

		var assignedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var file in Directory.GetFiles(cursorsDir))
		{
			if (!XcursorWriter.AliasToRole.TryGetValue(Path.GetFileName(file), out var roleName))
				continue;

			if (!assignedRoles.Add(roleName))
				continue;

			var destPath = TryReconstructCursorFile(file, reconstructedDir, roleName);

			if (destPath != null)
				result[roleName] = destPath;
		}

		return result;
	}

	public static bool LooksLikeXcursorTheme(string folderPath) =>
		Directory.Exists(Path.Combine(folderPath, XcursorCursorsFolderName));

	private static List<PackageEntry> BuildSinglePresetEntries(string extractedDir)
	{
		var markerPath = Path.Combine(extractedDir, SinglePresetMarkerFileName);
		var marker = File.Exists(markerPath) ? TryReadJson<SinglePresetMarker>(markerPath) : null;

		if (marker?.Format != SinglePresetFormatId)
			return new List<PackageEntry>();

		var cursorFiles = Directory.EnumerateFiles(extractedDir).Where(IsCursorFile).ToList();

		if (cursorFiles.Count == 0)
			return new List<PackageEntry>();

		var previewPath = cursorFiles.FirstOrDefault(file =>
			string.Equals(Path.GetFileNameWithoutExtension(file), CursorRoles.ArrowRoleName,
				StringComparison.OrdinalIgnoreCase)) ?? cursorFiles.FirstOrDefault();

		return new List<PackageEntry>
		{
			new()
			{
				Key = string.Empty,
				DisplayName = marker.Name,
				RoleCount = cursorFiles.Count,
				BaseSize = marker.BaseSize > 0 ? marker.BaseSize : RegistryCursorService.DefaultBaseSize,
				UseScaling = marker.UseScaling,
				PreviewPath = previewPath,
			},
		};
	}

	private static PresetDraft? BuildSinglePresetDraft(string extractedDir, PackageEntry entry)
	{
		var markerPath = Path.Combine(extractedDir, SinglePresetMarkerFileName);
		var marker = File.Exists(markerPath) ? TryReadJson<SinglePresetMarker>(markerPath) : null;

		if (marker == null)
			return null;

		var draft = new PresetDraft
		{
			Name = marker.Name,
			BaseSize = marker.BaseSize > 0 ? marker.BaseSize : RegistryCursorService.DefaultBaseSize,
			UseScaling = marker.UseScaling,
		};

		foreach (var file in Directory.EnumerateFiles(extractedDir).Where(IsCursorFile))
		{
			var role = CursorRoles.MatchByFileName(file);

			if (role != null)
				draft.RoleSources[role.RegistryName] = new RoleSourceDraft { OwnFilePath = file };
		}

		foreach (var role in marker.LockedRoles)
			draft.LockedRoles.Add(role);

		return draft.RoleSources.Count > 0 ? draft : null;
	}

	private static List<PackageEntry> BuildPlainFolderEntries(string extractedDir)
	{
		var entries = new List<PackageEntry>();

		foreach (var folderPath in Directory.GetDirectories(extractedDir))
		{
			var cursorFiles = Directory.EnumerateFiles(folderPath).Where(IsCursorFile).ToList();

			if (cursorFiles.Count == 0)
				continue;

			var previewPath = cursorFiles.FirstOrDefault(file =>
				string.Equals(Path.GetFileNameWithoutExtension(file), CursorRoles.ArrowRoleName,
					StringComparison.OrdinalIgnoreCase)) ?? cursorFiles.FirstOrDefault();

			entries.Add(new PackageEntry
			{
				Key = Path.GetFileName(folderPath),
				DisplayName = Path.GetFileName(folderPath),
				RoleCount = cursorFiles.Count,
				BaseSize = RegistryCursorService.DefaultBaseSize,
				PreviewPath = previewPath,
			});
		}

		return entries;
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
				PackageKind.XcursorTheme => BuildXcursorThemeDraft(package.ExtractedDir, entry),
				PackageKind.SinglePreset => BuildSinglePresetDraft(package.ExtractedDir, entry),
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

	public static string DownloadReadme()
	{
		var destPath = GetUniqueDownloadPath(Path.GetFileNameWithoutExtension(ReadmeFileName), Path.GetExtension(ReadmeFileName));

		File.WriteAllText(destPath, BuildReadmeContent());

		return destPath;
	}

	private static void WriteArchiveReadme(string stagingDir) =>
		File.WriteAllText(Path.Combine(stagingDir, ReadmeFileName), BuildReadmeContent());

	private static string BuildReadmeContent()
	{
		var uri = new Uri($"pack://application:,,,/Resources/{ReadmeResourceName}", UriKind.Absolute);

		using var stream = Application.GetResourceStream(uri)?.Stream;

		if (stream == null)
			return string.Empty;

		using var reader = new StreamReader(stream);

		return reader.ReadToEnd()
			.Replace("{{AppName}}", AppInfo.Name)
			.Replace("{{AppUrl}}", AppInfo.GitHubUrl)
			.Replace("{{AppCopyright}}", AppInfo.CopyrightLine);
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
		var draft = new PresetDraft { Name = preset.Name, BaseSize = preset.BaseSize, UseScaling = preset.UseScaling };

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
		var draft = new PresetDraft { Name = preset.Name, BaseSize = preset.BaseSize, UseScaling = preset.UseScaling };

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
