using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using TaskbarMonitor.Core.Settings;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using FontFamily = System.Windows.Media.FontFamily;

namespace TaskbarMonitor.App;

/// <summary>Taskbar-only appearance and layout. It never changes dashboard theme resources.</summary>
public sealed class TaskbarCustomizationViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly TaskbarSettings _settings;
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly Action _save;
    private TaskbarSectionViewModel _selectedSection;
    private string _selectedPreset = "Custom";
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? LayoutChanged;
    public ObservableCollection<TaskbarSectionViewModel> Sections { get; } = [];
    public string[] Locations { get; } = ["Near system tray", "Left", "Center", "Custom"];
    public string[] Styles { get; } = ["Colored text", "Soft pills", "Outlined", "Accent lines", "Solid tiles"];
    public string[] Fonts { get; } = ["Segoe UI", "Consolas"];
    public string[] Presets { get; } = ["Custom", "Colored text", "Soft pills", "Outlined", "Accent lines", "Solid tiles"];

    public TaskbarCustomizationViewModel(TaskbarSettings settings, Action save)
    {
        _settings = settings;
        _save = save;
        _settings.Location = Locations.Contains(settings.Location) ? settings.Location : "Near system tray";
        _settings.Gap = Math.Clamp(settings.Gap, 0, 24);
        _settings.HorizontalOffset = Math.Clamp(settings.HorizontalOffset, -400, 400);
        _settings.CustomPositionPercent = Math.Clamp(settings.CustomPositionPercent, 0, 100);
        var defaults = new TaskbarSettings();
        var ordered = (settings.Sections ?? []).Where(s => defaults.Sections.Any(d => d.Id == s.Id)).GroupBy(s => s.Id).Select(g => g.First()).ToList();
        ordered.AddRange(defaults.Sections.Where(d => ordered.All(s => s.Id != d.Id)));
        var needsTextMigration = settings.TextSizeVersion < 1;
        if (needsTextMigration)
        {
            foreach (var section in ordered.Where(section => section.FontSize <= 12))
            {
                var previous = Math.Max(9, section.FontSize);
                section.FontSize = Math.Min(14, previous + 2);
                section.Width = Math.Min(190, Math.Max(section.Id == "Network" ? 135 : 95, section.Width * section.FontSize / previous));
            }
            settings.TextSizeVersion = 1;
        }
        foreach (var section in ordered)
        {
            var model = new TaskbarSectionViewModel(section, Changed);
            model.SetGap(settings.Gap);
            Sections.Add(model);
        }
        settings.Sections = Sections.Select(section => section.Settings).ToList();
        _selectedSection = Sections[0];
        _selectedPreset = Sections.Select(s => s.Style).Distinct().Count() == 1 ? Sections[0].Style : "Custom";
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); save(); };
        if (needsTextMigration) _saveTimer.Start();
    }

    public TaskbarSectionViewModel SelectedSection { get => _selectedSection; set { if (value is null) return; _selectedSection = value; Notify(); } }
    public string Location { get => _settings.Location; set { if (!Locations.Contains(value) || value == _settings.Location) return; _settings.Location = value; Changed(true); } }
    public double CustomPositionPercent { get => _settings.CustomPositionPercent; set { _settings.CustomPositionPercent = Math.Clamp(value, 0, 100); _settings.Location = "Custom"; Changed(true); } }
    public double HorizontalOffset { get => _settings.HorizontalOffset; set { _settings.HorizontalOffset = Math.Clamp(value, -400, 400); Changed(true); } }
    public double Gap { get => _settings.Gap; set { _settings.Gap = Math.Clamp(value, 0, 24); Changed(true); } }
    public double TotalWidth => Math.Max(70, Sections.Where(section => section.Enabled).Sum(section => section.Width + Gap));
    public string SelectedPreset { get => _selectedPreset; set { if (!Presets.Contains(value)) return; _selectedPreset = value; if (value != "Custom") foreach (var section in Sections) section.ApplyStyle(value); Changed(false); } }
    public string GapLabel => $"{Gap:0} px";
    public string OffsetLabel => $"{HorizontalOffset:+0;-0;0} px";
    public string PositionLabel => $"{CustomPositionPercent:0}% across the taskbar";

    private void Changed(bool layout)
    {
        foreach (var section in Sections) section.SetGap(Gap);
        Notify(string.Empty);
        _saveTimer.Stop(); _saveTimer.Start();
        if (layout) LayoutChanged?.Invoke();
    }

    public void UpdateReadings(MonitorViewModel monitor)
    {
        foreach (var section in Sections)
        {
            var primary = section.Id switch { "CPU" => monitor.CpuUsageText + "%", "GPU" => monitor.GpuUsageText + "%", "RAM" => monitor.CompactRamText.Replace("RAM ", ""), _ => monitor.CompactDownloadText };
            var detail = section.Id switch { "CPU" => monitor.CpuTemperatureText, "GPU" => monitor.GpuTemperatureText, "RAM" => monitor.RamUsedText + " GB", _ => monitor.CompactUploadText };
            section.UpdateReadings(primary, detail);
        }
    }

    public void MoveSelected(int direction)
    {
        var index = Sections.IndexOf(SelectedSection);
        var destination = Math.Clamp(index + direction, 0, Sections.Count - 1);
        if (index == destination) return;
        Sections.Move(index, destination);
        _settings.Sections = Sections.Select(section => section.Settings).ToList();
        Changed(true);
    }

    public void Reset()
    {
        var defaults = new TaskbarSettings();
        _settings.Location = defaults.Location; _settings.Gap = defaults.Gap;
        _settings.HorizontalOffset = 0; _settings.CustomPositionPercent = 75;
        foreach (var section in Sections) section.Reset(defaults.Sections.First(s => s.Id == section.Id));
        for (var index = 0; index < defaults.Sections.Count; index++)
        {
            var currentIndex = Sections.IndexOf(Sections.First(section => section.Id == defaults.Sections[index].Id));
            if (currentIndex != index) Sections.Move(currentIndex, index);
        }
        _settings.Sections = Sections.Select(section => section.Settings).ToList();
        _selectedPreset = "Colored text";
        Changed(true);
    }

    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    public void Dispose() { if (_saveTimer.IsEnabled) { _saveTimer.Stop(); _save(); } }
}

public sealed class TaskbarSectionViewModel : INotifyPropertyChanged
{
    private readonly Action<bool> _changed;
    private string _primary = "—", _detail = "—", _colorError = "";
    private double _gap;
    public TaskbarSectionSettings Settings { get; }
    public event PropertyChangedEventHandler? PropertyChanged;

    public TaskbarSectionViewModel(TaskbarSectionSettings settings, Action<bool> changed)
    {
        Settings = settings; _changed = changed;
        Settings.Width = Math.Clamp(settings.Width, 64, 190);
        Settings.FontSize = Math.Clamp(settings.FontSize, 9, 16);
        Settings.FillOpacity = Math.Clamp(settings.FillOpacity, 0, 1);
        if (!TryColor(settings.TextColor, out _)) Settings.TextColor = "#EDF1FA";
        if (!TryColor(settings.AccentColor, out _)) Settings.AccentColor = "#8AB1FF";
        if (!TryColor(settings.BackgroundColor, out _)) Settings.BackgroundColor = "#8AB1FF";
        if (!new[] { "Colored text", "Soft pills", "Outlined", "Accent lines", "Solid tiles" }.Contains(settings.Style)) Settings.Style = "Colored text";
        if (settings.FontFamily is not ("Segoe UI" or "Consolas")) Settings.FontFamily = "Segoe UI";
    }
    public string Id => Settings.Id;
    public override string ToString() => Id;
    public string Label => Id == "Network" ? "NET" : Id;
    public string Primary => _primary;
    public string Detail => _detail;
    public bool Enabled { get => Settings.Enabled; set { Settings.Enabled = value; Change(true); } }
    public bool ShowLabel { get => Settings.ShowLabel; set { Settings.ShowLabel = value; Change(false); } }
    public bool ShowDetail { get => Settings.ShowDetail; set { Settings.ShowDetail = value; Change(false); } }
    public string Style { get => Settings.Style; set { Settings.Style = value; Change(false); } }
    public string TextColor { get => Settings.TextColor; set => SetColor(value, color => Settings.TextColor = color); }
    public string AccentColor { get => Settings.AccentColor; set => SetColor(value, color => Settings.AccentColor = color); }
    public string BackgroundColor { get => Settings.BackgroundColor; set => SetColor(value, color => Settings.BackgroundColor = color); }
    public double FillOpacity { get => Settings.FillOpacity; set { Settings.FillOpacity = Math.Clamp(value, 0, 1); Change(false); } }
    public double FontSize { get => Settings.FontSize; set { Settings.FontSize = Math.Clamp(value, 9, 16); Change(false); } }
    public double Width { get => Settings.Width; set { Settings.Width = Math.Clamp(value, 64, 190); Change(true); } }
    public string FontName { get => Settings.FontFamily; set { Settings.FontFamily = value; Change(false); } }
    public FontFamily Font => new(Settings.FontFamily);
    public double DetailFontSize => Math.Max(9, FontSize - 1);
    public double LabelFontSize => Math.Max(10, FontSize - 1);
    public string ColorError => _colorError;
    public string FillLabel => $"{FillOpacity * 100:0}% fill";
    public string FontSizeLabel => $"{FontSize:0} px";
    public string WidthLabel => $"{Width:0} px";
    public Visibility SectionVisibility => Enabled ? Visibility.Visible : Visibility.Collapsed;
    public Visibility LabelVisibility => ShowLabel ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DetailVisibility => ShowDetail ? Visibility.Visible : Visibility.Collapsed;
    public Thickness Spacing => new(0, 0, _gap, 0);
    public Brush TextBrush => ParseBrush(TextColor);
    public Brush DetailBrush { get { var brush = ParseBrush(TextColor); brush.Opacity = .78; return brush; } }
    public Brush AccentBrush => ParseBrush(AccentColor);
    public Brush FillBrush
    {
        get
        {
            if (Style is not ("Soft pills" or "Solid tiles")) return new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
            var brush = ParseBrush(BackgroundColor); brush.Opacity = FillOpacity; return brush;
        }
    }
    public Thickness OutlineThickness => Style switch { "Outlined" => new(1), "Accent lines" => new(0, 0, 0, 2), _ => new(0) };
    public CornerRadius Corners => Style switch { "Soft pills" => new(16), "Solid tiles" => new(4), "Accent lines" => new(0), _ => new(7) };

    public void SetGap(double gap) { _gap = gap; Notify(nameof(Spacing)); }
    public void UpdateReadings(string primary, string detail) { _primary = primary; _detail = detail; Notify(nameof(Primary)); Notify(nameof(Detail)); }
    public void ApplyStyle(string style)
    {
        Settings.Style = style;
        if (style == "Soft pills") { Settings.BackgroundColor = Settings.AccentColor; Settings.FillOpacity = .18; }
        if (style == "Solid tiles")
        {
            var accent = (Color)ColorConverter.ConvertFromString(Settings.AccentColor);
            Settings.BackgroundColor = $"#{(int)(accent.R * .30):X2}{(int)(accent.G * .30):X2}{(int)(accent.B * .30):X2}";
            Settings.FillOpacity = .9;
        }
        Notify(string.Empty);
    }
    public void Reset(TaskbarSectionSettings defaults)
    {
        _colorError = "";
        Settings.Enabled = true; Settings.Style = defaults.Style; Settings.TextColor = defaults.TextColor;
        Settings.AccentColor = defaults.AccentColor; Settings.BackgroundColor = defaults.BackgroundColor;
        Settings.FillOpacity = defaults.FillOpacity; Settings.FontSize = defaults.FontSize;
        Settings.FontFamily = defaults.FontFamily; Settings.Width = defaults.Width;
        Settings.ShowLabel = true; Settings.ShowDetail = true; Notify(string.Empty);
    }
    private void SetColor(string text, Action<string> set)
    {
        if (!TryColor(text, out var parsed)) { _colorError = "Use a hex color such as #8AB1FF."; Notify(nameof(ColorError)); Notify(string.Empty); return; }
        _colorError = ""; set($"#{parsed.R:X2}{parsed.G:X2}{parsed.B:X2}"); Change(false);
    }
    private void Change(bool layout) { Notify(string.Empty); _changed(layout); }
    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    private static SolidColorBrush ParseBrush(string value) => new((Color)ColorConverter.ConvertFromString(value));
    private static bool TryColor(string? value, out Color parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(value) || value.Length != 7 || value[0] != '#') return false;
        try { parsed = (Color)ColorConverter.ConvertFromString(value); return true; } catch (Exception ex) when (ex is FormatException or NotSupportedException or ArgumentException) { return false; }
    }
}
