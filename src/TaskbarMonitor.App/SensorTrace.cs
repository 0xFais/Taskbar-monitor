using System.IO;
using TaskbarMonitor.Windows;

namespace TaskbarMonitor.App;

internal static class SensorTrace
{
    private static readonly object Gate = new();
    private static string LogPath => Path.Combine(AppDataPaths.DataDirectory, "sensor-diagnostics.log");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
                File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
