using System.Security.Cryptography;
using Microsoft.Win32;

namespace TaskbarMonitor.Windows;

public static class SensorSetup
{
    public const string InstallerSha256 = "1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032";
    public static bool DriverInstalled
    {
        get
        {
            try
            {
                return CheckDriverInstalled();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException) { return false; }
        }
    }
    public static bool CheckDriverInstalled()
    {
        // Automatic setup must not mistake a denied registry read for a missing driver.
        using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO");
        return Version.TryParse(key?.GetValue("DisplayVersion") as string, out _);
    }
    public static bool VerifyInstaller(string path)
    {
        if (!System.IO.File.Exists(path)) return false;
        using var file = System.IO.File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(file)).Equals(InstallerSha256, StringComparison.OrdinalIgnoreCase);
    }
    public static async Task<string> PrepareInstallerAsync()
    {
        var folder = System.IO.Path.Combine(AppDataPaths.DataDirectory, "Setup");
        System.IO.Directory.CreateDirectory(folder);
        var path = System.IO.Path.Combine(folder, "PawnIO_setup-2.2.0.exe");
        if (VerifyInstaller(path)) return path;
        using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        using var source = await client.GetStreamAsync("https://github.com/namazso/PawnIO.Setup/releases/download/2.2.0/PawnIO_setup.exe");
        var temporary = path + ".download";
        await using (var target = System.IO.File.Create(temporary)) await source.CopyToAsync(target);
        if (!VerifyInstaller(temporary)) throw new InvalidOperationException("The sensor installer does not match the verified official release. Installation was stopped.");
        System.IO.File.Move(temporary, path, true);
        return path;
    }

    public static async Task<int> InstallAsync()
    {
        var installer = await PrepareInstallerAsync();
        // The official installer uses -install for its noninteractive installation path.
        // Windows UAC remains in control of administrator approval.
        using var process = await Task.Run(() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(installer)
        {
            UseShellExecute = true, Verb = "runas", Arguments = "-install", WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
        }));
        if (process is null) throw new InvalidOperationException("The sensor installer did not start.");
        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}
