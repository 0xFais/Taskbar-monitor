using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;

namespace TaskbarMonitor.App;

public partial class MetricCard : System.Windows.Controls.UserControl
{
    public MetricCard() => InitializeComponent();
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(MetricCard), new PropertyMetadata(""));
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(string), typeof(MetricCard), new PropertyMetadata(""));
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(MetricCard), new PropertyMetadata("—"));
    public static readonly DependencyProperty SuffixProperty = DependencyProperty.Register(nameof(Suffix), typeof(string), typeof(MetricCard), new PropertyMetadata(""));
    public static readonly DependencyProperty DetailProperty = DependencyProperty.Register(nameof(Detail), typeof(string), typeof(MetricCard), new PropertyMetadata(""));
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(nameof(Progress), typeof(double), typeof(MetricCard), new PropertyMetadata(0d, ProgressChanged));
    public static readonly DependencyProperty ValueFontSizeProperty = DependencyProperty.Register(nameof(ValueFontSize), typeof(double), typeof(MetricCard), new PropertyMetadata(30d));
    public static readonly DependencyProperty ShowProgressProperty = DependencyProperty.Register(nameof(ShowProgress), typeof(bool), typeof(MetricCard), new PropertyMetadata(true, (d, e) => { if (((MetricCard)d).Meter is { } meter) meter.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Hidden; }));
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Icon { get => (string)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Suffix { get => (string)GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }
    public string Detail { get => (string)GetValue(DetailProperty); set => SetValue(DetailProperty, value); }
    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public double ValueFontSize { get => (double)GetValue(ValueFontSizeProperty); set => SetValue(ValueFontSizeProperty, value); }
    public bool ShowProgress { get => (bool)GetValue(ShowProgressProperty); set => SetValue(ShowProgressProperty, value); }
    private static void ProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (MetricCard)d;
        if (card.Meter is null) return;
        var next = Math.Clamp((double)e.NewValue, 0, 100);
        if (card.IsLoaded && SystemParameters.ClientAreaAnimation)
            card.Meter.BeginAnimation(RangeBase.ValueProperty, new DoubleAnimation(next, TimeSpan.FromMilliseconds(180)));
        else card.Meter.Value = next;
    }
}
