using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DCSManager.Core.Interfaces;
using DCSManager.Services;
using DCSManager.UI.ViewModels;
using DCSManager.UI.Views;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace DCSManager.App;

public partial class App : Application
{
    private IHost _host = null!;
    private TaskbarIcon? _trayIcon;
    private MainWindow? _mainWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appDataDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DCSManager");
        System.IO.Directory.CreateDirectory(appDataDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                System.IO.Path.Combine(appDataDir, "logs", "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(ConfigureServices)
            .Build();

        await _host.StartAsync();
        SetupTrayIcon();

        var minimized = e.Args.Contains("--minimized");
        if (!minimized)
            ShowMainWindow();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient("github", c =>
        {
            c.BaseAddress = new Uri("https://api.github.com/");
            c.DefaultRequestHeaders.Add("User-Agent", "DCSManager/1.0");
            c.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            c.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            c.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHttpClient("download", c =>
        {
            c.Timeout = TimeSpan.FromMinutes(30);
        });

        services.AddSingleton<ICatalogService, CatalogService>();
        services.AddSingleton<IPluginStateStore, JsonPluginStateStore>();
        services.AddSingleton<IDcsInstallDetector, DcsInstallDetector>();
        services.AddSingleton<IDcsProcessMonitor, DcsProcessMonitor>();
        services.AddSingleton<IGitHubReleaseService, GitHubReleaseService>();
        services.AddSingleton<IVersionDetectionService, VersionDetectionService>();
        services.AddSingleton<IPluginInstallerService, PluginInstallerService>();
        services.AddSingleton<UpdateOrchestrator>();
        services.AddSingleton<IUpdateOrchestrator>(sp => sp.GetRequiredService<UpdateOrchestrator>());
        services.AddHostedService(sp => sp.GetRequiredService<UpdateOrchestrator>());

        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<MainWindow>();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = (TaskbarIcon?)TryFindResource("TrayIcon");
        if (_trayIcon == null) return;

        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowMainWindow();

        // Wire context menu via Tag
        if (_trayIcon.ContextMenu != null)
        {
            foreach (System.Windows.Controls.MenuItem item in _trayIcon.ContextMenu.Items.OfType<System.Windows.Controls.MenuItem>())
            {
                item.Click += (s, _) =>
                {
                    if (s is System.Windows.Controls.MenuItem mi)
                    {
                        switch (mi.Tag as string)
                        {
                            case "open": ShowMainWindow(); break;
                            case "check": Task.Run(() => _host.Services.GetRequiredService<IUpdateOrchestrator>().CheckNowAsync()); break;
                            case "exit": TrayExit_Click(s, new RoutedEventArgs()); break;
                        }
                    }
                };
            }
        }

        var orchestrator = _host.Services.GetRequiredService<IUpdateOrchestrator>();
        orchestrator.StateChanged += (_, _) => Dispatcher.Invoke(UpdateTrayTooltip);
        orchestrator.UpdateAvailable += (_, result) =>
            Dispatcher.Invoke(() =>
            {
                _trayIcon.ShowNotification("DCS Manager",
                    $"Update available: {result.PluginId} \u2192 {result.LatestVersion}",
                    NotificationIcon.Info);
                UpdateTrayTooltip();
            });
        orchestrator.UpdateApplied += (_, entry) =>
            Dispatcher.Invoke(() =>
            {
                if (entry.Success)
                    _trayIcon.ShowNotification("DCS Manager",
                        $"{entry.PluginId} updated to {entry.ToVersion}",
                        NotificationIcon.Info);
                UpdateTrayTooltip();
            });
    }

    private void UpdateTrayTooltip()
    {
        if (_trayIcon == null) return;
        var orchestrator = _host.Services.GetRequiredService<IUpdateOrchestrator>();
        _trayIcon.ToolTipText = orchestrator.PendingUpdates.Count > 0
            ? $"DCS Manager — {orchestrator.PendingUpdates.Count} update(s) available"
            : "DCS Manager — All plugins up to date";
    }

    public void ShowMainWindow()
    {
        if (_mainWindow == null || !_mainWindow.IsLoaded)
        {
            _mainWindow = _host.Services.GetRequiredService<MainWindow>();
            _mainWindow.Initialize();
        }
        _mainWindow.Show();
        _mainWindow.Activate();
        _mainWindow.WindowState = WindowState.Normal;
    }

    private async void TrayExit_Click(object sender, RoutedEventArgs e)
    {
        _trayIcon?.Dispose();
        await _host.StopAsync(TimeSpan.FromSeconds(5));
        _host.Dispose();
        Shutdown();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        try { await _host.StopAsync(TimeSpan.FromSeconds(3)); } catch { /* ignore */ }
        _host.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
