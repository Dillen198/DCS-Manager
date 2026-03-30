using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DCSManager.Core.Interfaces;
using DCSManager.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace DCSManager.Services;

public class DcsInstallDetector : IDcsInstallDetector
{
    private readonly ILogger<DcsInstallDetector> _logger;
    private List<DcsInstall>? _cached;

    public DcsInstallDetector(ILogger<DcsInstallDetector> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<DcsInstall> DetectInstalls()
    {
        if (_cached != null) return _cached;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<DcsInstall>();

        foreach (var path in CandidatePaths())
        {
            if (!Directory.Exists(path)) continue;
            var normalized = Path.GetFullPath(path).TrimEnd('\\');
            if (!seen.Add(normalized)) continue;

            var install = TryBuildInstall(normalized);
            if (install != null) results.Add(install);
        }

        _cached = results;
        _logger.LogInformation("Detected {Count} DCS install(s)", results.Count);
        return results;
    }

    public DcsInstall? GetInstall(DcsInstallType type)
    {
        foreach (var install in DetectInstalls())
            if (install.Type == type) return install;
        return null;
    }

    // ── candidate paths ──────────────────────────────────────────────────────

    private static IEnumerable<string> CandidatePaths()
    {
        // Registry (Eagle Dynamics keys, both old and new naming)
        var regKeys = new[]
        {
            @"SOFTWARE\Eagle Dynamics\DCS World",
            @"SOFTWARE\Eagle Dynamics\DCS World OpenBeta",
        };
        foreach (var regKey in regKeys)
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            using var key = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64).OpenSubKey(regKey);
            if (key?.GetValue("Path") is string p && !string.IsNullOrWhiteSpace(p))
                yield return p;
        }

        // Common folder names on all drives C-G
        var drives = new[] { "C", "D", "E", "F", "G" };
        var names  = new[] { "DCS World", "DCS World OpenBeta", "DCS" };
        foreach (var drive in drives)
        foreach (var name in names)
        {
            yield return $@"{drive}:\{name}";
            yield return $@"{drive}:\Program Files\Eagle Dynamics\{name}";
            yield return $@"{drive}:\Games\{name}";
        }
    }

    // ── build a DcsInstall from a confirmed path ──────────────────────────────

    private DcsInstall? TryBuildInstall(string installPath)
    {
        var exePath = Path.Combine(installPath, "bin-mt", "DCS.exe");
        if (!File.Exists(exePath))
            exePath = Path.Combine(installPath, "bin", "DCS.exe");
        if (!File.Exists(exePath))
        {
            _logger.LogDebug("No DCS.exe at {Path}", installPath);
            return null;
        }

        var (version, branch) = ReadAutoupdateCfg(installPath);
        var type = BranchToType(branch);
        var savedGames = ResolveSavedGames(type);

        _logger.LogInformation("Found DCS {Type} at {Path} (branch={Branch}, SavedGames={SG})",
            type, installPath, branch ?? "stable", savedGames);

        return new DcsInstall
        {
            Type        = type,
            Branch      = branch,
            InstallPath = installPath,
            ExePath     = exePath,
            SavedGamesPath = savedGames,
            Version     = version
        };
    }

    // ── autoupdate.cfg ────────────────────────────────────────────────────────

    private (string? version, string? branch) ReadAutoupdateCfg(string installPath)
    {
        try
        {
            var cfgPath = Path.Combine(installPath, "autoupdate.cfg");
            if (!File.Exists(cfgPath)) return (null, null);

            var json = File.ReadAllText(cfgPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var version = root.TryGetProperty("version", out var v) ? v.GetString() : null;
            var branch  = root.TryGetProperty("branch",  out var b) ? b.GetString() : null;
            return (version, branch);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read autoupdate.cfg at {Path}", installPath);
            return (null, null);
        }
    }

    private static DcsInstallType BranchToType(string? branch) => branch?.ToLowerInvariant() switch
    {
        "openbeta" => DcsInstallType.OpenBeta,
        "testing"  => DcsInstallType.Testing,
        _          => DcsInstallType.Stable   // null / "stable" / anything else
    };

    // ── saved games ───────────────────────────────────────────────────────────

    private static string ResolveSavedGames(DcsInstallType type)
    {
        var root = GetSavedGamesRoot();
        var suffix = type == DcsInstallType.OpenBeta ? "DCS.openbeta" : "DCS";
        return Path.Combine(root, suffix);
    }

    /// <summary>
    /// Returns the user's actual Saved Games folder, even if relocated via shell settings.
    /// Uses FOLDERID_SavedGames from the registry (User Shell Folders).
    /// </summary>
    private static string GetSavedGamesRoot()
    {
        const string shellFoldersKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders";
        const string savedGamesGuid  = "{4C5C32FF-BB9D-43B0-B5B4-2D72E54EAAA4}";

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(shellFoldersKey);
            if (key?.GetValue(savedGamesGuid) is string raw && !string.IsNullOrWhiteSpace(raw))
            {
                var expanded = Environment.ExpandEnvironmentVariables(raw);
                if (Directory.Exists(expanded)) return expanded;
            }
        }
        catch { /* fall through */ }

        // Fallback: %USERPROFILE%\Saved Games
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games");
    }
}
