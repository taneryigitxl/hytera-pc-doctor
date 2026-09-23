using PCDoctor.Core.Enums;

namespace PCDoctor.Core.Models.Overlay;

public sealed class OverlayOptions
{
    public bool Enabled { get; set; } = true;
    public OverlayCorner Corner { get; set; } = OverlayCorner.TopRight;
    public bool ShowCpuUsage { get; set; } = true;
    public bool ShowCpuTemp { get; set; } = true;
    public bool ShowGpuUsage { get; set; } = true;
    public bool ShowGpuTemp { get; set; } = true;
    public bool ShowRam { get; set; } = true;
    public bool ShowDisk { get; set; } = true;
}

public sealed class OverlaySnapshot
{
    public double? CpuUsage { get; init; }
    public double? CpuTempC { get; init; }
    public double? GpuUsage { get; init; }
    public double? GpuTempC { get; init; }
    public string GpuName { get; init; } = "GPU";
    public double? RamUsage { get; init; }
    public double? RamUsedGb { get; init; }
    public double? DiskUsage { get; init; }
    public string DiskName { get; init; } = "Disk";
}

public sealed class OverlayLine
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}
