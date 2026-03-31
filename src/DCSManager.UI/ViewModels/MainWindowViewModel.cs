using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DCSManager.Core.Interfaces;
using DCSManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace DCSManager.UI.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ICatalogService _catalog;
    private readonly IUpdateOrchestrator _orchestrator;
    private readonly IDcsInstallDetector _dcsDetector;
    private readonly IDcsProcessMonitor _dcsMonitor;
    private readonly IPluginStateStore _stateStore;
    private readonly IDcsVersionChecker _dcsVersionChecker;
    private readonly ILogger<MainWindowViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<PluginViewModel> _plugins = new();

    [ObservableProperty]
    private ObservableCollection<UpdateHistoryEntry> _history = new();

    [ObservableProperty]
    private ObservableCollection<DcsInstall> _dcsInstalls = new();

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private bool _isDcsRunning;

    [ObservableProperty]
    private bool _isChecking;

    [ObservableProperty]
    private string? _lastCheckedText;

    [ObservableProperty]
    private int _pendingUpdateCount;

    [ObservableProperty]
    private string _selectedTab = "Plugins";

    [ObservableProperty]
    private int _updateIntervalMinutes = 20;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private DcsInstall? _selectedDcsInstall;

    [ObservableProperty]
    private ObservableCollection<DcsVersionStatus> _dcsVersionStatuses = new();

    public ObservableCollection<string> Categories { get; } = new() { "All", "Communication", "Mission Planning", "Aircraft Mod", "Analysis", "Assets", "Hardware" };

    public ObservableCollection<PluginViewModel> FilteredPlugins => new(
        Plugins.Where(p =>
            (SelectedCategory == "All" || p.Definition.Category == SelectedCategory) &&
            (string.IsNullOrEmpty(SearchText) ||
             p.Definition.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
             p.Definition.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase))));

    public MainWindowViewModel(
        ICatalogService catalog,
        IUpdateOrchestrator orchestrator,
        IDcsInstallDetector dcsDetector,
        IDcsProcessMonitor dcsMonitor,
        IPluginStateStore stateStore,
        IDcsVersionChecker dcsVersionChecker,
        ILogger<MainWindowViewModel> logger)
    {
        _catalog = catalog;
        _orchestrator = orchestrator;
        _dcsDetector = dcsDetector;
        _dcsMonitor = dcsMonitor;
        _stateStore = stateStore;
        _dcsVersionChecker = dcsVersionChecker;
        _logger = logger;

        orchestrator.StateChanged += OnOrchestratorStateChanged;
        orchestrator.UpdateApplied += OnUpdateApplied;
        dcsMonitor.DcsStarted += (_, _) => Application.Current.Dispatcher.Invoke(() => IsDcsRunning = true);
        dcsMonitor.DcsExited += (_, _) => Application.Current.Dispatcher.Invoke(() => IsDcsRunning = false);
    }

    public async Task InitializeAsync()
    {
        IsDcsRunning = _dcsMonitor.IsDcsRunning();

        var installs = _dcsDetector.DetectInstalls();
        foreach (var install in installs)
            DcsInstalls.Add(install);

        await LoadPluginsAsync();
        LoadHistory();
        LoadSettings();
        _ = RefreshDcsVersionsAsync();
    }

    private async Task LoadPluginsAsync()
    {
        var defs = await _catalog.GetCatalogAsync();
        Plugins.Clear();

        foreach (var def in defs)
        {
            var vm = new PluginViewModel(def, OnInstallPlugin, OnOpenUrl);
            var state = _stateStore.GetState(def.Id);
            if (state != null)
            {
                vm.InstalledVersion = state.InstalledVersion;
                vm.AutoUpdate = state.AutoUpdate;
            }

            // Sync pending update status
            var pending = _orchestrator.PendingUpdates.FirstOrDefault(u => u.PluginId == def.Id);
            if (pending != null)
            {
                vm.LatestVersion = pending.LatestVersion;
                vm.Status = pending.Status;
            }
            else if (def.Source.Type != "github_release")
            {
                vm.Status = UpdateCheckStatus.ManualOnly;
            }
            else
            {
                vm.Status = state?.InstalledVersion != null ? UpdateCheckStatus.UpToDate : UpdateCheckStatus.NotInstalled;
            }

            Plugins.Add(vm);
        }

        PendingUpdateCount = _orchestrator.PendingUpdates.Count;
        OnPropertyChanged(nameof(FilteredPlugins));
    }

    private void LoadHistory()
    {
        History.Clear();
        foreach (var entry in _stateStore.GetHistory())
            History.Add(entry);
    }

    private void LoadSettings()
    {
        StartWithWindows = StartupManager.IsEnabled();
        var appSettings = _stateStore.GetAppSettings();
        UpdateIntervalMinutes = appSettings.CheckIntervalMinutes;

        // Pre-select the stored primary install, or default to first detected
        var storedPath = appSettings.PrimarySavedGamesPath;
        SelectedDcsInstall = DcsInstalls.FirstOrDefault(i =>
            string.Equals(i.SavedGamesPath, storedPath, StringComparison.OrdinalIgnoreCase))
            ?? DcsInstalls.FirstOrDefault();
    }

    [RelayCommand]
    private void SelectDcsInstall(DcsInstall? install)
    {
        SelectedDcsInstall = install;
        var settings = _stateStore.GetAppSettings();
        settings.PrimarySavedGamesPath = install?.SavedGamesPath;
        _stateStore.SaveAppSettings(settings);
    }

    [RelayCommand]
    private async Task CheckNow()
    {
        await _orchestrator.CheckNowAsync();
        await LoadPluginsAsync();
        await RefreshDcsVersionsAsync();
    }

    private async Task RefreshDcsVersionsAsync()
    {
        try
        {
            var statuses = await _dcsVersionChecker.CheckAllAsync(DcsInstalls);
            Application.Current.Dispatcher.Invoke(() =>
            {
                DcsVersionStatuses.Clear();
                foreach (var s in statuses)
                    DcsVersionStatuses.Add(s);
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DCS version check failed");
        }
    }

    [RelayCommand]
    private async Task ApplyAllUpdates()
    {
        var progress = new Progress<InstallProgress>(p =>
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Progress updates handled per-plugin
            }));
        await _orchestrator.ApplyAllPendingAsync(progress);
        await LoadPluginsAsync();
        LoadHistory();
    }

    [RelayCommand]
    private void ToggleStartWithWindows()
    {
        if (StartWithWindows)
            StartupManager.Enable();
        else
            StartupManager.Disable();
    }

    private async void OnInstallPlugin(PluginViewModel vm)
    {
        vm.IsInstalling = true;
        var update = _orchestrator.PendingUpdates.FirstOrDefault(u => u.PluginId == vm.Definition.Id)
            ?? new UpdateCheckResult
            {
                PluginId = vm.Definition.Id,
                LatestVersion = vm.LatestVersion,
                AssetDownloadUrl = null,
                Status = UpdateCheckStatus.NotInstalled
            };

        var progress = new Progress<InstallProgress>(p =>
            Application.Current.Dispatcher.Invoke(() =>
            {
                vm.InstallProgress = p.PercentComplete;
                vm.InstallStage = p.StatusMessage ?? p.Stage;
            }));

        await _orchestrator.ApplyUpdateAsync(vm.Definition.Id, progress);
        vm.IsInstalling = false;

        var state = _stateStore.GetState(vm.Definition.Id);
        vm.InstalledVersion = state?.InstalledVersion;
        vm.Status = state?.InstalledVersion != null ? UpdateCheckStatus.UpToDate : UpdateCheckStatus.NotInstalled;
        LoadHistory();
    }

    private static void OnOpenUrl(PluginViewModel vm)
    {
        var url = vm.Definition.ManualDownloadUrl ?? vm.Definition.HomepageUrl;
        if (url != null)
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void OnOrchestratorStateChanged(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsChecking = _orchestrator.IsChecking;
            PendingUpdateCount = _orchestrator.PendingUpdates.Count;
            LastCheckedText = _orchestrator.LastCheckedAt.HasValue
                ? $"Last checked {_orchestrator.LastCheckedAt.Value.ToLocalTime():HH:mm}"
                : null;
        });
    }

    private void OnUpdateApplied(object? sender, UpdateHistoryEntry entry)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            History.Insert(0, entry);
            PendingUpdateCount = _orchestrator.PendingUpdates.Count;
        });
    }

    partial void OnSearchTextChanged(string value) => OnPropertyChanged(nameof(FilteredPlugins));
    partial void OnSelectedCategoryChanged(string value) => OnPropertyChanged(nameof(FilteredPlugins));
}
