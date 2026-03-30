namespace DCSManager.Core.Models;

public record InstallProgress(string Stage, double PercentComplete, string? StatusMessage = null);

public record InstallResult(bool Success, string? InstalledVersion, string? ErrorMessage);
