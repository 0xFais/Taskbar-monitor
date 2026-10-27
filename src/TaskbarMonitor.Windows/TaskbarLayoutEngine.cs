namespace TaskbarMonitor.Windows;

/// <summary>Pure geometry calculation, separate from Explorer and UI Automation.</summary>
public static class TaskbarLayoutEngine
{
    public static (int X, int Width)? Choose(int leftEdge, int rightEdge, IEnumerable<(int Left, int Right)> occupied, TaskbarPlacementOptions options, double scale = 1)
    {
        var desired = (int)(Math.Clamp(options.Width, 70, 1000) * scale);
        var minimum = (int)(desired * .85);
        var cursor = leftEdge + (int)(12 * scale);
        var gaps = new List<(int Left, int Right)>();
        foreach (var interval in occupied.OrderBy(i => i.Left).Append((Left: rightEdge - 8, Right: rightEdge)))
        {
            var left = Math.Clamp(interval.Left, leftEdge, rightEdge);
            if (left - cursor >= minimum + 4) gaps.Add((cursor, left));
            cursor = Math.Max(cursor, interval.Right);
        }
        if (gaps.Count == 0) return null;
        var fullWidth = gaps.Where(g => g.Right - g.Left >= desired + 4).ToList();
        var candidates = fullWidth.Count > 0 ? fullWidth : gaps;
        var target = options.Location switch
        {
            "Left" => leftEdge,
            "Center" => leftEdge + (rightEdge - leftEdge - desired) / 2,
            "Custom" => leftEdge + (int)((rightEdge - leftEdge - desired) * Math.Clamp(options.CustomPercent, 0, 100) / 100),
            _ => rightEdge - desired
        };
        var gap = candidates.MinBy(g => Math.Abs(Math.Clamp(target, g.Left, Math.Max(g.Left, g.Right - desired)) - target));
        var width = Math.Min(desired, gap.Right - gap.Left - 4);
        var anchored = options.Location switch
        {
            "Near system tray" => gap.Right - width - 2,
            "Left" => gap.Left + 2,
            _ => Math.Clamp(target, gap.Left + 2, gap.Right - width - 2)
        };
        return (Math.Clamp(anchored + (int)(options.Offset * scale), gap.Left + 2, gap.Right - width - 2), width);
    }
}
