using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using TaskbarMonitor.Windows;

namespace TaskbarMonitor.App;

public partial class CompactPanelWindow : Window
{
    private const int FallbackPlacementsBeforeMovingAboveTaskbar = 5;
    private const int GwlExtendedStyle = -20;
    private const long WsExTransparent = 0x00000020;
    private const long WsExToolWindow = 0x00000080;
    private const long WsExNoActivate = 0x08000000;
    private const long WsExLayered = 0x00080000;
    private readonly TaskbarPlacementService _placement = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(3) };
    private bool _placing;
    private bool _closed;
    private TaskbarPlacement? _lastOnTaskbarPlacement;
    private int _consecutiveFallbackPlacements;
    private IntPtr _handle;

    public CompactPanelWindow(MonitorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Taskbar.LayoutChanged += Taskbar_LayoutChanged;
        SourceInitialized += (_, _) =>
        {
            _handle = new WindowInteropHelper(this).Handle;
            var style = GetWindowLongPtr(_handle, GwlExtendedStyle).ToInt64();
            SetWindowLongPtr(_handle, GwlExtendedStyle,
                new IntPtr(style | WsExTransparent | WsExToolWindow | WsExNoActivate | WsExLayered));
        };
        Loaded += async (_, _) => { _timer.Start(); await PlaceAsync(); };
        _timer.Tick += async (_, _) => await PlaceAsync();
        Closed += (_, _) => { _closed = true; _timer.Stop(); viewModel.Taskbar.LayoutChanged -= Taskbar_LayoutChanged; };
    }
    private async void Taskbar_LayoutChanged() => await PlaceAsync();

    private async Task PlaceAsync()
    {
        if (_placing || _closed) return;
        _placing = true;
        try
        {
            var taskbar = ((MonitorViewModel)DataContext).Taskbar;
            if (!taskbar.Sections.Any(section => section.Enabled))
            {
                Hide(); ((MonitorViewModel)DataContext).UpdateTaskbarStatus("All taskbar sections are hidden"); return;
            }
            var options = new TaskbarPlacementOptions(taskbar.Location, taskbar.HorizontalOffset, taskbar.CustomPositionPercent, taskbar.TotalWidth);
            var placement = await Task.Run(() => _placement.Locate(options));
            if (_closed) return;
            var monitor = (MonitorViewModel)DataContext;
            if (!placement.Visible)
            {
                _consecutiveFallbackPlacements = 0;
                monitor.UpdateTaskbarStatus(placement.Status);
                Hide();
                return;
            }

            if (placement.OnTaskbar)
            {
                _lastOnTaskbarPlacement = placement;
                _consecutiveFallbackPlacements = 0;
                monitor.UpdateTaskbarStatus(placement.Status);
            }
            else if (_lastOnTaskbarPlacement is { } previousPlacement)
            {
                _consecutiveFallbackPlacements++;
                // UI Automation can briefly return an incomplete taskbar tree while Explorer
                // refreshes. Keep the panel in its last valid taskbar slot through those
                // short gaps instead of making it jump above the taskbar.
                if (_consecutiveFallbackPlacements < FallbackPlacementsBeforeMovingAboveTaskbar)
                {
                    placement = previousPlacement;
                    monitor.UpdateTaskbarStatus("Keeping the last taskbar position while Windows refreshes its layout");
                }
                else
                {
                    monitor.UpdateTaskbarStatus(placement.Status);
                }
            }
            else
            {
                _consecutiveFallbackPlacements++;
                monitor.UpdateTaskbarStatus(placement.Status);
            }
            if (!IsVisible) Show();
            if (PresentationSource.FromVisual(this)?.CompositionTarget is { } target)
            {
                var size = target.TransformFromDevice.Transform(new Vector(placement.Width, placement.Height));
                Width = size.X; Height = size.Y;
            }
            TaskbarPlacementService.PositionOverlay(_handle, placement);
            Opacity = 1;
        }
        catch (Exception ex)
        {
            if (!_closed) ((MonitorViewModel)DataContext).UpdateTaskbarStatus("Unable to position the taskbar panel: " + ex.Message);
        }
        finally { _placing = false; }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
}
