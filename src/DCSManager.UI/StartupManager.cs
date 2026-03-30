using System;
using System.Reflection;
using Microsoft.Win32;

namespace DCSManager.UI;

public static class StartupManager
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DCSManager";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) != null;
    }

    public static void Enable()
    {
        var exePath = Assembly.GetEntryAssembly()?.Location
            ?? Environment.ProcessPath ?? "";

        // Handle single-file publish (dll path → exe path)
        if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            exePath = exePath[..^4] + ".exe";

        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.SetValue(ValueName, $"\"{exePath}\" --minimized");
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
