using System.Windows;

namespace TaskbarMonitor.App;

public partial class TaskbarSettingsView : System.Windows.Controls.UserControl
{
    public TaskbarSettingsView() => InitializeComponent();
    private TaskbarCustomizationViewModel Model => ((MonitorViewModel)DataContext).Taskbar;
    private void Reset_Click(object sender, RoutedEventArgs e) => Model.Reset();
    private void MoveLeft_Click(object sender, RoutedEventArgs e) => Model.MoveSelected(-1);
    private void MoveRight_Click(object sender, RoutedEventArgs e) => Model.MoveSelected(1);
    private void ChooseColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string property, DataContext: TaskbarSectionViewModel section }) return;
        var current = property switch { "AccentColor" => section.AccentColor, "BackgroundColor" => section.BackgroundColor, _ => section.TextColor };
        using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true, Color = System.Drawing.ColorTranslator.FromHtml(current) };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        var selected = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        if (property == "AccentColor") section.AccentColor = selected;
        else if (property == "BackgroundColor") section.BackgroundColor = selected;
        else section.TextColor = selected;
    }
}
