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

        var results = new List<DcsInstall>();

        var stable = TryDetectInstall(DcsInstallType.Stable);
        if (stable != null) results.Add(stable);

        var beta = TryDetectInstall(DcsInstallType.OpenBeta);
        if (beta != null) results.Add(beta);

        _cached = results;
        return results;
    }

    public DcsInstall? GetInstall(DcsInstallType type)
    {
        foreach (var install in DetectInstalls())
            if (install.Type == type) return install;
        return null;
    }

    private DcsInstall? TryDetectInstall(DcsInstallType type)
    {
        var regKey = type == DcsInstallType.OpenBeta
            ? @"SOFTWARE\Eagle Dynamics\DCS World OpenBeta"
            : @"SOFTWARE\Eagle Dynamics\DCS World";

        string? installPath = null;

        // Try registry (64-bit then 32-bit view)
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            using var key = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64)
                                       .OpenSubKey(regKey);
            if (key?.GetValue("Path") is string p && Directory.Exists(p))
            {
                installPath = p;
                break;
            }
        }

        // Fallback: common paths
        if (installPath == null)
        {
            var fallbacks = type == DcsInstallType.OpenBeta
                ? new[] { @"F:\DCS World OpenBeta", @"C:\Program Files\Eagle Dynamics\DCS World OpenBeta" }
                : new[] { @"F:\DCS World", @"C:\Program Files\Eagle Dynamics\DCS World" };

            foreach (var fb in fallbacks)
                if (Directory.Exists(fb)) { installPath = fb; break; }
        }

        if (installPath == null) return null;

        // Resolve exe path (prefer MT build)
        var exePath = Path.Combine(installPath, "bin-mt", "DCS.exe");
        if (!File.Exists(exePath))
            exePath = Path.Combine(installPath, "bin", "DCS.exe");

        if (!File.Exists(exePath))
        {
            _logger.LogWarning("DCS install at {Path} has no DCS.exe", installPath);
            return null;
        }

        var savedGames = ResolveSavedGames(type);
        var version = ReadDcsVersion(installPath);

        return new DcsInstall
        {
            Type = type,
            InstallPath = installPath,
            ExePath = exePath,
            SavedGamesPath = savedGames,
            Version = version
        };
    }

    private static string ResolveSavedGames(DcsInstallType type)
    {
        var savedGamesRoot = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        // Try FOLDERID_SavedGames via known path
        var savedGames = Path.Combine(savedGamesRoot, "Saved Games");
        if (!Directory.Exists(savedGames))
            savedGames = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Saved Games");

        var suffix = type == DcsInstallType.OpenBeta ? "DCS.openbeta" : "DCS";
        return Path.Combine(savedGames, suffix);
    }

    private string? ReadDcsVersion(string installPath)
    {
        try
        {
            var cfgPath = Path.Combine(installPath, "autoupdate.cfg");
            if (!File.Exists(cfgPath)) return null;
            var json = File.ReadAllText(cfgPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("version", out var v))
                return v.GetString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read DCS version");
        }
        return null;
    }
}
