using System.Text.Json;

namespace SMH_android;

/// <summary>GitHub Releases 최신 버전을 확인해 업데이트 여부를 판단한다.</summary>
public static class UpdateChecker
{
    private const string LatestApi =
        "https://api.github.com/repos/gogamegood-beep/SMHMacro/releases/latest";

    public record Result(bool HasUpdate, string LatestVersion, string DownloadUrl);

    public static async Task<Result?> CheckAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("SMHMacro-Updater");
            var json = await http.GetStringAsync(LatestApi);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var tag = root.TryGetProperty("tag_name", out var t) ? (t.GetString() ?? "") : "";
            var latest = tag.TrimStart('v', 'V');

            // APK 자산 다운로드 URL (없으면 릴리스 페이지로 폴백)
            string url = "";
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                foreach (var a in assets.EnumerateArray())
                    if ((a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "").EndsWith(".apk"))
                    {
                        url = a.TryGetProperty("browser_download_url", out var d) ? d.GetString() ?? "" : "";
                        break;
                    }
            if (string.IsNullOrEmpty(url) && root.TryGetProperty("html_url", out var h))
                url = h.GetString() ?? "";

            var current = AppInfo.Current.VersionString;
            return new Result(IsNewer(latest, current), latest, url);
        }
        catch
        {
            return null; // 오프라인/오류 시 조용히 무시
        }
    }

    private static bool IsNewer(string latest, string current)
    {
        if (!Version.TryParse(Pad(latest), out var vl)) return false;
        if (!Version.TryParse(Pad(current), out var vc)) return false;
        return vl > vc;
    }

    // "1" → "1.0", "1.2" → "1.2.0" 등 Version 파싱 보정
    private static string Pad(string v)
    {
        var parts = v.Split('.');
        return parts.Length switch
        {
            1 => v + ".0",
            _ => v,
        };
    }
}
