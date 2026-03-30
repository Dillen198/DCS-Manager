using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using DCSManager.UI.ViewModels;

namespace DCSManager.UI.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    public async void Initialize()
    {
        await _viewModel.InitializeAsync();
        NavigateTo("Plugins");
    }

    private void NavigateTo(string tab)
    {
        TabPlugins.Visibility = tab == "Plugins" ? Visibility.Visible : Visibility.Collapsed;
        TabUpdates.Visibility = tab == "Updates" ? Visibility.Visible : Visibility.Collapsed;
        TabHistory.Visibility = tab == "History" ? Visibility.Visible : Visibility.Collapsed;
        TabSettings.Visibility = tab == "Settings" ? Visibility.Visible : Visibility.Collapsed;

        // Update nav button styles (simple tag-based approach)
        foreach (var btn in new[] { BtnPlugins, BtnUpdates, BtnHistory, BtnSettings })
        {
            btn.Style = (string)btn.Tag == tab
                ? (Style)FindResource("NavButtonActive")
                : (Style)FindResource("NavButton");
        }
    }

    private void NavPlugins_Click(object sender, RoutedEventArgs e) => NavigateTo("Plugins");
    private void NavUpdates_Click(object sender, RoutedEventArgs e) => NavigateTo("Updates");
    private void NavHistory_Click(object sender, RoutedEventArgs e) => NavigateTo("History");
    private void NavSettings_Click(object sender, RoutedEventArgs e) => NavigateTo("Settings");

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide(); // Minimize to tray instead of closing
    }

    private void OpenLogFile_Click(object sender, RoutedEventArgs e)
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DCSManager", "logs");
        if (Directory.Exists(logDir))
            System.Diagnostics.Process.Start("explorer.exe", logDir);
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DCSManager");
        if (Directory.Exists(dir))
            System.Diagnostics.Process.Start("explorer.exe", dir);
    }
}
