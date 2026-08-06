using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ChocoboColorCalculator.Desktop;

public sealed record DesktopUpdate(Version Version, string ReleaseUrl, string DownloadUrl);

public sealed class DesktopUpdateService : IDisposable
{
    private const string ReleasesUrl =
        "https://api.github.com/repos/t0nysama/ChocoboColorCalculator/releases?per_page=20";
    private const string DesktopTagPrefix = "desktop-v";
    private const string DesktopAssetName = "ChocoboColorCalculator-Desktop-win-x64.zip";

    private readonly HttpClient client;

    public DesktopUpdateService(HttpMessageHandler? handler = null)
    {
        handler ??= new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
        };

        client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(4),
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ChocoboColorCalculator-Desktop", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2026-03-10");
    }

    public async Task<DesktopUpdate?> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.GetAsync(
                ReleasesUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return null;

            DesktopUpdate? newest = null;
            foreach (var release in document.RootElement.EnumerateArray())
            {
                if (ReadBoolean(release, "draft") || ReadBoolean(release, "prerelease"))
                    continue;

                var tag = ReadString(release, "tag_name");
                var version = ParseDesktopVersion(tag);
                if (version is null || version <= currentVersion || (newest is not null && version <= newest.Version))
                    continue;

                var releaseUrl = ReadString(release, "html_url");
                if (string.IsNullOrWhiteSpace(releaseUrl))
                    continue;

                var downloadUrl = FindDownloadUrl(release) ?? releaseUrl;
                newest = new DesktopUpdate(version, releaseUrl, downloadUrl);
            }

            return newest;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose() => client.Dispose();

    private static Version? ParseDesktopVersion(string? tag)
    {
        if (tag is null || !tag.StartsWith(DesktopTagPrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var versionText = tag[DesktopTagPrefix.Length..];
        var suffixIndex = versionText.IndexOfAny(['-', '+']);
        if (suffixIndex >= 0)
            versionText = versionText[..suffixIndex];
        return Version.TryParse(versionText, out var version) ? version : null;
    }

    private static string? FindDownloadUrl(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var asset in assets.EnumerateArray())
        {
            if (string.Equals(ReadString(asset, "name"), DesktopAssetName, StringComparison.OrdinalIgnoreCase))
                return ReadString(asset, "browser_download_url");
        }
        return null;
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool ReadBoolean(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.True;
}
