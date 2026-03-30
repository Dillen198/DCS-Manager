using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DCSManager.Core.Interfaces;
using DCSManager.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Semver;

namespace DCSManager.Services;

public class UpdateOrchestrator : BackgroundService, IUpdateOrchestrator
{
    private readonly ICatalogService _catalog;
    private readonly IGitHubReleaseService _github;
    private readonly IVersionDetectionService _versionDetection;
    private readonly IPluginInstallerService _installer;
    private readonly IPluginStateStore _stateStore;
    private readonly IDcsProcessMonitor _dcsMonitor;
    private readonly ILogger<UpdateOrchestrator> _logger;

    private readonly List<UpdateCheckResult> _pendingUpdates = new();
    private readonly SemaphoreSlim _checkLock = new(1, 1);
    private TimeSpan _checkInterval = TimeSpan.FromMinutes(20);

    public IReadOnlyList<UpdateCheckResult> PendingUpdates => _pendingUpdates.AsReadOnly();
    public DateTimeOffset? LastCheckedAt { get; private set; }
    public bool IsChecking { get; private set; }
    public bool IsInstalling { get; private set; }

    public event EventHandler? StateChanged;
    public event EventHandler<UpdateCheckResult>? UpdateAvailable;
    public event EventHandler<UpdateHistoryEntry>? UpdateApplied;

    public UpdateOrchestrator(
        ICatalogService catalog,
        IGitHubReleaseService github,
        IVersionDetectionService versionDetection,
        IPluginInstallerService installer,
        IPluginStateStore stateStore,
        IDcsProcessMonitor dcsMonitor,
        ILogger<UpdateOrchestrator> logger)
    {
        _catalog = catalog;
        _github = github;
        _versionDetection = versionDetection;
        _installer = installer;
        _stateStore = stateStore;
        _dcsMonitor = dcsMonitor;
        _logger = logger;

        _dcsMonitor.DcsExited += OnDcsExited;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial check shortly after startup
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await CheckNowAsync(stoppingToken);
            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    public async Task CheckNowAsync(CancellationToken ct = default)
    {
        if (!await _checkLock.WaitAsync(0)) return; // skip if already running
        try
        {
            IsChecking = true;
            StateChanged?.Invoke(this, EventArgs.Empty);

            var plugins = await _catalog.GetCatalogAsync(ct);
            var newUpdates = new List<UpdateCheckResult>();

            foreach (var plugin in plugins.Where(p => p.Enabled))
            {
                var result = await CheckPluginAsync(plugin, ct);
                if (result.Status == UpdateCheckStatus.UpdateAvailable)
                {
                    newUpdates.Add(result);
                    if (!_pendingUpdates.Any(p => p.PluginId == plugin.Id))
                        UpdateAvailable?.Invoke(this, result);
                }

                var state = _stateStore.GetState(plugin.Id) ?? new PluginInstallState { PluginId = plugin.Id };
                state.LastCheckedVersion = result.LatestVersion;
                state.LastCheckedAt = DateTimeOffset.UtcNow;
                _stateStore.SetState(plugin.Id, state);
            }

            _pendingUpdates.RemoveAll(p => !newUpdates.Any(n => n.PluginId == p.PluginId));
            foreach (var update in newUpdates.Where(n => !_pendingUpdates.Any(p => p.PluginId == n.PluginId)))
                _pendingUpdates.Add(update);

            _stateStore.Save();
            LastCheckedAt = DateTimeOffset.UtcNow;

            // Auto-apply if DCS not running
            if (_pendingUpdates.Count > 0 && !_dcsMonitor.IsDcsRunning())
                await ApplyAllPendingAsync(new Progress<InstallProgress>(), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error during update check");
        }
        finally
        {
            IsChecking = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task ApplyUpdateAsync(string pluginId, IProgress<InstallProgress> progress, CancellationToken ct = default)
    {
        var update = _pendingUpdates.FirstOrDefault(u => u.PluginId == pluginId);
        if (update == null) return;

        var plugins = await _catalog.GetCatalogAsync(ct);
        var plugin = plugins.FirstOrDefault(p => p.Id == pluginId);
        if (plugin == null) return;

        IsInstalling = true;
        StateChanged?.Invoke(this, EventArgs.Empty);

        var state = _stateStore.GetState(pluginId) ?? new PluginInstallState { PluginId = pluginId };
        var fromVersion = state.InstalledVersion;

        try
        {
            var result = await _installer.InstallAsync(plugin, update, progress, ct);

            var entry = new UpdateHistoryEntry
            {
                PluginId = pluginId,
                FromVersion = fromVersion,
                ToVersion = result.InstalledVersion ?? update.LatestVersion ?? "",
                Timestamp = DateTimeOffset.UtcNow,
                Success = result.Success,
                ErrorMessage = result.ErrorMessage
            };

            if (result.Success)
            {
                state.InstalledVersion = result.InstalledVersion ?? update.LatestVersion;
                state.InstalledAt = DateTimeOffset.UtcNow;
                _stateStore.SetState(pluginId, state);
                _pendingUpdates.RemoveAll(u => u.PluginId == pluginId);
            }

            _stateStore.AddHistoryEntry(entry);
            _stateStore.Save();
            UpdateApplied?.Invoke(this, entry);
        }
        finally
        {
            IsInstalling = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task ApplyAllPendingAsync(IProgress<InstallProgress> progress, CancellationToken ct = default)
    {
        var toApply = _pendingUpdates.ToList();
        foreach (var update in toApply)
        {
            if (ct.IsCancellationRequested) break;
            var state = _stateStore.GetState(update.PluginId);
            if (state?.AutoUpdate == false) continue;
            await ApplyUpdateAsync(update.PluginId, progress, ct);
        }
    }

    private async Task<UpdateCheckResult> CheckPluginAsync(PluginDefinition plugin, CancellationToken ct)
    {
        if (plugin.Source.Type != "github_release")
            return new UpdateCheckResult { PluginId = plugin.Id, Status = UpdateCheckStatus.ManualOnly };

        try
        {
            var release = await _github.GetLatestReleaseAsync(plugin.Source.Owner!, plugin.Source.Repo!, ct);
            if (release == null)
                return new UpdateCheckResult { PluginId = plugin.Id, Status = UpdateCheckStatus.Error, ErrorMessage = "No release found" };

            var latestTag = release.TagName.TrimStart('v');
            var installedVersion = _versionDetection.GetInstalledVersion(plugin);
            var isNewer = IsNewer(latestTag, installedVersion);

            // Find matching asset
            string? assetUrl = null, assetName = null;
            long? assetSize = null;
            if (plugin.AssetSelector.Pattern != null)
            {
                var regex = new Regex(plugin.AssetSelector.Pattern, RegexOptions.IgnoreCase);
                foreach (var asset in release.Assets)
                {
                    if (regex.IsMatch(asset.Name))
                    {
                        assetUrl = asset.BrowserDownloadUrl;
                        assetName = asset.Name;
                        assetSize = asset.Size;
                        break;
                    }
                }
            }

            return new UpdateCheckResult
            {
                PluginId = plugin.Id,
                CurrentVersion = installedVersion,
                LatestVersion = release.TagName,
                UpdateAvailable = isNewer,
                AssetDownloadUrl = assetUrl,
                AssetFileName = assetName,
                AssetSizeBytes = assetSize,
                ReleasedAt = release.PublishedAt,
                ReleaseNotes = release.Body,
                Status = installedVersion == null ? UpdateCheckStatus.NotInstalled
                       : isNewer ? UpdateCheckStatus.UpdateAvailable
                       : UpdateCheckStatus.UpToDate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check {Plugin}", plugin.Id);
            return new UpdateCheckResult { PluginId = plugin.Id, Status = UpdateCheckStatus.Error, ErrorMessage = ex.Message };
        }
    }

    private static bool IsNewer(string latest, string? current)
    {
        if (string.IsNullOrEmpty(current)) return true;
        if (latest == current) return false;

        try
        {
            if (SemVersion.TryParse(latest, SemVersionStyles.Any, out var latestSem) &&
                SemVersion.TryParse(current, SemVersionStyles.Any, out var currentSem))
                return SemVersion.ComparePrecedence(latestSem, currentSem) > 0;
        }
        catch { /* fall through */ }

        if (Version.TryParse(latest, out var latestV) && Version.TryParse(current, out var currentV))
            return latestV > currentV;

        return string.Compare(latest, current, StringComparison.OrdinalIgnoreCase) > 0;
    }

    private async void OnDcsExited(object? sender, EventArgs e)
    {
        if (_pendingUpdates.Count == 0) return;
        _logger.LogInformation("DCS exited, applying {Count} pending updates", _pendingUpdates.Count);
        await ApplyAllPendingAsync(new Progress<InstallProgress>());
    }

    public override void Dispose()
    {
        _dcsMonitor.DcsExited -= OnDcsExited;
        base.Dispose();
    }
}
