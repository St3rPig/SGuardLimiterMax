using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace SGuardLimiterMax.Services;

/// <summary>Carries the remote release data when a newer version is available.</summary>
public record UpdateInfo(string TagName, string HtmlUrl);

/// <summary>
/// Checks GitHub Releases for a newer version of this assembly.
/// All failures are swallowed — the caller always receives null on error.
/// </summary>
public static class UpdateChecker
{
    private static readonly HttpClient _http = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "SGuardLimiterMax-UpdateChecker" } },
        Timeout = TimeSpan.FromSeconds(10)
    };

    private const string ApiUrl =
        "https://api.github.com/repos/St3rPig/SGuardLimiterMax/releases/latest";

    /// <summary>
    /// Queries GitHub for the latest release. Returns <c>null</c> when the current
    /// version is up to date, the request fails, or the response cannot be parsed.
    /// </summary>
    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync(ApiUrl, ct);
            if (!response.IsSuccessStatusCode) return null;

            string json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("tag_name", out var tagEl) ||
                !root.TryGetProperty("html_url", out var urlEl))
                return null;

            string tagName = tagEl.GetString() ?? string.Empty;
            string htmlUrl = urlEl.GetString() ?? string.Empty;

            string versionStr = tagName.TrimStart('v');
            if (!Version.TryParse(versionStr, out var remoteVersion)) return null;

            var current = Assembly.GetExecutingAssembly().GetName().Version;
            if (current == null || current >= remoteVersion) return null;

            return new UpdateInfo(tagName, htmlUrl);
        }
        catch { return null; }
    }
}
