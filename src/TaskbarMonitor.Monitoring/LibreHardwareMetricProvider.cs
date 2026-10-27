using LibreHardwareMonitor.Hardware;
using TaskbarMonitor.Core.Metrics;

namespace TaskbarMonitor.Monitoring;

/// <summary>Reads optional CPU/GPU load and temperature sensors. Missing sensors stay null.</summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class LibreHardwareMetricProvider : IMetricProvider
{
    private readonly Computer _computer;
    private readonly object _gate = new();
    private bool _opened;
    private bool _disposed;
    public LibreHardwareMetricProvider(bool cpuOnly = false)
    {
        _computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = !cpuOnly };
    }

    public MetricSnapshot Sample()
    {
        lock (_gate)
        {
            if (_disposed) return MetricSnapshot.Empty;
            try
            {
                if (!_opened)
                {
                    _computer.Open();
                    _opened = true;
                }

                _computer.Accept(new UpdateVisitor());
                var cpu = FindHardware(HardwareType.Cpu);
                var gpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel);
                var temperature = Read(cpu, SensorType.Temperature, "package", "tctl/tdie", "core average");
                var temperatureStatus = DescribeCpuTemperature(temperature);
                return new MetricSnapshot(
                    Read(cpu, SensorType.Load, "total", "cpu total"),
                    temperature,
                    Read(gpu, SensorType.Load, "core", "d3d 3d", "gpu core"),
                    Read(gpu, SensorType.Temperature),
                    null, null, null, null, DateTimeOffset.UtcNow,
                    temperature is null ? "CPU temperature needs sensor setup" : null, temperatureStatus);
            }
            catch (Exception ex)
            {
                return new MetricSnapshot(null, null, null, null, null, null, null, null,
                    DateTimeOffset.UtcNow, "Hardware sensors unavailable: " + ex.Message, "CPU sensor access failed: " + ex.Message);
            }
        }
    }

    private IHardware? FindHardware(HardwareType type) => _computer.Hardware.FirstOrDefault(h => h.HardwareType == type);

    private static string DescribeCpuTemperature(double? temperature)
    {
        if (temperature.HasValue) return "CPU temperature is available.";
        try
        {
            if (!LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled)
                return "PawnIO sensor support is not installed. Install it in Customize to enable CPU temperature.";
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or TypeInitializationException) { }
        return "The sensor driver is installed, but no readable CPU package or core temperature was reported.";
    }

    private static double? Read(IHardware? hardware, SensorType type, params string[] preferredNames)
    {
        if (hardware is null) return null;
        var sensors = Enumerate(hardware).Where(s => s.SensorType == type && s.Value.HasValue
            && float.IsFinite(s.Value.Value)
            && (type != SensorType.Temperature || s.Value.Value is > 0 and <= 150)).ToArray();
        if (sensors.Length == 0) return null;
        foreach (var name in preferredNames)
        {
            var preferred = sensors.FirstOrDefault(s => s.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            if (preferred?.Value is { } preferredValue) return preferredValue;
        }
        return sensors[0].Value;
    }

    private static IEnumerable<ISensor> Enumerate(IHardware hardware)
    {
        foreach (var sensor in hardware.Sensors) yield return sensor;
        foreach (var child in hardware.SubHardware)
            foreach (var sensor in Enumerate(child)) yield return sensor;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            if (_opened) _computer.Close();
            _disposed = true;
        }
    }

    private sealed class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);
        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (var child in hardware.SubHardware) child.Accept(this);
        }
        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }
}
