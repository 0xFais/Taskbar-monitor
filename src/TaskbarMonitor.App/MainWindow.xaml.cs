using System.Windows;
using System.Windows.Media;
using System.Windows.Forms;
using TaskbarMonitor.Core.Settings;

namespace TaskbarMonitor.App;

public partial class MainWindow : Window
{
    private readonly MonitorViewModel _viewModel;
    private readonly NotifyIcon _trayIcon;
    private CompactPanelWindow? _compactPanel;
    private bool _exitRequested;
    private bool _sensorSetupBusy;
    public bool StartMinimized => _viewModel.StartMinimized;
    public bool AutoSetupCpuTemperature => _viewModel.AutoSetupCpuTemperature;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MonitorViewModel();
        DataContext = _viewModel;
        ApplyTheme(_viewModel.Theme);
        _viewModel.ThemeChanged += ApplyTheme;
        _viewModel.CompactPanelChanged += ToggleCompactPanel;
        _viewModel.CpuSensorSetupRequested += () => _ = InitializeSensorSupportAsync();
        _trayIcon = new NotifyIcon
        {
            Text = "Taskbar-monitor",
            Icon = CreateTrayIcon(),
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };
        _trayIcon.ContextMenuStrip.Items.Add("Open Taskbar-monitor", null, (_, _) => Dispatcher.Invoke(ShowDashboard));
        _trayIcon.ContextMenuStrip.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(RequestExit));
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowDashboard);
        if (_viewModel.CompactPanelEnabled) ToggleCompactPanel(true);
        Closing += (_, e) => { if (!_exitRequested) { e.Cancel = true; HideDashboard(); } };
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) HideDashboard(); };
        Closed += (_, _) => { _compactPanel?.Close(); _trayIcon.Visible = false; _trayIcon.Dispose(); _viewModel.Dispose(); };
    }

    private static System.Drawing.Icon CreateTrayIcon()
    {
        try
        {
            var executablePath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(executablePath) && System.Drawing.Icon.ExtractAssociatedIcon(executablePath) is { } icon)
                return icon;
        }
        catch (Exception)
        {
            // Keep the tray available if the executable icon cannot be loaded.
        }

        return System.Drawing.SystemIcons.Application;
    }

    private void Overview_Click(object sender, RoutedEventArgs e)
    {
        OverviewPage.Visibility = Visibility.Visible;
        AppearancePage.Visibility = Visibility.Collapsed;
        TaskbarPage.Visibility = Visibility.Collapsed;
        OverviewNav.SetResourceReference(BackgroundProperty, "NavSelectedBackground");
        AppearanceNav.ClearValue(BackgroundProperty);
        TaskbarNav.ClearValue(BackgroundProperty);
        ContentScroll.ScrollToTop();
        FadeIn(OverviewPage);
    }

    private void Appearance_Click(object sender, RoutedEventArgs e)
    {
        OverviewPage.Visibility = Visibility.Collapsed;
        AppearancePage.Visibility = Visibility.Visible;
        TaskbarPage.Visibility = Visibility.Collapsed;
        AppearanceNav.SetResourceReference(BackgroundProperty, "NavSelectedBackground");
        OverviewNav.ClearValue(BackgroundProperty);
        TaskbarNav.ClearValue(BackgroundProperty);
        ContentScroll.ScrollToTop();
        FadeIn(AppearancePage);
    }

    private void Taskbar_Click(object sender, RoutedEventArgs e) => ShowTaskbarCustomization();
    public void ShowTaskbarCustomization()
    {
        ShowDashboard();
        OverviewPage.Visibility = Visibility.Collapsed;
        AppearancePage.Visibility = Visibility.Collapsed;
        TaskbarPage.Visibility = Visibility.Visible;
        TaskbarNav.SetResourceReference(BackgroundProperty, "NavSelectedBackground");
        OverviewNav.ClearValue(BackgroundProperty);
        AppearanceNav.ClearValue(BackgroundProperty);
        ContentScroll.ScrollToTop();
        FadeIn(TaskbarPage);
    }

    public void ShowDashboard() { ShowInTaskbar = true; WindowState = WindowState.Normal; Show(); Activate(); }
    private void HideDashboard() { Hide(); ShowInTaskbar = false; }
    public void RequestExit() { _exitRequested = true; Close(); }

    public async Task InitializeSensorSupportAsync()
    {
        SensorTrace.Write($"InitializeSensorSupportAsync entered. enabled={_viewModel.AutoSetupCpuTemperature}, busy={_sensorSetupBusy}, exiting={_exitRequested}.");
        if (_sensorSetupBusy || !_viewModel.AutoSetupCpuTemperature || _exitRequested) return;
        await SetUpSensorsAsync();
    }

    private async Task SetUpSensorsAsync()
    {
        if (_sensorSetupBusy) return;
        _sensorSetupBusy = true;
        try
        {
            var driverInstalled = TaskbarMonitor.Windows.SensorSetup.CheckDriverInstalled();
            SensorTrace.Write($"Sensor setup starting. DriverInstalled={driverInstalled}.");
            _viewModel.ReportCpuTemperatureStatus(driverInstalled
                ? "PawnIO is already installed. Preparing CPU sensor access…"
                : "PawnIO is missing. Preparing its verified official installer. Windows will request administrator approval.");
            var result = await TaskbarMonitor.Core.Metrics.SensorStartupCoordinator.EnsureAsync(
                TaskbarMonitor.Windows.SensorSetup.CheckDriverInstalled, TaskbarMonitor.Windows.SensorSetup.InstallAsync);
            SensorTrace.Write($"Sensor setup detection result: {result.State}: {result.Message}");
            if (_exitRequested) return;
            _viewModel.RefreshCpuDriverStatus();
            _viewModel.ReportCpuTemperatureStatus(result.Message);
            if (result.State != TaskbarMonitor.Core.Metrics.SensorStartupState.Ready)
            {
                ShowDashboard(); Appearance_Click(this, new RoutedEventArgs());
            }
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        { SensorTrace.Write("Administrator approval was cancelled in the sensor setup flow."); _viewModel.ReportCpuTemperatureStatus("Administrator approval was cancelled. The app will keep monitoring without CPU temperature. Retry in Customize when ready."); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or System.IO.IOException or System.Net.Http.HttpRequestException or InvalidOperationException or TaskCanceledException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            SensorTrace.Write("Sensor setup failed: " + ex);
            _viewModel.ReportCpuTemperatureStatus("Automatic CPU sensor setup could not finish: " + ex.Message);
            if (!_exitRequested) { ShowDashboard(); Appearance_Click(this, new RoutedEventArgs()); }
        }
        finally { _sensorSetupBusy = false; }
    }
    private void Minimize_Click(object sender, RoutedEventArgs e) => HideDashboard();
    private void Close_Click(object sender, RoutedEventArgs e) => HideDashboard();
    private void Quit_Click(object sender, RoutedEventArgs e) => RequestExit();
    private async void InstallCpuSupport_Click(object sender, RoutedEventArgs e)
    {
        var button = (System.Windows.Controls.Button)sender;
        button.IsEnabled = false;
        try { await SetUpSensorsAsync(); }
        finally { button.IsEnabled = true; }
    }
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void ToggleTheme_Click(object sender, RoutedEventArgs e) => _viewModel.Theme = _viewModel.Theme == "Dark" ? "Light" : "Dark";
    private void HistoryMetric_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.RadioButton { Tag: string metric }) _viewModel.SelectedMetric = metric;
    }
    private static void FadeIn(UIElement element)
    {
        if (SystemParameters.ClientAreaAnimation)
            element.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
    }

    private void Theme_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (IsLoaded && DataContext is MonitorViewModel vm) vm.Theme = (string)((System.Windows.Controls.ComboBox)sender).SelectedItem;
    }

    private void Refresh_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (IsLoaded && DataContext is MonitorViewModel vm && ((System.Windows.Controls.ComboBox)sender).SelectedItem is int interval) vm.RefreshIntervalMs = interval;
    }

    private void ToggleCompactPanel(bool enabled)
    {
        if (enabled)
        {
            if (_compactPanel is not null) return;
            _compactPanel = new CompactPanelWindow(_viewModel);
            _compactPanel.Show();
        }
        else if (_compactPanel is { } panel)
        {
            panel.Close();
            _compactPanel = null;
            _viewModel.UpdateTaskbarStatus("Compact taskbar panel is turned off");
        }
    }

    private void ApplyTheme(string theme)
    {
        var light = theme == "Light";
        SetBrush("AppBackground", light ? "#F4F6FA" : "#11141B");
        SetBrush("SidebarBackground", light ? "#FFFFFF" : "#1A1F29");
        SetBrush("CardBackground", light ? "#FFFFFF" : "#1A1F29");
        SetBrush("CardRaised", light ? "#EAF0F8" : "#252C38");
        SetBrush("MainText", light ? "#20283B" : "#EDF1FA");
        SetBrush("MutedText", light ? "#647086" : "#A2ADC0");
        SetBrush("BorderBrushSoft", light ? "#DCE3EE" : "#303949");
        SetBrush("AccentBrush", light ? "#386BDC" : "#8AB1FF");
        SetBrush("NavSelectedBackground", light ? "#E9F0FF" : "#253552");
        SetBrush("GreenBrush", light ? "#17856B" : "#7BD8B7");
        SetBrush("OrangeBrush", light ? "#AB6428" : "#EDBA84");
    }

    private void SetBrush(string key, string color)
    {
        var parsed = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color);
        System.Windows.Application.Current.Resources[key] = new SolidColorBrush(parsed);
    }
}
