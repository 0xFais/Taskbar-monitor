namespace TaskbarMonitor.Core.Settings;

public sealed class TaskbarSettings
{
    public string Location { get; set; } = "Near system tray";
    public double CustomPositionPercent { get; set; } = 75;
    public double HorizontalOffset { get; set; }
    public double Gap { get; set; } = 10;
    public int TextSizeVersion { get; set; }
    public List<TaskbarSectionSettings> Sections { get; set; } =
    [
        new() { Id = "CPU", TextColor = "#8AB1FF", AccentColor = "#8AB1FF", BackgroundColor = "#8AB1FF", Width = 95 },
        new() { Id = "GPU", TextColor = "#C4ADFF", AccentColor = "#C4ADFF", BackgroundColor = "#C4ADFF", Width = 95 },
        new() { Id = "RAM", TextColor = "#7BD8B7", AccentColor = "#7BD8B7", BackgroundColor = "#7BD8B7", Width = 95 },
        new() { Id = "Network", TextColor = "#EDBA84", AccentColor = "#EDBA84", BackgroundColor = "#EDBA84", Width = 135 }
    ];
}

public sealed class TaskbarSectionSettings
{
    public string Id { get; set; } = "CPU";
    public bool Enabled { get; set; } = true;
    public string Style { get; set; } = "Colored text";
    public string TextColor { get; set; } = "#8AB1FF";
    public string AccentColor { get; set; } = "#8AB1FF";
    public string BackgroundColor { get; set; } = "#8AB1FF";
    public double FillOpacity { get; set; } = .20;
    public double FontSize { get; set; } = 13;
    public string FontFamily { get; set; } = "Segoe UI";
    public double Width { get; set; } = 95;
    public bool ShowLabel { get; set; } = true;
    public bool ShowDetail { get; set; } = true;
}
