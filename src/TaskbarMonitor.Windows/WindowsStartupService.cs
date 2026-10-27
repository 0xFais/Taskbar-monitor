using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using Microsoft.Win32;

namespace TaskbarMonitor.Windows;

public static class WindowsStartupService
{
    private const string TaskName = "\\Taskbar-monitor";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string LegacyValueName = "TaskbarMonitor";

    public static bool IsEnabled()
    {
        using var process = StartSchtasks("/Query", "/TN", TaskName);
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        _ = output.GetAwaiter().GetResult();
        _ = error.GetAwaiter().GetResult();
        if (process.ExitCode == 0) return true;

        // Upgrade an existing Run entry to an elevated scheduled task automatically.
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        if (runKey?.GetValue(LegacyValueName) is not string command || string.IsNullOrWhiteSpace(command)) return false;
        SetEnabled(true);
        return true;
    }

    public static void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
                throw new InvalidOperationException("Windows could not find the app executable path.");

            var currentUser = WindowsIdentity.GetCurrent().Name;
            using var process = StartSchtasks(
                "/Create", "/TN", TaskName,
                "/SC", "ONLOGON",
                "/TR", $"\"{executablePath}\"",
                "/RU", currentUser,
                "/IT",
                "/RL", "HIGHEST",
                "/F");
            EnsureSuccess(process, "create the elevated sign-in task");
            RemoveLegacyRunEntry();
            return;
        }

        using (var process = StartSchtasks("/Delete", "/TN", TaskName, "/F"))
        {
            // Removing the setting is idempotent, including if an old version used Run registry.
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            _ = output.GetAwaiter().GetResult();
            _ = error.GetAwaiter().GetResult();
        }
        RemoveLegacyRunEntry();
    }

    private static Process StartSchtasks(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("schtasks.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        return Process.Start(startInfo) ?? throw new InvalidOperationException("Windows could not start Task Scheduler.");
    }

    private static void EnsureSuccess(Process process, string operation)
    {
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        if (process.ExitCode == 0) return;
        var details = error.GetAwaiter().GetResult();
        if (string.IsNullOrWhiteSpace(details)) details = output.GetAwaiter().GetResult();
        throw new IOException($"Windows could not {operation}: {details.Trim()}");
    }

    private static void RemoveLegacyRunEntry()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        runKey?.DeleteValue(LegacyValueName, throwOnMissingValue: false);
    }
}
