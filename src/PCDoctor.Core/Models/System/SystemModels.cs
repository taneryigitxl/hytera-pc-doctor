namespace PCDoctor.Core.Models.System;

public sealed class OperatingSystemInfo
{
    public bool IsAvailable { get; init; } = true;
    public string? Error { get; init; }
    public string DeviceName { get; init; } = Environment.MachineName;
    public string Caption { get; init; } = "Unknown";
    public string Edition { get; init; } = "Unknown";
    public string Version { get; init; } = "Unknown";
    public string DisplayVersion { get; init; } = "Unknown";
    public string Build { get; init; } = "Unknown";
    public string Architecture { get; init; } = "Unknown";
    public DateTimeOffset? InstallDate { get; init; }
    public DateTimeOffset? LastBootTime { get; init; }
    public TimeSpan Uptime { get; init; }
    public string RegisteredOwner { get; init; } = "Unknown";
    public string ProductId { get; init; } = "Unknown";
    public bool PendingReboot { get; init; }
    public IReadOnlyList<string> PendingRebootReasons { get; init; } = [];

    public static OperatingSystemInfo Unavailable(string error) => new()
    {
        IsAvailable = false,
        Error = error
    };
}

public sealed class DashboardSnapshot
{
    public string DeviceName { get; init; } = Environment.MachineName;
    public string WindowsVersion { get; init; } = "Unknown";
    public string CpuModel { get; init; } = "Unknown";
    public double CpuUsagePercent { get; init; }
    public long RamTotalBytes { get; init; }
    public long RamUsedBytes { get; init; }
    public long RamAvailableBytes { get; init; }
    public double RamUsagePercent { get; init; }
    public long DiskTotalBytes { get; init; }
    public long DiskUsedBytes { get; init; }
    public double DiskUsagePercent { get; init; }
    public string PrimaryVolumeName { get; init; } = "Unknown";
    public string GpuName { get; init; } = "Unknown";
    public string IpAddress { get; init; } = "Unknown";
    public TimeSpan Uptime { get; init; }
    public DateTimeOffset CollectedAt { get; init; } = DateTimeOffset.Now;
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
