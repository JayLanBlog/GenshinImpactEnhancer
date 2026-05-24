using Stella.Utils.Logging;
using System.Net.Http;
using System.Text.Json;

namespace Stella.Update;

public static class UpdateChecker
{
    private const string GitHubApiUrl = "https://api.github.com/repos/sefinek/Genshin-Impact-ReShade/releases/latest";

    /// <summary>
    /// 解析版本字符串，支持 "v8.10.1.0" 和 "1.2.3" 格式。
    /// </summary>
    public static Version ParseVersion(string versionString)
    {
        try
        {
            var clean = versionString.TrimStart('v', 'V');
            return Version.Parse(clean);
        }
        catch (Exception ex)
        {
            StellaLogger.Warn("UpdateChecker", $"Failed to parse version '{versionString}': {ex.Message}");
            return new Version(0, 0);
        }
    }

    /// <summary>
    /// 检查是否有更新可用。
    /// </summary>
    public static bool IsUpdateAvailable(Version current, Version latest)
    {
        return latest > current;
    }

    /// <summary>
    /// 从 GitHub Releases API 获取最新版本信息。
    /// </summary>
    public static async Task<ReleaseInfo?> FetchLatestReleaseAsync(HttpClient? client = null)
    {
        var http = client ?? new HttpClient();
        try
        {
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Genshin-Stella-Mod");
            var response = await http.GetStringAsync(GitHubApiUrl);

            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            var tagName = root.GetProperty("tag_name").GetString() ?? "0.0.0";
            var version = ParseVersion(tagName);

            var info = new ReleaseInfo
            {
                Version = version,
                TagName = tagName,
                Name = root.GetProperty("name").GetString() ?? tagName,
                PublishedAt = root.GetProperty("published_at").GetDateTime(),
                DownloadUrl = FindDownloadUrl(root)
            };

            StellaLogger.Info("UpdateChecker", $"Latest release: {info.TagName} ({info.Version})");
            return info;
        }
        catch (Exception ex)
        {
            StellaLogger.Warn("UpdateChecker", $"Failed to fetch release info: {ex.Message}");
            return null;
        }
    }

    private static string? FindDownloadUrl(JsonElement root)
    {
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (name.EndsWith(".exe") || name.EndsWith(".msi") || name.EndsWith(".zip"))
                {
                    return asset.GetProperty("browser_download_url").GetString();
                }
            }
        }
        return null;
    }

    public class ReleaseInfo
    {
        public Version Version { get; init; } = new(0, 0);
        public string TagName { get; init; } = "";
        public string Name { get; init; } = "";
        public DateTime PublishedAt { get; init; }
        public string? DownloadUrl { get; init; }
    }
}
