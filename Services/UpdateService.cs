using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace SJHotkeys.Services;

public sealed class UpdateService
{
    private const string LatestReleaseApi = "https://api.github.com/repos/sjstudios00/SJHotkeys/releases/latest";
    private static readonly HttpClient Client = CreateClient();
    public bool LatestReleaseExists { get; private set; }

    public async Task<(Version Version, string Tag, string AssetUrl, string AssetName)?> CheckAsync(CancellationToken cancellationToken = default)
    {
        LatestReleaseExists = false;
        using var response = await Client.GetAsync(LatestReleaseApi, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        LatestReleaseExists = true;
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var root = document.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest)) return null;
        var current = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0);
        if (latest <= current) return null;

        var processPath = Environment.ProcessPath;
        var portable = processPath is not null && new FileInfo(processPath).Length > 20 * 1024 * 1024;
        var assetName = portable
            ? $"SJ-Hotkeys-v{latest}-win-x64-portable.exe"
            : $"SJ-Hotkeys-v{latest}-win-x64-lightweight.exe";
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() == assetName)
            {
                var url = asset.GetProperty("browser_download_url").GetString();
                if (!Uri.TryCreate(url, UriKind.Absolute, out var assetUri) || assetUri.Scheme != Uri.UriSchemeHttps || assetUri.Host != "github.com")
                    throw new InvalidDataException("The release contains an invalid update download URL.");
                return (latest, tag, assetUri.AbsoluteUri, assetName);
            }
        }
        throw new InvalidDataException($"The release {tag} does not contain the expected update file {assetName}.");
    }

    public async Task<string> DownloadAndVerifyAsync(string assetUrl, string assetName, CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(Path.GetTempPath(), "SJ Hotkeys", "updates");
        Directory.CreateDirectory(directory);
        var downloadPath = Path.Combine(directory, assetName);
        var checksumPath = Path.Combine(directory, "SHA256SUMS.txt");
        using (var response = await Client.GetAsync(assetUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);
            await input.CopyToAsync(output, cancellationToken);
        }

        var checksumUrl = assetUrl[..(assetUrl.LastIndexOf('/') + 1)] + "SHA256SUMS.txt";
        using (var response = await Client.GetAsync(checksumUrl, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            await using var output = File.Create(checksumPath);
            await response.Content.CopyToAsync(output, cancellationToken);
        }
        var expected = (await File.ReadAllLinesAsync(checksumPath, cancellationToken))
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .FirstOrDefault(parts => parts.Length >= 2 && parts[^1] == assetName)?[0];
        if (string.IsNullOrWhiteSpace(expected)) throw new InvalidDataException("The release checksum file does not list the downloaded update.");
        await using var downloaded = File.OpenRead(downloadPath);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(downloaded, cancellationToken));
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), Convert.FromHexString(actual)))
            throw new InvalidDataException("The update file failed its SHA-256 integrity check.");
        return downloadPath;
    }

    public static void StartInstaller(string downloadedPath)
    {
        var appPath = Environment.ProcessPath ?? throw new InvalidOperationException("The application path is unavailable.");
        var helper = new ProcessStartInfo(appPath) { UseShellExecute = true };
        helper.ArgumentList.Add("--apply-update");
        helper.ArgumentList.Add(appPath);
        helper.ArgumentList.Add(downloadedPath);
        helper.ArgumentList.Add(Environment.ProcessId.ToString());
        _ = Process.Start(helper) ?? throw new InvalidOperationException("The update helper process could not be started.");
        if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow)
            mainWindow.PrepareForShutdown();
        else
            System.Windows.Application.Current.Shutdown();
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SJ-Hotkeys-Updater/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}
