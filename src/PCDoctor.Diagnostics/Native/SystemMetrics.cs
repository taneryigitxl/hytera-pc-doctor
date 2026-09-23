using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PCDoctor.Diagnostics.Native;

internal static class SystemMetrics
{
    public static (long TotalBytes, long AvailableBytes, uint LoadPercent) ReadMemory()
    {
        var status = new NativeMethods.MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<NativeMethods.MemoryStatusEx>()
        };

        if (!NativeMethods.GlobalMemoryStatusEx(ref status))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "GlobalMemoryStatusEx failed.");
        }

        return ((long)status.TotalPhys, (long)status.AvailPhys, status.MemoryLoad);
    }

    public static async Task<double> SampleCpuUsageAsync(int sampleMilliseconds = 800, CancellationToken cancellationToken = default)
    {
        if (!NativeMethods.GetSystemTimes(out var idle1, out var kernel1, out var user1))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "GetSystemTimes failed.");
        }

        await Task.Delay(sampleMilliseconds, cancellationToken).ConfigureAwait(false);

        if (!NativeMethods.GetSystemTimes(out var idle2, out var kernel2, out var user2))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "GetSystemTimes failed.");
        }

        var idle = idle2 - idle1;
        var kernel = kernel2 - kernel1;
        var user = user2 - user1;
        var total = kernel + user;
        if (total <= 0)
        {
            return 0;
        }

        var busy = total - idle;
        return Math.Clamp(busy * 100d / total, 0, 100);
    }
}
