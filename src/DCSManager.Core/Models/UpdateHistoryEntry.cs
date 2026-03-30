using System;
using System.Text.Json.Serialization;

namespace DCSManager.Core.Models;

public record UpdateHistoryEntry
{
    [JsonPropertyName("pluginId")]
    public string PluginId { get; init; } = "";

    [JsonPropertyName("fromVersion")]
    public string? FromVersion { get; init; }

    [JsonPropertyName("toVersion")]
    public string ToVersion { get; init; } = "";

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }
}
