namespace DCSManager.Core.Models;

public enum DcsInstallType { Stable, OpenBeta, Testing }

public record DcsInstall
{
    public DcsInstallType Type { get; init; }
    public string InstallPath { get; init; } = "";
    public string ExePath { get; init; } = "";
    public string SavedGamesPath { get; init; } = "";
    public string? Version { get; init; }
    public string? Branch { get; init; }

    public string Label => Type switch
    {
        DcsInstallType.OpenBeta => "DCS World (Open Beta)",
        DcsInstallType.Testing  => "DCS World (Testing)",
        _                       => "DCS World (Stable)"
    };
}
