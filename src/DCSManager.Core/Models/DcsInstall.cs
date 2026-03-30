namespace DCSManager.Core.Models;

public enum DcsInstallType { Stable, OpenBeta }

public record DcsInstall
{
    public DcsInstallType Type { get; init; }
    public string InstallPath { get; init; } = "";
    public string ExePath { get; init; } = "";
    public string SavedGamesPath { get; init; } = "";
    public string? Version { get; init; }

    public string Label => Type == DcsInstallType.OpenBeta ? "DCS Open Beta" : "DCS World (Stable)";
}
