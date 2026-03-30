using DCSManager.Core.Models;

namespace DCSManager.Core.Interfaces;

public interface IVersionDetectionService
{
    string? GetInstalledVersion(PluginDefinition plugin);
}
