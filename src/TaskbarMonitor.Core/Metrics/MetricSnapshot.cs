namespace TaskbarMonitor.Core.Metrics;

public sealed record MetricSnapshot(
    double? CpuUsage,
    double? CpuTemperature,
    double? GpuUsage,
    double? GpuTemperature,
    double? RamUsedBytes,
    double? RamTotalBytes,
    double? DownloadBytesPerSecond,
    double? UploadBytesPerSecond,
    DateTimeOffset CapturedAt,
    string? SensorStatus = null,
    string? CpuTemperatureStatus = null)
{
    public static MetricSnapshot Empty { get; } = new(null, null, null, null, null, null, null, null, DateTimeOffset.MinValue);
}
