Taskbar-monitor — portable for Windows x64

1. Extract the ZIP into a folder you can write to.
2. Run TaskbarMonitor.App.exe. No separate .NET installation is needed.
3. Keep portable.flag beside the executable. Your preferences are saved in Data\settings.json.
4. Copy the whole folder, including Data, to carry your settings to another PC.

On the first portable launch, existing Taskbar-monitor preferences on this PC are imported automatically. Once Data\settings.json exists, that portable copy uses its own preferences.

CPU temperature setup
The app requests administrator approval at launch so CPU temperature can be read in the main process. Automatic PawnIO setup is off by default. Use Customize to install sensor support if it is missing; Windows will request approval for driver installation. Internet access is needed only to download missing sensor support.

CPU, GPU, memory, and network monitoring run in the same app process. CPU temperature is sampled automatically when PawnIO and a supported sensor are available.

PawnIO is a Windows hardware driver. Its installation is system-wide and cannot travel as a portable driver. The Taskbar-monitor app and preferences remain portable. Installing PawnIO does not install .NET or Taskbar-monitor.

If sensor setup requires a restart, restart Windows when convenient; the app never restarts Windows automatically.

Controls
- Click the compact taskbar panel or double-click the tray icon to open the dashboard.
- Closing the dashboard keeps monitoring in the tray. Use Exit in its tray menu or Customize to quit.
- Customize: themes, start minimized, refresh rate, automatic CPU sensor setup.
- Taskbar: location, section order, colors, transparency, fonts, widths, and styles.

Some hardware sensors may remain unsupported even when the driver is installed.
