using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DCSManager.Core.Models;

public record PluginDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("description")]
    public string Description { get; init; } = "";

    [JsonPropertyName("category")]
    public string Category { get; init; } = "";

    [JsonPropertyName("tags")]
    public List<string> Tags { get; init; } = new();

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;

    [JsonPropertyName("deprecationNotice")]
    public string? DeprecationNotice { get; init; }

    [JsonPropertyName("manualDownloadUrl")]
    public string? ManualDownloadUrl { get; init; }

    [JsonPropertyName("manualInstallNote")]
    public string? ManualInstallNote { get; init; }

    [JsonPropertyName("source")]
    public PluginSource Source { get; init; } = new();

    [JsonPropertyName("assetSelector")]
    public AssetSelector AssetSelector { get; init; } = new();

    [JsonPropertyName("installStrategy")]
    public InstallStrategy InstallStrategy { get; init; } = new();

    [JsonPropertyName("installPaths")]
    public List<InstallPath> InstallPaths { get; init; } = new();

    [JsonPropertyName("versionDetection")]
    public VersionDetection VersionDetection { get; init; } = new();

    [JsonPropertyName("iconUrl")]
    public string? IconUrl { get; init; }

    [JsonPropertyName("homepageUrl")]
    public string? HomepageUrl { get; init; }

    [JsonPropertyName("licenseType")]
    public string? LicenseType { get; init; }
}

public record PluginSource
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = ""; // "github_release" | "manual_url"

    [JsonPropertyName("owner")]
    public string? Owner { get; init; }

    [JsonPropertyName("repo")]
    public string? Repo { get; init; }
}

public record AssetSelector
{
    [JsonPropertyName("strategy")]
    public string Strategy { get; init; } = "name_regex"; // "name_regex" | "exact_name"

    [JsonPropertyName("pattern")]
    public string? Pattern { get; init; }

    [JsonPropertyName("preferredAssetName")]
    public string? PreferredAssetName { get; init; }
}

public record InstallStrategy
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = ""; // "run_installer_exe" | "run_installer_msi" | "extract_zip_to_folder" | "open_browser_url"

    [JsonPropertyName("silentArgs")]
    public string? SilentArgs { get; init; }

    [JsonPropertyName("requiresAdmin")]
    public bool RequiresAdmin { get; init; }

    [JsonPropertyName("installerAssetPattern")]
    public string? InstallerAssetPattern { get; init; }
}

public record InstallPath
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("label")]
    public string Label { get; init; } = "";

    [JsonPropertyName("path")]
    public string? Path { get; init; }

    [JsonPropertyName("pathTemplate")]
    public string? PathTemplate { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = "fixed"; // "fixed" | "saved_games_relative"
}

public record VersionDetection
{
    [JsonPropertyName("strategy")]
    public string Strategy { get; init; } = "state_file_only"; // "exe_file_version" | "state_file_only" | "folder_manifest"

    [JsonPropertyName("path")]
    public string? Path { get; init; }
}
