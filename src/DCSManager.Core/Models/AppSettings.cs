namespace DCSManager.Core.Models;

/// <summary>Persisted application-level settings (stored in state.json).</summary>
public class AppSettings
{
    /// <summary>
    /// The SavedGames path of the DCS install chosen as the "primary" installation.
    /// Used when installing mods that target a saved_games_relative path.
    /// If null the first detected install is used.
    /// </summary>
    public string? PrimarySavedGamesPath { get; set; }

    /// <summary>Check interval in minutes.</summary>
    public int CheckIntervalMinutes { get; set; } = 20;
}
