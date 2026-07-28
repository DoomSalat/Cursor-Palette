using System.Net.Http;
using System.Text.Json;

namespace CursorPalette.Services;

public record UpdateInfo(string Version, string DownloadUrl, string? Sha256);

public static class UpdateChecker
{
	private const string ReleasesApiUrl = "https://api.github.com/repos/DoomSalat/Cursor-Palette/releases/latest";
	private const string UserAgent = "Cursor-Palette-App";
	private const string TagNameProperty = "tag_name";
	private const string AssetsProperty = "assets";
	private const string NameProperty = "name";
	private const string BrowserDownloadUrlProperty = "browser_download_url";
	private const string ExeExtension = ".exe";
	private const string Sha256Extension = ".sha256";
	private const string VersionPrefix = "v";
	private static readonly string[] ChecksumBundleNames = { "sha256sums.txt", "checksums.txt", "sha256sums" };
	private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(10);

	private static readonly HttpClient HttpClient = new();

	static UpdateChecker()
	{
		HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
		HttpClient.Timeout = HttpTimeout;
	}

	public static async Task<bool> IsUpdateAvailableAsync(string currentVersion)
	{
		var info = await GetLatestReleaseInfoAsync();
		if (info is null)
			return false;

		if (!Version.TryParse(info.Version, out var latestVersion))
			return false;

		if (!Version.TryParse(currentVersion, out var currentVer))
			return false;

		return latestVersion > currentVer;
	}

	public static async Task<UpdateInfo?> GetLatestReleaseInfoAsync()
	{
		try
		{
			using var response = await HttpClient.GetAsync(ReleasesApiUrl);
			response.EnsureSuccessStatusCode();

			var json = await response.Content.ReadAsStringAsync();
			using var doc = JsonDocument.Parse(json);

			if (!doc.RootElement.TryGetProperty(TagNameProperty, out var tagProp))
				return null;

			var tag = tagProp.GetString();
			if (string.IsNullOrEmpty(tag))
				return null;

			if (tag.StartsWith(VersionPrefix, StringComparison.OrdinalIgnoreCase))
				tag = tag[1..];

			var exeAsset = FindExeAsset(doc.RootElement);
			var downloadUrl = exeAsset?.Url ?? AppInfo.GitHubReleasesUrl;
			var sha256 = exeAsset is null ? null : await FindSha256Async(doc.RootElement, exeAsset.Value.Name);

			return new UpdateInfo(tag, downloadUrl, sha256);
		}
		catch
		{
			return null;
		}
	}

	private static (string Name, string Url)? FindExeAsset(JsonElement root)
	{
		if (!root.TryGetProperty(AssetsProperty, out var assets))
			return null;

		foreach (var asset in assets.EnumerateArray())
		{
			if (!asset.TryGetProperty(NameProperty, out var nameProp))
				continue;

			var name = nameProp.GetString();
			if (string.IsNullOrEmpty(name))
				continue;

			if (name.EndsWith(ExeExtension, StringComparison.OrdinalIgnoreCase) &&
				asset.TryGetProperty(BrowserDownloadUrlProperty, out var urlProp))
			{
				var url = urlProp.GetString();
				if (!string.IsNullOrEmpty(url))
					return (name, url);
			}
		}

		return null;
	}

	private static async Task<string?> FindSha256Async(JsonElement root, string exeAssetName)
	{
		if (!root.TryGetProperty(AssetsProperty, out var assets))
			return null;

		var perFileChecksumName = exeAssetName + Sha256Extension;

		string? bundleUrl = null;

		foreach (var asset in assets.EnumerateArray())
		{
			if (!asset.TryGetProperty(NameProperty, out var nameProp))
				continue;

			var name = nameProp.GetString();
			if (string.IsNullOrEmpty(name))
				continue;

			if (!asset.TryGetProperty(BrowserDownloadUrlProperty, out var urlProp))
				continue;

			var url = urlProp.GetString();
			if (string.IsNullOrEmpty(url))
				continue;

			if (string.Equals(name, perFileChecksumName, StringComparison.OrdinalIgnoreCase))
				return await DownloadAndParseChecksumAsync(url, exeAssetName);

			if (Array.Exists(ChecksumBundleNames, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
				bundleUrl = url;
		}

		return bundleUrl is null ? null : await DownloadAndParseChecksumAsync(bundleUrl, exeAssetName);
	}

	private static async Task<string?> DownloadAndParseChecksumAsync(string checksumUrl, string exeAssetName)
	{
		try
		{
			var content = await HttpClient.GetStringAsync(checksumUrl);

			foreach (var rawLine in content.Split('\n'))
			{
				var line = rawLine.Trim();
				if (line.Length == 0)
					continue;

				var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length == 0)
					continue;

				var hash = parts[0].Trim();

				if (parts.Length == 1)
					return hash;

				var fileName = parts[^1].TrimStart('*');
				if (string.Equals(fileName, exeAssetName, StringComparison.OrdinalIgnoreCase))
					return hash;
			}

			return null;
		}
		catch
		{
			return null;
		}
	}
}
