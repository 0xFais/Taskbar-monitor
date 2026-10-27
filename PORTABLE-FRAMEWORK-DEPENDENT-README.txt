Taskbar-monitor — framework-dependent portable build for Windows x64

This package does not include the .NET runtime. Install the Microsoft .NET 10 Desktop Runtime (x64) on the PC before launching TaskbarMonitor.App.exe. No Taskbar-monitor installer is needed. If the runtime is missing, Windows will show a runtime-required message.

Extract the ZIP to a folder where you can write. Keep portable.flag beside TaskbarMonitor.App.exe. Preferences are stored in Data\settings.json within this folder. Copy the whole extracted folder to move the app and its settings to another PC. The destination PC also needs the .NET 10 Desktop Runtime.

CPU temperature setup
The app requests administrator approval at launch so CPU temperature can be read in the main process. Automatic PawnIO setup is off by default. Use Customize to install sensor support if it is missing; Windows will request approval for driver installation. Internet access is needed only to download missing sensor support.

CPU, GPU, memory, and network monitoring run in the same app process. CPU temperature is sampled automatically when PawnIO and a supported sensor are available.

PawnIO is a Windows hardware driver. Its installation is system-wide and cannot travel in a portable folder. The app and its preferences remain portable. If sensor setup requires a restart, restart Windows when convenient; the app never restarts Windows automatically.

Controls
- Click the compact taskbar panel or double-click the tray icon to open the dashboard.
- Closing the dashboard keeps monitoring in the tray. Use Exit in its tray menu or Customize to quit.
- Customize: themes, start minimized, refresh rate, and automatic CPU sensor setup.
- Taskbar: location, section order, colors, transparency, fonts, widths, and styles.

Some hardware sensors may remain unsupported even when PawnIO is installed.
