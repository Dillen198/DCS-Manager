using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DCSManager.Core.Interfaces;
using DCSManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace DCSManager.Services;

public class PluginInstallerService : IPluginInstallerService
{
    private static readonly string TempBase = Path.Combine(Path.GetTempPath(), "DCSManager");

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PluginInstallerService> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _pluginLocks = new();

    public PluginInstallerService(IHttpClientFactory httpClientFactory, ILogger<PluginInstallerService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<InstallResult> InstallAsync(
        PluginDefinition plugin,
        UpdateCheckResult updateInfo,
        IProgress<InstallProgress> progress,
        CancellationToken ct = default)
    {
        var sem = _pluginLocks.GetOrAdd(plugin.Id, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct);
        try
        {
            return await DoInstallAsync(plugin, updateInfo, progress, ct);
        }
        finally
        {
            sem.Release();
        }
    }

    private async Task<InstallResult> DoInstallAsync(
        PluginDefinition plugin,
        UpdateCheckResult updateInfo,
        IProgress<InstallProgress> progress,
        CancellationToken ct)
    {
        var strategy = plugin.InstallStrategy.Type;

        if (strategy == "open_browser_url")
        {
            var url = plugin.ManualDownloadUrl ?? plugin.HomepageUrl ?? "";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return new InstallResult(true, null, null);
        }

        if (string.IsNullOrEmpty(updateInfo.AssetDownloadUrl))
            return new InstallResult(false, null, "No download URL available");

        var tempDir = Path.Combine(TempBase, plugin.Id);
        Directory.CreateDirectory(tempDir);
        var fileName = updateInfo.AssetFileName ?? Path.GetFileName(updateInfo.AssetDownloadUrl);
        var destFile = Path.Combine(tempDir, fileName);

        try
        {
            // Download
            progress.Report(new InstallProgress("Downloading", 0, $"Downloading {fileName}..."));
            await DownloadFileAsync(updateInfo.AssetDownloadUrl, destFile, updateInfo.AssetSizeBytes, progress, ct);

            // Verify
            progress.Report(new InstallProgress("Verifying", 95, "Verifying download..."));
            if (updateInfo.AssetSizeBytes.HasValue)
            {
                var fileSize = new FileInfo(destFile).Length;
                if (fileSize != updateInfo.AssetSizeBytes.Value)
                    return new InstallResult(false, null, $"Download size mismatch: expected {updateInfo.AssetSizeBytes}, got {fileSize}");
            }

            // Install
            progress.Report(new InstallProgress("Installing", 97, "Installing..."));
            var result = strategy switch
            {
                "run_installer_exe" => await RunInstallerAsync(destFile, plugin.InstallStrategy.SilentArgs ?? "/S", ct),
                "run_installer_msi" => await RunMsiInstallerAsync(destFile, ct),
                "extract_zip_to_folder" => ExtractZip(destFile, plugin),
                _ => new InstallResult(false, null, $"Unknown install strategy: {strategy}")
            };

            if (result.Success)
                progress.Report(new InstallProgress("Done", 100, "Installation complete"));

            return result with { InstalledVersion = updateInfo.LatestVersion };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Install failed for {Plugin}", plugin.Id);
            return new InstallResult(false, null, ex.Message);
        }
        finally
        {
            // Cleanup temp
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    private async Task DownloadFileAsync(string url, string destFile, long? expectedSize,
        IProgress<InstallProgress> progress, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("download");
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? expectedSize ?? -1;

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long downloaded = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
            downloaded += read;
            if (totalBytes > 0)
            {
                var pct = (double)downloaded / totalBytes * 90; // 0-90% for download phase
                progress.Report(new InstallProgress("Downloading", pct,
                    $"Downloading {downloaded / 1024 / 1024:F1} MB / {totalBytes / 1024 / 1024:F1} MB"));
            }
        }
    }

    private async Task<InstallResult> RunInstallerAsync(string exePath, string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = args,
            UseShellExecute = true,
            Verb = "runas"
        };
        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start installer");
        await proc.WaitForExitAsync(ct);
        return proc.ExitCode == 0
            ? new InstallResult(true, null, null)
            : new InstallResult(false, null, $"Installer exited with code {proc.ExitCode}");
    }

    private async Task<InstallResult> RunMsiInstallerAsync(string msiPath, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "msiexec.exe",
            Arguments = $"/i \"{msiPath}\" /quiet /norestart",
            UseShellExecute = true,
            Verb = "runas"
        };
        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start msiexec");
        await proc.WaitForExitAsync(ct);
        return proc.ExitCode == 0
            ? new InstallResult(true, null, null)
            : new InstallResult(false, null, $"MSI installer exited with code {proc.ExitCode}");
    }

    private InstallResult ExtractZip(string zipPath, PluginDefinition plugin)
    {
        foreach (var installPath in plugin.InstallPaths)
        {
            var target = ResolveInstallPath(installPath);
            if (string.IsNullOrEmpty(target)) continue;
            Directory.CreateDirectory(target);
            ZipFile.ExtractToDirectory(zipPath, target, overwriteFiles: true);
            _logger.LogInformation("Extracted {Plugin} to {Target}", plugin.Id, target);
        }
        return new InstallResult(true, null, null);
    }

    private static string? ResolveInstallPath(InstallPath path)
    {
        if (path.Type == "fixed")
            return path.Path;

        if (path.Type == "saved_games_relative" && path.PathTemplate != null)
        {
            var savedGames = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Saved Games");
            return path.PathTemplate
                .Replace("{SavedGamesStable}", Path.Combine(savedGames, "DCS"))
                .Replace("{SavedGamesOpenBeta}", Path.Combine(savedGames, "DCS.openbeta"));
        }

        return null;
    }
}
