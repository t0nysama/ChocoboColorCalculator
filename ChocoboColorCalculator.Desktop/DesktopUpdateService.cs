using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace ChocoboColorCalculator.Desktop;

public sealed record DesktopUpdate(
    Version Version,
    string ReleaseUrl,
    string DownloadUrl,
    long AssetSize,
    string Sha256);

public sealed record DesktopUpdateCheckResult(
    bool Succeeded,
    DesktopUpdate? Update,
    string? ErrorMessage = null);

public sealed record DesktopUpdateProgress(string Message, double Percent);

public sealed record PreparedDesktopUpdate(
    DesktopUpdate Update,
    string WorkingDirectory,
    string PayloadDirectory,
    string ExecutablePath);

public sealed class DesktopUpdateService : IDisposable
{
    public const string DesktopExecutableName = "ChocoboColorCalculator.Desktop.exe";

    private const string ReleasesUrl =
        "https://api.github.com/repos/t0nysama/ChocoboColorCalculator/releases?per_page=20";
    private const string DesktopTagPrefix = "desktop-v";
    private const string DesktopAssetName = "ChocoboColorCalculator-Desktop-win-x64.zip";
    private const long MaximumArchiveBytes = 500L * 1024 * 1024;
    private const long MaximumExtractedBytes = 750L * 1024 * 1024;

    private readonly HttpClient client;

    public DesktopUpdateService(HttpMessageHandler? handler = null)
    {
        handler ??= new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
        };

        client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ChocoboColorCalculator-Desktop", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2026-03-10");
    }

    public async Task<DesktopUpdateCheckResult> CheckAsync(
        Version currentVersion,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(4));

        try
        {
            using var response = await client.GetAsync(
                ReleasesUrl,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return new DesktopUpdateCheckResult(false, null, $"GitHub returned HTTP {(int)response.StatusCode}.");

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token).ConfigureAwait(false);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return new DesktopUpdateCheckResult(false, null, "GitHub returned an unexpected response.");

            DesktopUpdate? newest = null;
            foreach (var release in document.RootElement.EnumerateArray())
            {
                if (ReadBoolean(release, "draft") || ReadBoolean(release, "prerelease"))
                    continue;

                var version = ParseDesktopVersion(ReadString(release, "tag_name"));
                if (version is null || version <= currentVersion || (newest is not null && version <= newest.Version))
                    continue;

                var releaseUrl = ReadString(release, "html_url");
                var asset = FindDesktopAsset(release);
                if (string.IsNullOrWhiteSpace(releaseUrl) || asset is null)
                    continue;

                newest = new DesktopUpdate(
                    version,
                    releaseUrl,
                    asset.Value.DownloadUrl,
                    asset.Value.Size,
                    asset.Value.Sha256);
            }

            return new DesktopUpdateCheckResult(true, newest);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new DesktopUpdateCheckResult(false, null, "The update check timed out.");
        }
        catch
        {
            return new DesktopUpdateCheckResult(false, null, "GitHub could not be reached.");
        }
    }

    public async Task<PreparedDesktopUpdate> DownloadAndPrepareAsync(
        DesktopUpdate update,
        IProgress<DesktopUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateUpdateMetadata(update);
        CleanupOldUpdates();

        var workingDirectory = Path.Combine(
            UpdateRootDirectory,
            $"{update.Version}-{Guid.NewGuid():N}");
        var payloadDirectory = Path.Combine(workingDirectory, "payload");
        var archivePath = Path.Combine(workingDirectory, DesktopAssetName);
        Directory.CreateDirectory(payloadDirectory);

        try
        {
            progress?.Report(new DesktopUpdateProgress("Connecting to GitHub...", 2));
            using var response = await client.GetAsync(
                update.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var contentLength = response.Content.Headers.ContentLength ?? update.AssetSize;
            if (contentLength <= 0 || contentLength > MaximumArchiveBytes)
                throw new InvalidDataException("The update archive size is invalid.");

            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var destination = new FileStream(
                archivePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                128 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[128 * 1024];
                long downloaded = 0;
                var lastReportedPercent = -1d;
                while (true)
                {
                    var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                        break;

                    downloaded += read;
                    if (downloaded > MaximumArchiveBytes)
                        throw new InvalidDataException("The update archive exceeded the allowed size.");
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);

                    var percent = 5 + (downloaded * 70d / contentLength);
                    if (percent - lastReportedPercent >= 1)
                    {
                        progress?.Report(new DesktopUpdateProgress(
                            $"Downloading update... {downloaded / 1024d / 1024d:F1} MB",
                            Math.Min(75, percent)));
                        lastReportedPercent = percent;
                    }
                }

                if (update.AssetSize > 0 && downloaded != update.AssetSize)
                    throw new InvalidDataException("The downloaded update size does not match the GitHub release.");
            }

            progress?.Report(new DesktopUpdateProgress("Verifying update integrity...", 80));
            await VerifySha256Async(archivePath, update.Sha256, cancellationToken).ConfigureAwait(false);

            progress?.Report(new DesktopUpdateProgress("Preparing update files...", 88));
            ExtractArchiveSafely(archivePath, payloadDirectory);

            var executablePath = Path.Combine(payloadDirectory, DesktopExecutableName);
            ValidatePreparedExecutable(executablePath, update.Version);
            progress?.Report(new DesktopUpdateProgress("Update ready to install.", 100));
            return new PreparedDesktopUpdate(update, workingDirectory, payloadDirectory, executablePath);
        }
        catch
        {
            TryDeleteDirectory(workingDirectory);
            throw;
        }
    }

    public void Dispose() => client.Dispose();

    public static string UpdateRootDirectory => Path.Combine(
        Path.GetTempPath(),
        "Chocobo Color Calculator",
        "Updates");

    public static void CleanupOldUpdates()
    {
        try
        {
            if (!Directory.Exists(UpdateRootDirectory))
                return;
            foreach (var directory in Directory.EnumerateDirectories(UpdateRootDirectory))
            {
                if (Directory.GetCreationTimeUtc(directory) < DateTime.UtcNow.AddDays(-2))
                    TryDeleteDirectory(directory);
            }
        }
        catch
        {
            // Update cleanup is best-effort and must never affect application startup.
        }
    }

    private static void ValidateUpdateMetadata(DesktopUpdate update)
    {
        if (!Uri.TryCreate(update.DownloadUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The update download URL is not trusted.");
        if (update.AssetSize <= 0 || update.AssetSize > MaximumArchiveBytes)
            throw new InvalidDataException("The update archive size is invalid.");
        if (update.Sha256.Length != 64 || update.Sha256.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidDataException("The GitHub release does not include a valid SHA-256 digest.");
    }

    private static async Task VerifySha256Async(
        string archivePath,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            archivePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        var actualSha256 = Convert.ToHexString(hash);
        if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The update failed its SHA-256 integrity check.");
    }

    private static void ExtractArchiveSafely(string archivePath, string destinationDirectory)
    {
        var destinationRoot = Path.GetFullPath(destinationDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count > 50 || archive.Entries.Sum(entry => entry.Length) > MaximumExtractedBytes)
            throw new InvalidDataException("The update archive contains an unexpected amount of data.");

        foreach (var entry in archive.Entries)
        {
            var destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, entry.FullName));
            if (!destinationPath.StartsWith(destinationRoot, StringComparison.Ordinal))
                throw new InvalidDataException("The update archive contains an unsafe path.");
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            entry.ExtractToFile(destinationPath, overwrite: true);
        }
    }

    private static void ValidatePreparedExecutable(string executablePath, Version expectedVersion)
    {
        if (!File.Exists(executablePath))
            throw new InvalidDataException($"The update does not contain {DesktopExecutableName}.");

        var fileVersionText = FileVersionInfo.GetVersionInfo(executablePath).FileVersion;
        if (!Version.TryParse(fileVersionText, out var fileVersion) ||
            fileVersion.Major != expectedVersion.Major ||
            fileVersion.Minor != expectedVersion.Minor ||
            fileVersion.Build != expectedVersion.Build)
            throw new InvalidDataException("The downloaded executable version does not match the release.");
    }

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

    private static (string DownloadUrl, long Size, string Sha256)? FindDesktopAsset(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var asset in assets.EnumerateArray())
        {
            if (!string.Equals(ReadString(asset, "name"), DesktopAssetName, StringComparison.OrdinalIgnoreCase))
                continue;
            var downloadUrl = ReadString(asset, "browser_download_url");
            var digest = ReadString(asset, "digest");
            var size = ReadInt64(asset, "size");
            if (string.IsNullOrWhiteSpace(downloadUrl) ||
                string.IsNullOrWhiteSpace(digest) ||
                !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                return null;
            return (downloadUrl, size, digest["sha256:".Length..]);
        }
        return null;
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static long ReadInt64(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.TryGetInt64(out var value)
            ? value
            : 0;

    private static bool ReadBoolean(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.True;

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch
        {
            // A future launch can remove a temporary directory that is still in use.
        }
    }
}
