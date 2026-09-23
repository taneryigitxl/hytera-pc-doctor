namespace PCDoctor.Core.Models.Hardware;

public sealed class CpuInfo
{
    public bool IsAvailable { get; init; } = true;
    public string? Error { get; init; }
    public string Name { get; init; } = "Unknown";
    public string Manufacturer { get; init; } = "Unknown";
    public int CoreCount { get; init; }
    public int LogicalProcessorCount { get; init; }
    public uint MaxClockMhz { get; init; }
    public uint CurrentClockMhz { get; init; }
    public uint L2CacheKb { get; init; }
    public uint L3CacheKb { get; init; }
    public string Socket { get; init; } = "Unknown";
    public string Status { get; init; } = "Unknown";
    public string Architecture { get; init; } = "Unknown";
    public double UsagePercent { get; init; }

    public static CpuInfo Unavailable(string error) => new()
    {
        IsAvailable = false,
        Error = error
    };
}

public sealed class MemoryModuleInfo
{
    public string BankLabel { get; init; } = "Unknown";
    public string DeviceLocator { get; init; } = "Unknown";
    public string Manufacturer { get; init; } = "Unknown";
    public string PartNumber { get; init; } = "Unknown";
    public string SerialNumber { get; init; } = "Unknown";
    public long CapacityBytes { get; init; }
    public uint SpeedMhz { get; init; }
    public string FormFactor { get; init; } = "Unknown";
    public string MemoryType { get; init; } = "Unknown";
}

public sealed class MemoryInfo
{
    public bool IsAvailable { get; init; } = true;
    public string? Error { get; init; }
    public long TotalBytes { get; init; }
    public long AvailableBytes { get; init; }
    public long InUseBytes => Math.Max(0, TotalBytes - AvailableBytes);
    public double UsagePercent => TotalBytes <= 0 ? 0 : InUseBytes * 100d / TotalBytes;
    public IReadOnlyList<MemoryModuleInfo> Modules { get; init; } = [];

    public static MemoryInfo Unavailable(string error) => new()
    {
        IsAvailable = false,
        Error = error
    };
}

public sealed class GpuInfo
{
    public bool IsAvailable { get; init; } = true;
    public string? Error { get; init; }
    public string Name { get; init; } = "Unknown";
    public string AdapterRam { get; init; } = "Unknown";
    public string DriverVersion { get; init; } = "Unknown";
    public string DriverDate { get; init; } = "Unknown";
    public string VideoProcessor { get; init; } = "Unknown";
    public string CurrentResolution { get; init; } = "Unknown";
    public uint RefreshRateHz { get; init; }
    public string Status { get; init; } = "Unknown";
    public string PnpDeviceId { get; init; } = "Unknown";
}

public sealed class MotherboardInfo
{
    public bool IsAvailable { get; init; } = true;
    public string? Error { get; init; }
    public string Manufacturer { get; init; } = "Unknown";
    public string Product { get; init; } = "Unknown";
    public string Version { get; init; } = "Unknown";
    public string SerialNumber { get; init; } = "Unknown";

    public static MotherboardInfo Unavailable(string error) => new()
    {
        IsAvailable = false,
        Error = error
    };
}

public sealed class BiosInfo
{
    public bool IsAvailable { get; init; } = true;
    public string? Error { get; init; }
    public string Manufacturer { get; init; } = "Unknown";
    public string Version { get; init; } = "Unknown";
    public string ReleaseDate { get; init; } = "Unknown";
    public string SerialNumber { get; init; } = "Unknown";
    public string SmbiosVersion { get; init; } = "Unknown";

    public static BiosInfo Unavailable(string error) => new()
    {
        IsAvailable = false,
        Error = error
    };
}

public sealed class PhysicalDiskInfo
{
    public string Model { get; init; } = "Unknown";
    public string InterfaceType { get; init; } = "Unknown";
    public string MediaType { get; init; } = "Unknown";
    public string SerialNumber { get; init; } = "Unknown";
    public string Firmware { get; init; } = "Unknown";
    public long SizeBytes { get; init; }
    public uint Partitions { get; init; }
    public string Status { get; init; } = "Unknown";
    public string DeviceId { get; init; } = "Unknown";
}

public sealed class LogicalVolumeInfo
{
    public string Name { get; init; } = "Unknown";
    public string VolumeLabel { get; init; } = string.Empty;
    public string DriveType { get; init; } = "Unknown";
    public string FileSystem { get; init; } = "Unknown";
    public long TotalBytes { get; init; }
    public long FreeBytes { get; init; }
    public long UsedBytes => Math.Max(0, TotalBytes - FreeBytes);
    public double UsagePercent => TotalBytes <= 0 ? 0 : UsedBytes * 100d / TotalBytes;
    public bool IsReady { get; init; }
}

public sealed class NetworkAdapterInfo
{
    public string Name { get; init; } = "Unknown";
    public string Description { get; init; } = "Unknown";
    public string Status { get; init; } = "Unknown";
    public string MacAddress { get; init; } = "Unknown";
    public string Speed { get; init; } = "Unknown";
    public IReadOnlyList<string> UnicastAddresses { get; init; } = [];
    public IReadOnlyList<string> DnsAddresses { get; init; } = [];
    public IReadOnlyList<string> Gateways { get; init; } = [];
    public bool IsOperational { get; init; }
    public string Type { get; init; } = "Unknown";
}

public sealed class HardwareInventory
{
    public CpuInfo Cpu { get; init; } = new();
    public MemoryInfo Memory { get; init; } = new();
    public IReadOnlyList<GpuInfo> Gpus { get; init; } = [];
    public MotherboardInfo Motherboard { get; init; } = new();
    public BiosInfo Bios { get; init; } = new();
    public IReadOnlyList<PhysicalDiskInfo> Disks { get; init; } = [];
    public IReadOnlyList<LogicalVolumeInfo> Volumes { get; init; } = [];
    public IReadOnlyList<NetworkAdapterInfo> Adapters { get; init; } = [];
    public IReadOnlyList<string> CollectionWarnings { get; init; } = [];
}

public sealed class ProblemDeviceInfo
{
    public string Name { get; init; } = "Unknown";
    public string DeviceId { get; init; } = "Unknown";
    public string Manufacturer { get; init; } = "Unknown";
    public string PnpClass { get; init; } = "Unknown";
    public uint ConfigManagerErrorCode { get; init; }
    public string ErrorDescription { get; init; } = "Unknown";
    public string Status { get; init; } = "Unknown";
}
