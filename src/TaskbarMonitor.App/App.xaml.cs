namespace TaskbarMonitor.App;
public partial class App : System.Windows.Application
{
    private Mutex? _instanceMutex;
    private EventWaitHandle? _openDashboard;
    private RegisteredWaitHandle? _openWait;
    private bool _ownsInstance;

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        SensorTrace.Write($"Application startup. Args=[{string.Join(" ", e.Args)}], portable={TaskbarMonitor.Windows.AppDataPaths.IsPortable}.");
        _instanceMutex = new Mutex(true, "Local\\TaskbarMonitor.FluentDesk", out _ownsInstance);
        if (!_ownsInstance)
        {
            using var existing = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\TaskbarMonitor.OpenDashboard");
            existing.Set();
            Shutdown();
            return;
        }
        _openDashboard = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\TaskbarMonitor.OpenDashboard");
        _openWait = ThreadPool.RegisterWaitForSingleObject(_openDashboard, (_, _) =>
        {
            if (!Dispatcher.HasShutdownStarted)
                Dispatcher.BeginInvoke(() => { if (MainWindow is MainWindow dashboard) dashboard.ShowDashboard(); });
        }, null, Timeout.Infinite, false);
        var dashboard = new MainWindow();
        MainWindow = dashboard;
        SensorTrace.Write($"Dashboard created. StartMinimized={dashboard.StartMinimized}, automaticSensorSetup={dashboard.AutoSetupCpuTemperature}.");
        if (!dashboard.StartMinimized && !e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase))
            dashboard.ShowDashboard();
        if (!e.Args.Contains("--skip-sensor-setup", StringComparer.OrdinalIgnoreCase))
            await dashboard.InitializeSensorSupportAsync();
        else SensorTrace.Write("Sensor setup skipped by command line.");
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _openWait?.Unregister(null);
        _openDashboard?.Dispose();
        if (_ownsInstance) _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
