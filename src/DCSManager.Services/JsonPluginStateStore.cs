using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using DCSManager.Core.Interfaces;
using DCSManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace DCSManager.Services;

public class JsonPluginStateStore : IPluginStateStore
{
    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DCSManager");

    private static readonly string StateFile = Path.Combine(AppDataDir, "state.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILogger<JsonPluginStateStore> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private StateRoot _state;

    public JsonPluginStateStore(ILogger<JsonPluginStateStore> logger)
    {
        _logger = logger;
        _state = Load();
    }

    public PluginInstallState? GetState(string pluginId)
        => _state.Plugins.TryGetValue(pluginId, out var s) ? s : null;

    public void SetState(string pluginId, PluginInstallState state)
    {
        _state.Plugins[pluginId] = state;
    }

    public IReadOnlyList<UpdateHistoryEntry> GetHistory(string? pluginId = null)
    {
        if (pluginId == null) return _state.History.AsReadOnly();
        return _state.History.Where(h => h.PluginId == pluginId).ToList();
    }

    public void AddHistoryEntry(UpdateHistoryEntry entry)
    {
        _state.History.Insert(0, entry);
        if (_state.History.Count > 500)
            _state.History.RemoveRange(500, _state.History.Count - 500);
    }

    public void Save()
    {
        _writeLock.Wait();
        try
        {
            Directory.CreateDirectory(AppDataDir);
            var tmp = StateFile + ".tmp";
            var json = JsonSerializer.Serialize(_state, JsonOptions);
            File.WriteAllText(tmp, json);
            File.Move(tmp, StateFile, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save state");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private StateRoot Load()
    {
        try
        {
            if (!File.Exists(StateFile))
                return new StateRoot();

            var json = File.ReadAllText(StateFile);
            return JsonSerializer.Deserialize<StateRoot>(json) ?? new StateRoot();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load state, starting fresh");
            return new StateRoot();
        }
    }

    private class StateRoot
    {
        public Dictionary<string, PluginInstallState> Plugins { get; set; } = new();
        public List<UpdateHistoryEntry> History { get; set; } = new();
    }
}
