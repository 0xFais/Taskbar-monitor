# Taskbar-monitor

A Windows 11 system monitor built with WPF and C#. The Fluent Desk interface shows CPU and GPU load, available CPU/GPU temperatures, RAM use, live network rates, and a rolling usage graph. A compact overlay displays the readings on unused space in the primary taskbar.

## Portable versions

Both packages run without a Taskbar-monitor installer and keep preferences beside the executable in `Data\settings.json`. Keep `portable.flag` next to `TaskbarMonitor.App.exe`; copy the whole extracted folder to move the app and its settings.

- **Self-contained:** `publish-portable.ps1` creates `publish\portable-win-x64` and a ZIP that bundles the .NET 10 runtime. It can launch without a separate .NET installation.
- **Smaller, framework-dependent:** `publish-framework-dependent.ps1` creates `publish\portable-framework-dependent-win-x64` and a ZIP without .NET runtime files. The PC must have the .NET 10 Desktop Runtime for Windows x64 installed.

On first launch, either portable build imports existing Taskbar-monitor preferences from this PC once.

The app requests administrator approval at launch because CPU sensor access runs in the main process. It reads CPU temperature through the official signed PawnIO driver. Automatic driver setup is off by default; if PawnIO is missing, use **Customize → Install sensor support** or opt into automatic setup. An internet connection is needed to download missing sensor support.

## Build from source

Install the .NET 10 SDK and Visual Studio with the .NET desktop development workload. From this folder run:

```powershell
dotnet run --project src\TaskbarMonitor.App\TaskbarMonitor.App.csproj
```

Or open `TaskbarMonitor.slnx` in Visual Studio and run `TaskbarMonitor.App`. The app currently targets 64-bit Windows. User settings are stored in `%LocalAppData%\TaskbarMonitor\settings.json`.

To publish either portable build, use the matching script above from this folder. Both scripts require the .NET 10 SDK to build.

## What works today

- CPU usage, physical RAM use, and aggregate network rates from active adapters.
- Optional CPU/GPU load and temperature sensors through LibreHardwareMonitor.
- Fluent Desk dashboard with four metric cards, dark and light themes, and a 60-second CPU/GPU/RAM history chart.
- Saved refresh interval, compact panel visibility, **Start minimized**, and a **Run at Windows sign-in** toggle in **Customize**.
- A separate **Taskbar** page with a live preview, placement controls, and individual CPU/GPU/RAM/network appearance settings.
- A notification-area icon with commands to reopen or exit the app.
- Missing or unsupported sensors display as unavailable; they do not stop the other readings.

The compact panel is a separate, non-activating WPF overlay positioned within unused space on the primary Windows taskbar. It does not inject code into Explorer or reserve space within the taskbar. The app rechecks placement when the taskbar layout chang…590 tokens truncated… CPU sensor setup is enabled by default. At launch, the app detects PawnIO. If it is installed, installation is skipped. If it is missing, the app downloads the official signed PawnIO 2.2.0 installer, checks the SHA-256 against the verified release, and starts installation through Windows administrator approval. A cancelled or failed installation leaves the other readings available. If a restart is required, the app reports it and never restarts Windows itself.

CPU, GPU, memory, and network monitoring run in the same app process. CPU readings are sampled automatically when PawnIO and a supported CPU sensor are available. A valid CPU sensor is still required—no temperature is invented when hardware access fails.

**Customize → Automatic CPU sensor setup** controls whether PawnIO is checked and installed at launch. **Install sensor support** is also available for manual setup. Developers can use `--skip-sensor-setup` for unattended UI checks without launching installation prompts.

To rebuild the self-contained portable ZIP, run `./publish-portable.ps1`. To build the smaller framework-dependent package, run `./publish-framework-dependent.ps1`. Both scripts include applicable dependency notices.

The taskbar defaults to 13 px text, with larger matching labels and second-line readings. Existing 11–12 px sections receive a one-time 2 px increase and enough width to keep the larger text readable; position, section order, colors, and styles are preserved.

## Project layout

- `TaskbarMonitor.Core` contains shared metric and settings contracts.
- `TaskbarMonitor.Monitoring` reads optional hardware sensors.
- `TaskbarMonitor.Windows` reads Windows performance, memory, network, and settings data.
- `TaskbarMonitor.App` contains the WPF dashboard, themes, tray icon, and compact panel.

Metric providers implement `IMetricProvider` and return a `MetricSnapshot`. Keep sensor-specific code in the monitoring project and keep WPF concerns inside the app. This keeps new data sources independent from the screen layout.

## Next steps

1. Add selectable adapters and GPU devices, sensor selection, and clearer sensor diagnostics.
2. Add a live appearance preview, font sizing, metric ordering, and compact panel placement controls.
3. Add threshold alerts, longer-term history, and CSV export.
4. Add simulated metric providers and focused tests for sampling, missing hardware, and settings migration.
5. Add contributor setup, hardware compatibility reporting, and release packaging.

## Dependency notice

Hardware sensor support uses [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor), distributed under the Mozilla Public License 2.0. Review its license and third-party notices before redistribution.
