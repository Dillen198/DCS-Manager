using System;

namespace DCSManager.Core.Models;

public record UpdateCheckResult
{
    public string PluginId { get; init; } = "";
    public string? CurrentVersion { get; init; }
    public string? LatestVersion { get; init; }
    public bool UpdateAvailable { get; init; }
    public string? AssetDownloadUrl { get; init; }
    public string? AssetFileName { get; init; }
    public long? AssetSizeBytes { get; init; }
    public DateTimeOffset? ReleasedAt { get; init; }
    public string? ReleaseNotes { get; init; }
    public UpdateCheckStatus Status { get; init; }
    public string? ErrorMessage { get; init; }
}

public enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    NotInstalled,
    Error,
    ManualOnly
}
