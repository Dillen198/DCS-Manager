using System.Collections.Generic;
using DCSManager.Core.Models;

namespace DCSManager.Core.Interfaces;

public interface IPluginStateStore
{
    PluginInstallState? GetState(string pluginId);
    void SetState(string pluginId, PluginInstallState state);
    IReadOnlyList<UpdateHistoryEntry> GetHistory(string? pluginId = null);
    void AddHistoryEntry(UpdateHistoryEntry entry);
    void Save();

    AppSettings GetAppSettings();
    void SaveAppSettings(AppSettings settings);
}
