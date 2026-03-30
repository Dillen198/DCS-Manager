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
                "folder_manifest" => GetFolderManifestVersion(plugin.VersionDetection.Path),
                _ => _stateStore.GetState(plugin.Id)?.InstalledVersion
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
}
