# Contributing

Thank you for helping improve Taskbar-monitor.

## Getting started

1. Install the .NET 10 SDK and Visual Studio's .NET desktop development workload.
2. Open `TaskbarMonitor.slnx` or run the app with `dotnet run --project src\TaskbarMonitor.App\TaskbarMonitor.App.csproj`.
3. Use the **Overview** page to check the readings available on your PC. Missing temperature sensors are normal on some hardware.

## Where to make changes

- Add or adjust shared data shapes in `TaskbarMonitor.Core`.
- Implement a new sensor source in `TaskbarMonitor.Monitoring` or `TaskbarMonitor.Windows`.
- Keep layout, theme resources, and interaction code in `TaskbarMonitor.App`.
- `MetricCard` provides the shared Fluent Desk cards. `HistoryChart` renders actual, bounded samples; avoid filling missing history with invented readings.
- `TaskbarPlacementService` calculates unused taskbar space in physical pixels; the WPF panel handles display scaling. Keep the fallback when no collision-free area exists.
- `TaskbarLayoutEngine` contains the placement calculation without Windows calls. Test right alignment, occupied areas, offsets, scaling, and crowded taskbars here.
- `TaskbarCustomizationViewModel` owns taskbar-only settings and section styles. `TaskbarPanelView` is shared by the real overlay and both live previews. Keep taskbar colors independent from dashboard resource brushes.
- Implement `IMetricProvider` when adding a metric source. Represent unavailable readings with `null` rather than zero.

Keep sampling away from the WPF interface thread. Avoid per-refresh allocations and animations that run continuously. When changing saved settings, retain defaults for fields that are missing from older settings files.

Theme changes replace brush resources because XAML brushes may be frozen. Do not mutate a resource brush's Color property directly.

## Before submitting a change

- Build the solution: `dotnet build TaskbarMonitor.slnx`.
- Run the app and check both themes, missing sensors, compact panel behavior, and display scaling when relevant.
- Describe the tested hardware and any readings that were unavailable.
- Include screenshots for visible interface changes.

Please review the licenses for all new dependencies. LibreHardwareMonitorLib is MPL-2.0 licensed.
