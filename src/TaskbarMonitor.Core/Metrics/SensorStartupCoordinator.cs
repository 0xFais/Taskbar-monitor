namespace TaskbarMonitor.Core.Metrics;

public enum SensorStartupState { Ready, RestartRequired, NotInstalled }
public sealed record SensorStartupResult(SensorStartupState State, string Message);

public static class SensorStartupCoordinator
{
    /// <summary>Installation is requested only when a fresh detection says the driver is missing.</summary>
    public static async Task<SensorStartupResult> EnsureAsync(Func<bool> isInstalled, Func<Task<int>> install)
    {
        if (isInstalled()) return new(SensorStartupState.Ready, "PawnIO is already installed.");
        var exitCode = await install();
        if (exitCode == 3010) return new(SensorStartupState.RestartRequired, "Windows needs a restart to finish installing CPU sensor support.");
        if (exitCode == 0 && isInstalled()) return new(SensorStartupState.Ready, "PawnIO sensor support is installed.");
        return new(SensorStartupState.NotInstalled, $"CPU sensor support could not be installed (exit code {exitCode}). You can retry in Customize.");
    }
}
