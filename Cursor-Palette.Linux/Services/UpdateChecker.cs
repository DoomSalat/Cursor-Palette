using System.Net.Http;
using System.Text.Json;

namespace CursorPalette.Linux.Services;

public record UpdateInfo(string Version, string DownloadUrl);

public static class UpdateChecker
{
	private const string ReleasesApiUrl = "https://api.github.com/repos/DoomSalat/Cursor-Palette/releases/latest";
	private const string UserAgent = "Cursor-Palette-App";
	private const string TagNameProperty = "tag_name";
	private const string AssetsProperty = "assets";
	private const string NameProperty = "name";
	private const string BrowserDownloadUrlProperty = "browser_download_url";
	private const string VersionPrefix = "v";
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

			var downloadUrl = AppInfo.GitHubReleasesUrl;

			return new UpdateInfo(tag, downloadUrl);
		}
		catch
		{
			return null;
		}
	}
}
