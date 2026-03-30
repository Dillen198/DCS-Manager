using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DCSManager.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DCSManager.Services;

public class DcsProcessMonitor : IDcsProcessMonitor, IDisposable
{
    private readonly ILogger<DcsProcessMonitor> _logger;
    private Timer? _pollTimer;
    private bool _wasRunning;

    public event EventHandler? DcsStarted;
    public event EventHandler? DcsExited;

    public DcsProcessMonitor(ILogger<DcsProcessMonitor> logger)
    {
        _logger = logger;
        _wasRunning = IsDcsRunning();
        _pollTimer = new Timer(Poll, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }

    public bool IsDcsRunning()
        => Process.GetProcessesByName("DCS").Length > 0;

    public async Task WaitForDcsExitAsync(CancellationToken ct = default)
    {
        var processes = Process.GetProcessesByName("DCS");
        if (processes.Length == 0) return;

        var tcs = new TaskCompletionSource<bool>();
        using var reg = ct.Register(() => tcs.TrySetCanceled());

        var remaining = processes.Length;
        foreach (var p in processes)
        {
            p.EnableRaisingEvents = true;
            p.Exited += (_, _) =>
            {
                if (Interlocked.Decrement(ref remaining) == 0)
                    tcs.TrySetResult(true);
            };

            if (p.HasExited && Interlocked.Decrement(ref remaining) == 0)
                tcs.TrySetResult(true);
        }

        await tcs.Task;
        _logger.LogInformation("DCS process exited");
    }

    private void Poll(object? _)
    {
        var running = IsDcsRunning();
        if (running && !_wasRunning)
        {
            _logger.LogInformation("DCS started");
            DcsStarted?.Invoke(this, EventArgs.Empty);
        }
        else if (!running && _wasRunning)
        {
            _logger.LogInformation("DCS exited (detected via poll)");
            DcsExited?.Invoke(this, EventArgs.Empty);
        }
        _wasRunning = running;
    }

    public void Dispose()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
    }
}
