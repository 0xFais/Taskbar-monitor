using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Pen = System.Windows.Media.Pen;

namespace TaskbarMonitor.App;

public sealed record HistoryPoint(DateTimeOffset Time, double? Value);

public sealed class HistoryChart : FrameworkElement
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(nameof(Points), typeof(IReadOnlyList<HistoryPoint>), typeof(HistoryChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(nameof(LineBrush), typeof(Brush), typeof(HistoryChart), new FrameworkPropertyMetadata(Brushes.CornflowerBlue, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty AreaBrushProperty = DependencyProperty.Register(nameof(AreaBrush), typeof(Brush), typeof(HistoryChart), new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty GridBrushProperty = DependencyProperty.Register(nameof(GridBrush), typeof(Brush), typeof(HistoryChart), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));
    public IReadOnlyList<HistoryPoint>? Points { get => (IReadOnlyList<HistoryPoint>?)GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    public Brush LineBrush { get => (Brush)GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }
    public Brush AreaBrush { get => (Brush)GetValue(AreaBrushProperty); set => SetValue(AreaBrushProperty, value); }
    public Brush GridBrush { get => (Brush)GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0) return;
        var gridPen = new Pen(GridBrush, .7);
        foreach (var fraction in new[] { .15, .5, .85 })
            dc.DrawLine(gridPen, new System.Windows.Point(0, height * fraction), new System.Windows.Point(width, height * fraction));
        if (Points is not { Count: > 0 } points) return;
        var end = points[^1].Time;
        var segments = new List<List<System.Windows.Point>>();
        var segment = new List<System.Windows.Point>();
        foreach (var sample in points)
        {
            if (sample.Value is not { } value) { if (segment.Count > 0) segments.Add(segment); segment = []; continue; }
            var x = Math.Clamp(1 - (end - sample.Time).TotalSeconds / 60, 0, 1) * width;
            segment.Add(new System.Windows.Point(x, height - Math.Clamp(value, 0, 100) / 100 * height));
        }
        if (segment.Count > 0) segments.Add(segment);
        foreach (var group in segments)
        {
            if (group.Count == 1) { dc.DrawEllipse(LineBrush, null, group[0], 2.5, 2.5); continue; }
            var line = new StreamGeometry();
            using (var ctx = line.Open()) { ctx.BeginFigure(group[0], false, false); ctx.PolyLineTo(group.Skip(1).ToList(), true, false); }
            var area = new StreamGeometry();
            using (var ctx = area.Open())
            {
                ctx.BeginFigure(new System.Windows.Point(group[0].X, height), true, true);
                ctx.LineTo(group[0], true, false); ctx.PolyLineTo(group.Skip(1).ToList(), true, false);
                ctx.LineTo(new System.Windows.Point(group[^1].X, height), true, false);
            }
            dc.DrawGeometry(AreaBrush, null, area);
            dc.DrawGeometry(null, new Pen(LineBrush, 2) { LineJoin = PenLineJoin.Round }, line);
        }
    }
}
