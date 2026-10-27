using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace TaskbarMonitor.Windows;

public sealed record TaskbarPlacement(int X, int Y, int Width, int Height, bool Visible, bool OnTaskbar, string Status);
public sealed record TaskbarPlacementOptions(string Location = "Near system tray", double Offset = 0, double CustomPercent = 75, double Width = 400);

/// <summary>Finds unused space on the primary taskbar without modifying Explorer.</summary>
public sealed class TaskbarPlacementService
{
    public TaskbarPlacement Locate(TaskbarPlacementOptions? options = null)
    {
        options ??= new();
        var taskbar = FindWindow("Shell_TrayWnd", null);
        if (taskbar == IntPtr.Zero || !GetWindowRect(taskbar, out var bounds))
            return new(0, 0, 400, 40, false, false, "Waiting for the Windows taskbar");

        var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        GetMonitorInfo(MonitorFromWindow(taskbar, 2), ref monitor);
        var horizontal = bounds.Right - bounds.Left > bounds.Bottom - bounds.Top;
        var shownHeight = Math.Min(bounds.Bottom, monitor.Monitor.Bottom) - Math.Max(bounds.Top, monitor.Monitor.Top);
        if (!horizontal || shownHeight < 5)
            return new(0, 0, 400, 40, false, false, horizontal ? "Panel follows the auto-hidden taskbar" : "A horizontal taskbar is required");

        if (IsFullScreen(taskbar, monitor.Monitor))
            return new(0, 0, 400, 40, false, true, "Taskbar panel hidden for a full-screen app");

        var occupied = new List<(int Left, int Right)>();
        var trustworthy = false;
        try
        {
            var root = AutomationElement.FromHandle(taskbar);
            var condition = new OrCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.RadioButton),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem));
            var elements = root.FindAll(TreeScope.Descendants, condition);
            foreach (AutomationElement element in elements)
            {
                var current = element.Current;
                var rect = current.BoundingRectangle;
                if (current.IsOffscreen || rect.IsEmpty || rect.Width < 2 || rect.Height < 2) continue;
                if (rect.Bottom <= bounds.Top || rect.Top >= bounds.Bottom) continue;
                occupied.Add(((int)Math.Floor(rect.Left) - 5, (int)Math.Ceiling(rect.Right) + 5));
            }
            trustworthy = occupied.Count >= 2;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or COMException) { }

        var dpi = GetDpiForWindow(taskbar);
        var scale = (dpi == 0 ? 96d : dpi) / 96d;
        var tray = FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
        if (tray != IntPtr.Zero && GetWindowRect(tray, out var trayRect))
            occupied.Add((trayRect.Left - 8, trayRect.Right));
        else
            occupied.Add((bounds.Right - (int)(260 * scale), bounds.Right));

        var desired = (int)(Math.Clamp(options.Width, 70, 1000) * scale);
        var chosen = trustworthy ? TaskbarLayoutEngine.Choose(bounds.Left, bounds.Right, occupied, options, scale) : null;
        var height = Math.Max(28, Math.Min(bounds.Bottom - bounds.Top - 4, (int)(42 * scale)));
        if (chosen is { } placement)
        {
            return new(placement.X, bounds.Top + (bounds.Bottom - bounds.Top - height) / 2,
                placement.Width, height, true, true, $"On the taskbar · {options.Location.ToLowerInvariant()}");
        }

        // Never obscure Start, running applications, or the notification area.
        var fallbackWidth = Math.Min(desired, monitor.Work.Right - monitor.Work.Left - 24);
        var fallbackX = options.Location == "Left" ? monitor.Work.Left + 12 : monitor.Work.Right - fallbackWidth - 12;
        return new(fallbackX, monitor.Work.Bottom - height - 8, fallbackWidth, height,
            true, false, "Taskbar has no free space; panel temporarily sits above it");
    }

    private static bool IsFullScreen(IntPtr taskbar, Rect monitor)
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero || foreground == taskbar) return false;
        var className = new System.Text.StringBuilder(128);
        GetClassName(foreground, className, className.Capacity);
        if (className.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd") return false;
        return GetWindowRect(foreground, out var r) && r.Left <= monitor.Left && r.Top <= monitor.Top && r.Right >= monitor.Right && r.Bottom >= monitor.Bottom;
    }

    public static void PositionOverlay(IntPtr window, TaskbarPlacement placement)
    {
        SetWindowPos(window, new IntPtr(-1), placement.X, placement.Y, placement.Width, placement.Height, 0x0010 | 0x0040);
    }

    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string className, string? name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string? name);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, System.Text.StringBuilder name, int count);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
}
