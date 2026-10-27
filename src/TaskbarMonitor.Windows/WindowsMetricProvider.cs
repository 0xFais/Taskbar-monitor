using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using TaskbarMonitor.Core.Metrics;

namespace TaskbarMonitor.Windows;

/// <summary>Reads CPU, physical memory, and aggregate network rates using Windows/.NET APIs.</summary>
public sealed class WindowsMetricProvider : IMetricProvider
{
    private ulong _lastIdle, _lastKernel, _lastUser, _lastReceived, _lastSent;
    private DateTime _lastSample;
    private bool _hasPrevious;

    public MetricSnapshot Sample()
    {
        var now = DateTime.UtcNow;
        GetSystemTimes(out var idleFile, out var kernelFile, out var userFile);
        var idle = ToUInt64(idleFile);
        var kernel = ToUInt64(kernelFile);
        var user = ToUInt64(userFile);
        var received = 0UL;
        var sent = 0UL;
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
            try
            {
                var stats = nic.GetIPv4Statistics();
                received += (ulong)Math.Max(0, stats.BytesReceived);
                sent += (ulong)Math.Max(0, stats.BytesSent);
            }
            catch (NetworkInformationException) { }
        }

        double? cpu = null, downRate = null, upRate = null;
        var elapsed = (now - _lastSample).TotalSeconds;
        if (_hasPrevious && elapsed > 0)
        {
            var total = (kernel - _lastKernel) + (user - _lastUser);
            var idleDelta = idle - _lastIdle;
            if (total > 0 && idleDelta <= total) cpu = Math.Clamp(100d * (total - idleDelta) / total, 0, 100);
            downRate = Math.Max(0, received - _lastReceived) / elapsed;
            upRate = Math.Max(0, sent - _lastSent) / elapsed;
        }

        _lastIdle = idle; _lastKernel = kernel; _lastUser = user;
        _lastReceived = received; _lastSent = sent; _lastSample = now; _hasPrevious = true;
        var memory = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        var hasMemory = GlobalMemoryStatusEx(ref memory);
        return new MetricSnapshot(cpu, null, null, null,
            hasMemory ? memory.TotalPhysical - memory.AvailablePhysical : null,
            hasMemory ? memory.TotalPhysical : null,
            downRate, upRate, DateTimeOffset.UtcNow);
    }

    private static ulong ToUInt64(FileTime value) => ((ulong)value.High << 32) | value.Low;

    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
    [StructLayout(LayoutKind.Sequential)] private struct FileTime { public uint Low; public uint High; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] private struct MemoryStatusEx
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
    public void Dispose() { }
}
