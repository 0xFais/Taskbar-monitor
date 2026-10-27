namespace TaskbarMonitor.Core.Metrics;

public interface IMetricProvider : IDisposable
{
    MetricSnapshot Sample();
}
