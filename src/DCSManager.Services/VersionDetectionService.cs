using System;
using System.Diagnostics;
using System.IO;
using DCSManager.Core.Interfaces;
using DCSManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace DCSManager.Services;

public class VersionDetectionService : IVersionDetectionService
{
    private readonly IPluginStateStore _stateStore;
    private readonly ILogger<VersionDetectionService> _logger;

    public VersionDetectionService(IPluginStateStore stateStore, ILogger<VersionDetectionService> logger)
    {
        _stateStore = stateStore;
        _logger = logger;
    }

    public string? GetInstalledVersion(PluginDefinition plugin)
    {
        try
        {
            return plugin.VersionDetection.Strategy switch
            {
                "exe_file_version" => GetExeFileVersion(plugin.VersionDetection.Path),
                "folder_manifest"  => GetFolderManifestVersion(plugin.VersionDetection.Path),
                "folder_exists"    => GetFolderExistsVersion(plugin.VersionDetection.Path),
                _                  => _stateStore.GetState(plugin.Id)?.InstalledVersion
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to detect version for {Plugin}", plugin.Id);
            return null;
        }
    }

    private static string? GetExeFileVersion(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        var info = FileVersionInfo.GetVersionInfo(path);
        return string.IsNullOrWhiteSpace(info.FileVersion) ? info.ProductVersion : info.FileVersion;
    }

    private static string? GetFolderManifestVersion(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        var versionFile = Path.Combine(path, "version.txt");
        if (File.Exists(versionFile))
            return File.ReadAllText(versionFile).Trim();

        return null;
    }

    /// <summary>
    /// Returns "installed" if the folder exists, null otherwise.
    /// Supports {SavedGames} token which expands to the user's Saved Games folder.
    /// </summary>
    private static string? GetFolderExistsVersion(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        var expanded = ExpandSavedGamesToken(path);
        return Directory.Exists(expanded) ? "installed" : null;
    }

    private static string ExpandSavedGamesToken(string path)
    {
        const string shellFoldersKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders";
        const string savedGamesGuid  = "{4C5C32FF-BB9D-43B0-B5B4-2D72E54EAAA4}";

        string savedGamesRoot;
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(shellFoldersKey);
            var raw = key?.GetValue(savedGamesGuid) as string;
            savedGamesRoot = !string.IsNullOrWhiteSpace(raw)
                ? Environment.ExpandEnvironmentVariables(raw)
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games");
        }
        catch
        {
            savedGamesRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games");
        }

        return path
            .Replace("{SavedGamesStable}",   Path.Combine(savedGamesRoot, "DCS"))
            .Replace("{SavedGamesOpenBeta}", Path.Combine(savedGamesRoot, "DCS.openbeta"))
            .Replace("{SavedGames}",         savedGamesRoot);
    }
}
