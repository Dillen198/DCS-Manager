using System;
using System.Threading;
using System.Threading.Tasks;

namespace DCSManager.Core.Interfaces;

public interface IDcsProcessMonitor
{
    bool IsDcsRunning();
    Task WaitForDcsExitAsync(CancellationToken ct = default);
    event EventHandler? DcsStarted;
    event EventHandler? DcsExited;
}
