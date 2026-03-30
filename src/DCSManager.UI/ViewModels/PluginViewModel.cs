using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DCSManager.Core.Models;

namespace DCSManager.UI.ViewModels;

public partial class PluginViewModel : ObservableObject
{
    private readonly Action<PluginViewModel> _installAction;
    private readonly Action<PluginViewModel> _openUrlAction;

    [ObservableProperty]
    private PluginDefinition _definition;

    [ObservableProperty]
    private string? _installedVersion;

    [ObservableProperty]
    private string? _latestVersion;

    [ObservableProperty]
    private UpdateCheckStatus _status = UpdateCheckStatus.UpToDate;

    [ObservableProperty]
    private bool _autoUpdate = true;

    [ObservableProperty]
    private double _installProgress;

    [ObservableProperty]
    private string? _installStage;

    [ObservableProperty]
    private bool _isInstalling;

    public bool IsManualOnly => Definition.Source.Type != "github_release";
    public bool HasUpdate => Status == UpdateCheckStatus.UpdateAvailable;
    public bool IsDeprecated => !string.IsNullOrEmpty(Definition.DeprecationNotice);

    public string StatusLabel => Status switch
    {
        UpdateCheckStatus.NotInstalled => "Not Installed",
        UpdateCheckStatus.UpdateAvailable => $"Update: {LatestVersion}",
        UpdateCheckStatus.UpToDate => $"Up to date ({InstalledVersion})",
        UpdateCheckStatus.ManualOnly => "Manual Download",
        UpdateCheckStatus.Error => "Check Failed",
        _ => ""
    };

    public string ActionLabel => IsManualOnly ? "Open Download Page"
        : Status == UpdateCheckStatus.NotInstalled ? "Install"
        : Status == UpdateCheckStatus.UpdateAvailable ? $"Update to {LatestVersion}"
        : "Reinstall";

    public PluginViewModel(PluginDefinition definition, Action<PluginViewModel> installAction, Action<PluginViewModel> openUrlAction)
    {
        _definition = definition;
        _installAction = installAction;
        _openUrlAction = openUrlAction;
    }

    [RelayCommand]
    private void Install()
    {
        if (IsManualOnly)
            _openUrlAction(this);
        else
            _installAction(this);
    }

    partial void OnStatusChanged(UpdateCheckStatus value)
    {
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(ActionLabel));
        OnPropertyChanged(nameof(HasUpdate));
    }

    partial void OnInstalledVersionChanged(string? value) => OnPropertyChanged(nameof(StatusLabel));
    partial void OnLatestVersionChanged(string? value)
    {
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(ActionLabel));
    }
}
