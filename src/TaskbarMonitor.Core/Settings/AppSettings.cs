namespace TaskbarMonitor.Core.Settings;

public sealed class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public int RefreshIntervalMs { get; set; } = 1000;
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; }
    public bool AutoSetupCpuTemperature { get; set; }
    public bool CpuSensorStartupPreferenceConfigured { get; set; }
    public bool ShowCompactPanel { get; set; } = true;
    public string CompactPanelPosition { get; set; } = "BottomRight";
    public TaskbarSettings Taskbar { get; set; } = new();
}
