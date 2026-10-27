using System.IO;

namespace TaskbarMonitor.Windows;

/// <summary>Portable builds keep their preferences and setup cache beside the executable.</summary>
public static class AppDataPaths
{
    public static bool IsPortable => IsPortableDirectory(AppContext.BaseDirectory);
    public static string LegacyDataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskbarMonitor");
    public static string DataDirectory => ResolveDataDirectory(AppContext.BaseDirectory, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");
    public static bool IsPortableDirectory(string executableDirectory) => File.Exists(Path.Combine(executableDirectory, "portable.flag"));
    public static string ResolveDataDirectory(string executableDirectory, string localAppData) => IsPortableDirectory(executableDirectory)
        ? Path.Combine(executableDirectory, "Data") : Path.Combine(localAppData, "TaskbarMonitor");
}
