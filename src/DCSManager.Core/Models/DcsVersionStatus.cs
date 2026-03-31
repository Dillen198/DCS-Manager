namespace DCSManager.Core.Models;

public record DcsVersionStatus
{
    public DcsInstall Install { get; init; } = null!;
    public string? InstalledVersion { get; init; }
    public string? LatestVersion { get; init; }
    public string? Branch { get; init; }
    public bool IsUpToDate => LatestVersion != null && InstalledVersion != null &&
                              InstalledVersion == LatestVersion;
    public bool HasUpdate => LatestVersion != null && InstalledVersion != null &&
                             InstalledVersion != LatestVersion;
    public bool CheckFailed { get; init; }
}
