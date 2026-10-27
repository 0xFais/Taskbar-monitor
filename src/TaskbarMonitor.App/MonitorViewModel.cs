using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using TaskbarMonitor.Core.Metrics;
using TaskbarMonitor.Core.Settings;
using TaskbarMonitor.Monitoring;
using TaskbarMonitor.Windows;

namespace TaskbarMonitor.App;

public sealed class MonitorViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ISettingsStore _settingsStore = new JsonSettingsStore();
    private readonly WindowsMetricProvider _systemProvider = new();
    private readonly LibreHardwareMetricProvider _hardwareProvider = new();
    private readonly Dispatcher _dispatcher = System.Windows.Application.Current.Dispatcher;
    private System.Threading.Timer? _timer;
    private int _sampleInProgress;
    private MetricSnapshot _snapshot = MetricSnapshot.Empty;
    private string _theme;
    private int _refreshIntervalMs;
    private bool _compactPanelEnabled;
    private string? _runtimeStatus;
    private readonly List<HistoryPoint> _cpuHistory = [];
    private readonly List<HistoryPoint> _gpuHistory = [];
    private readonly List<HistoryPoint> _ramHistory = [];
    private string _selectedMetric = "CPU";
    private string _taskbarStatus = "Finding space on the taskbar…";
    private int _disposed;
    private bool _cpuDriverInstalled;
    private string? _cpuSetupMessage;

    public MonitorViewModel()
    {
        Settings = _settingsStore.Load();
        // Automatic CPU setup used to be enabled by default. Treat settings from older
        // versions as not opted in, so signing in never unexpectedly triggers UAC.
        if (!Settings.CpuSensorStartupPreferenceConfigured)
        {
            Settings.AutoSetupCpuTemperature = false;
            Settings.CpuSensorStartupPreferenceConfigured = true;
            try { _settingsStore.Save(Settings); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        }
        Settings.Theme = Themes.Contains(Settings.Theme) ? Settings.Theme : "Dark";
        Settings.RefreshIntervalMs = RefreshOptions.Contains(Settings.RefreshIntervalMs) ? Settings.RefreshIntervalMs : 1000;
        Settings.Taskbar ??= new();
        Settings.StartWithWindows = WindowsStartupService.IsEnabled();
        Taskbar = new TaskbarCustomizationViewModel(Settings.Taskbar, Save);
        Taskbar.UpdateReadings(this);
        _cpuDriverInstalled = SensorSetup.DriverInstalled;
        _theme = Settings.Theme;
        _refreshIntervalMs = Settings.RefreshIntervalMs;
        _compactPanelEnabled = Settings.ShowCompactPanel;
        _timer = new System.Threading.Timer(_ => Sample(), null, 0, RefreshIntervalMs);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<string>? ThemeChanged;
    public event Action<bool>? CompactPanelChanged;
    public event Action? CpuSensorSetupRequested;
    public AppSettings Settings { get; }
    public TaskbarCustomizationViewModel Taskbar { get; }
    public string[] Themes { get; } = ["Dark", "Light"];
    public int[] RefreshOptions { get; } = [500, 1000, 2000, 5000];
    public string StatusText => _runtimeStatus ?? _snapshot.SensorStatus ?? "Monitoring active";
    public double CpuUsage => _snapshot.CpuUsage ?? 0;
    public double GpuUsage => _snapshot.GpuUsage ?? 0;
    public double RamUsage => _snapshot.RamTotalBytes is > 0 && _snapshot.RamUsedBytes is { } used ? Math.Clamp(100 * used / _snapshot.RamTotalBytes.Value, 0, 100) : 0;
    public string CpuUsageText => Number(_snapshot.CpuUsage);
    public string GpuUsageText => Number(_snapshot.GpuUsage);
    public string CpuTemperatureText => Temperature(_snapshot.CpuTemperature);
    public string CpuTemperatureStatus => _cpuSetupMessage ?? _snapshot.CpuTemperatureStatus ?? "Checking CPU temperature sensor access…";
    public Visibility InstallCpuDriverVisibility => _cpuDriverInstalled ? Visibility.Collapsed : Visibility.Visible;
    public void RefreshCpuDriverStatus() { _cpuDriverInstalled = SensorSetup.DriverInstalled; OnPropertyChanged(nameof(InstallCpuDriverVisibility)); }
    public void ReportCpuTemperatureStatus(string message)
    {
        _cpuSetupMessage = message;
        OnPropertyChanged(nameof(CpuTemperatureStatus));
    }
    public string GpuTemperatureText => Temperature(_snapshot.GpuTemperature);
    public string RamText => _snapshot.RamUsedBytes is { } used && _snapshot.RamTotalBytes is { } total ? $"{Bytes(used)} / {Bytes(total)}" : "— / —";
    public string RamPercentText => _snapshot.RamTotalBytes is > 0 ? $"{RamUsage:0}% used" : "Memory unavailable";
    public string DownloadText => Rate(_snapshot.DownloadBytesPerSecond);
    public string UploadText => Rate(_snapshot.UploadBytesPerSecond);
    public string RamUsedText => _snapshot.RamUsedBytes is { } used ? (used / 1073741824d).ToString("0.0") : "—";
    public string RamDetailText => _snapshot.RamTotalBytes is { } total ? $"of {total / 1073741824d:0} GB · {RamUsage:0}% used" : "Memory unavailable";
    public string CompactRamText => _snapshot.RamTotalBytes is > 0 ? $"RAM {RamUsage:0}%" : "RAM —";
    public string CompactDownloadText => "↓ " + Rate(_snapshot.DownloadBytesPerSecond).Replace("KB/s", "K/s").Replace("MB/s", "M/s").Replace("GB/s", "G/s");
    public string CompactUploadText => "↑ " + Rate(_snapshot.UploadBytesPerSecond).Replace("KB/s", "K/s").Replace("MB/s", "M/s").Replace("GB/s", "G/s");
    public string TaskbarStatus => _taskbarStatus;
    public string AppEditionText => AppDataPaths.IsPortable ? "Taskbar-monitor  ·  Fluent Desk  ·  Portable" : "Taskbar-monitor  ·  Fluent Desk";
    public string StorageModeText => AppDataPaths.IsPortable ? "Portable mode: your settings travel with this app folder." : "Your preferences are saved on this PC.";
    public bool AutoSetupCpuTemperature
    {
        get => Settings.AutoSetupCpuTemperature;
        set
        {
            if (Settings.AutoSetupCpuTemperature == value) return;
            Settings.AutoSetupCpuTemperature = value;
            Save();
            OnPropertyChanged();
            if (value) CpuSensorSetupRequested?.Invoke();
        }
    }
    public bool StartMinimized
    {
        get => Settings.StartMinimized;
        set { if (Settings.StartMinimized == value) return; Settings.StartMinimized = value; Save(); OnPropertyChanged(); }
    }
    public bool StartWithWindows
    {
        get => Settings.StartWithWindows;
        set
        {
            if (Settings.StartWithWindows == value) return;
            try
            {
                WindowsStartupService.SetEnabled(value);
                Settings.StartWithWindows = value;
                Save();
                OnPropertyChanged();
                SetStartupStatus(value
                    ? "Taskbar-monitor will start when you sign in to Windows."
                    : "Taskbar-monitor will no longer start automatically.");
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException or InvalidOperationException)
            {
                OnPropertyChanged();
                SetStartupStatus($"Could not update Windows startup: {ex.Message}");
            }
        }
    }
    public string StartupStatus => _startupStatus ?? "Starts Taskbar-monitor when you sign in to Windows.";
    private string? _startupStatus;
    private void SetStartupStatus(string message)
    {
        _startupStatus = message;
        OnPropertyChanged(nameof(StartupStatus));
    }
    public string SelectedMetric
    {
        get => _selectedMetric;
        set { if (Set(ref _selectedMetric, value)) { OnPropertyChanged(nameof(SelectedHistory)); OnPropertyChanged(nameof(HistoryTitle)); OnPropertyChanged(nameof(HistoryValue)); OnPropertyChanged(nameof(IsCpuSelected)); OnPropertyChanged(nameof(IsGpuSelected)); OnPropertyChanged(nameof(IsRamSelected)); } }
    }
    public bool IsCpuSelected => SelectedMetric == "CPU";
    public bool IsGpuSelected => SelectedMetric == "GPU";
    public bool IsRamSelected => SelectedMetric == "RAM";
    public string HistoryTitle => SelectedMetric + " activity";
    public string HistoryValue => SelectedMetric switch { "GPU" => GpuUsageText + "% usage", "RAM" => RamPercentText, _ => CpuUsageText + "% usage" };
    public IReadOnlyList<HistoryPoint> SelectedHistory => (SelectedMetric switch { "GPU" => _gpuHistory, "RAM" => _ramHistory, _ => _cpuHistory }).ToArray();
    public string CompactLine => $"CPU {CpuUsageText}%  {CpuTemperatureText}     GPU {GpuUsageText}%  {GpuTemperatureText}     RAM {RamUsage:0}%     ↓ {DownloadText}  ↑ {UploadText}";

    public string Theme
    {
        get => _theme;
        set { if (Set(ref _theme, value)) { Settings.Theme = value; Save(); ThemeChanged?.Invoke(value); } }
    }
    public int RefreshIntervalMs
    {
        get => _refreshIntervalMs;
        set { if (Set(ref _refreshIntervalMs, value)) { Settings.RefreshIntervalMs = value; _timer?.Change(0, value); Save(); } }
    }
    public bool CompactPanelEnabled
    {
        get => _compactPanelEnabled;
        set { if (Set(ref _compactPanelEnabled, value)) { Settings.ShowCompactPanel = value; Save(); CompactPanelChanged?.Invoke(value); } }
    }

    private void Sample()
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        if (Interlocked.Exchange(ref _sampleInProgress, 1) != 0) return;
        try
        {
            var system = _systemProvider.Sample();
            MetricSnapshot hardware;
            try { hardware = _hardwareProvider.Sample(); }
            catch (Exception ex) { hardware = new(null, null, null, null, null, null, null, null, DateTimeOffset.UtcNow, ex.Message); }
            var missingSensors = hardware.CpuUsage is null && hardware.CpuTemperature is null && hardware.GpuUsage is null && hardware.GpuTemperature is null;
            var next = system with
            {
                CpuTemperature = hardware.CpuTemperature,
                CpuTemperatureStatus = hardware.CpuTemperatureStatus,
                GpuUsage = hardware.GpuUsage,
                GpuTemperature = hardware.GpuTemperature,
                SensorStatus = hardware.SensorStatus ?? (missingSensors ? "GPU and temperature sensors unavailable" : null)
            };
            if (!_dispatcher.HasShutdownStarted && Volatile.Read(ref _disposed) == 0)
                _dispatcher.BeginInvoke(() => UpdateSnapshot(next));
        }
        catch
        {
            if (!_dispatcher.HasShutdownStarted && Volatile.Read(ref _disposed) == 0)
                _dispatcher.BeginInvoke(() => StatusTextChanged("Some readings are temporarily unavailable"));
        }
        finally { Interlocked.Exchange(ref _sampleInProgress, 0); }
    }

    private void UpdateSnapshot(MetricSnapshot value)
    {
        _snapshot = value;
        _runtimeStatus = null;
        Taskbar.UpdateReadings(this);
        Append(_cpuHistory, value.CapturedAt, value.CpuUsage);
        Append(_gpuHistory, value.CapturedAt, value.GpuUsage);
        Append(_ramHistory, value.CapturedAt, value.RamTotalBytes is > 0 ? RamUsage : null);
        OnPropertyChanged(string.Empty);
    }
    private static void Append(List<HistoryPoint> history, DateTimeOffset time, double? value)
    {
        history.Add(new HistoryPoint(time, value));
        history.RemoveAll(point => point.Time < time.AddSeconds(-60));
        if (history.Count > 600) history.RemoveRange(0, history.Count - 600);
    }
    public void UpdateTaskbarStatus(string value) { _taskbarStatus = value; OnPropertyChanged(nameof(TaskbarStatus)); }
    private void StatusTextChanged(string value) { _runtimeStatus = value; OnPropertyChanged(nameof(StatusText)); }
    private void Save() { try { _settingsStore.Save(Settings); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    private static string Number(double? value) => value is { } number ? number.ToString("0", CultureInfo.InvariantCulture) : "—";
    private static string Temperature(double? value) => value is { } number ? $"{number:0} °C" : "— °C";
    private static string Bytes(double value) => $"{value / 1024d / 1024d / 1024d:0.0} GB";
    private static string Rate(double? value)
    {
        if (value is not { } rate) return "—";
        var units = new[] { "B/s", "KB/s", "MB/s", "GB/s" };
        var index = 0;
        while (rate >= 1024 && index < units.Length - 1) { rate /= 1024; index++; }
        return $"{rate:0.0} {units[index]}";
    }
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        OnPropertyChanged(nameof(CompactLine));
        return true;
    }
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public void Dispose() { Interlocked.Exchange(ref _disposed, 1); _timer?.Dispose(); Taskbar.Dispose(); _systemProvider.Dispose(); _hardwareProvider.Dispose(); }
}
