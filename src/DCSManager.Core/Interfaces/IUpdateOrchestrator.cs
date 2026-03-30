using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DCSManager.Core.Models;

namespace DCSManager.Core.Interfaces;

public interface IUpdateOrchestrator
{
    IReadOnlyList<UpdateCheckResult> PendingUpdates { get; }
    DateTimeOffset? LastCheckedAt { get; }
    bool IsChecking { get; }
    bool IsInstalling { get; }

    Task CheckNowAsync(CancellationToken ct = default);
    Task ApplyUpdateAsync(string pluginId, IProgress<InstallProgress> progress, CancellationToken ct = default);
    Task ApplyAllPendingAsync(IProgress<InstallProgress> progress, CancellationToken ct = default);

    event EventHandler? StateChanged;
    event EventHandler<UpdateCheckResult>? UpdateAvailable;
    event EventHandler<UpdateHistoryEntry>? UpdateApplied;
}
