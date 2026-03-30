using System;
using System.Threading;
using System.Threading.Tasks;
using DCSManager.Core.Models;

namespace DCSManager.Core.Interfaces;

public interface IPluginInstallerService
{
    Task<InstallResult> InstallAsync(
        PluginDefinition plugin,
        UpdateCheckResult updateInfo,
        IProgress<InstallProgress> progress,
        CancellationToken ct = default);
}
