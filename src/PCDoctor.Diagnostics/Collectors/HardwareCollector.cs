using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using PCDoctor.Core.Formatting;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Logging;
using PCDoctor.Core.Models.Hardware;
using PCDoctor.Core.Models.Results;
using PCDoctor.Diagnostics.Native;

namespace PCDoctor.Diagnostics.Collectors;

public sealed class HardwareCollector : IHardwareService
{
    private readonly IAppLogger _logger;
    private readonly WmiClient _wmi;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private HardwareInventory? _cached;
    private DateTimeOffset _cachedAt;

    public HardwareCollector(IAppLogger logger, WmiClient wmi)
    {
        _logger = logger;
        _wmi = wmi;
    }

    public async Task<OperationResult<HardwareInventory>> GetInventoryAsync(CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cached is not null && DateTimeOffset.Now - _cachedAt < TimeSpan.FromSeconds(3))
            {
                return OperationResult<HardwareInventory>.Ok(_cached, _cached.CollectionWarnings);
            }

            var inventory = await CollectInventoryAsync(cancellationToken).ConfigureAwait(false);
            _cached = inventory;
            _cachedAt = DateTimeOffset.Now;
            return OperationResult<HardwareInventory>.Ok(inventory, inventory.CollectionWarnings);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private async Task<HardwareInventory> CollectInventoryAsync(CancellationToken cancellationToken)
    {
        var warnings = new List<string>();

        var cpu = await CollectOrUnavailable(
            () => CollectCpuAsync(cancellationToken),
            CpuInfo.Unavailable,
            "CPU",
            warnings).ConfigureAwait(false);

        var memory = await CollectOrUnavailable(
            () => CollectMemoryAsync(cancellationToken),
            MemoryInfo.Unavailable,
            "Memory",
            warnings).ConfigureAwait(false);

        var gpus = await CollectList(
            () => CollectGpusAsync(cancellationToken),
            "GPU",
            warnings).ConfigureAwait(false);

        var motherboard = await CollectOrUnavailable(
            () => CollectMotherboardAsync(cancellationToken),
            MotherboardInfo.Unavailable,
            "Motherboard",
            warnings).ConfigureAwait(false);

        var bios = await CollectOrUnavailable(
            () => CollectBiosAsync(cancellationToken),
            BiosInfo.Unavailable,
            "BIOS",
            warnings).ConfigureAwait(false);

        var disks = await CollectList(
            () => CollectDisksAsync(cancellationToken),
            "Physical disks",
            warnings).ConfigureAwait(false);

        var volumes = CollectVolumes(warnings);
        var adapters = CollectAdapters(warnings);

        return new HardwareInventory
        {
            Cpu = cpu,
            Memory = memory,
            Gpus = gpus,
            Motherboard = motherboard,
            Bios = bios,
            Disks = disks,
            Volumes = volumes,
            Adapters = adapters,
            CollectionWarnings = warnings
        };
    }

    public async Task<OperationResult<IReadOnlyList<ProblemDeviceInfo>>> GetProblemDevicesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await _wmi.QueryAsync(
                "SELECT Name, DeviceID, Manufacturer, PNPClass, ConfigManagerErrorCode, Status FROM Win32_PnPEntity WHERE ConfigManagerErrorCode != 0",
                cancellationToken).ConfigureAwait(false);

            var devices = rows.Select(row =>
            {
                var code = row.Number<uint>("ConfigManagerErrorCode");
                return new ProblemDeviceInfo
                {
                    Name = row.Text("Name"),
                    DeviceId = row.Text("DeviceID"),
                    Manufacturer = row.Text("Manufacturer"),
                    PnpClass = row.Text("PNPClass"),
                    ConfigManagerErrorCode = code,
                    ErrorDescription = DescribeConfigManagerError(code),
                    Status = row.Text("Status")
                };
            }).ToList();

            return OperationResult<IReadOnlyList<ProblemDeviceInfo>>.Ok(devices);
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to query problem Plug and Play devices.", ex);
            return OperationResult<IReadOnlyList<ProblemDeviceInfo>>.Fail(
                "Problem devices could not be read from Win32_PnPEntity.",
                ex.ToString());
        }
    }

    private async Task<CpuInfo> CollectCpuAsync(CancellationToken cancellationToken)
    {
        var rows = await _wmi.QueryAsync(
            "SELECT Name, Manufacturer, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, CurrentClockSpeed, L2CacheSize, L3CacheSize, SocketDesignation, Status, Architecture FROM Win32_Processor",
            cancellationToken).ConfigureAwait(false);

        var row = rows.FirstOrDefault() ?? throw new InvalidOperationException("Win32_Processor returned no records.");
        double usage = 0;
        try
        {
            usage = await SystemMetrics.SampleCpuUsageAsync(400, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Warn("CPU usage sample failed while collecting hardware inventory.", ex);
        }

        return new CpuInfo
        {
            Name = row.Text("Name"),
            Manufacturer = row.Text("Manufacturer"),
            CoreCount = row.Number<int>("NumberOfCores"),
            LogicalProcessorCount = row.Number<int>("NumberOfLogicalProcessors"),
            MaxClockMhz = row.Number<uint>("MaxClockSpeed"),
            CurrentClockMhz = row.Number<uint>("CurrentClockSpeed"),
            L2CacheKb = row.Number<uint>("L2CacheSize"),
            L3CacheKb = row.Number<uint>("L3CacheSize"),
            Socket = row.Text("SocketDesignation"),
            Status = row.Text("Status"),
            Architecture = DescribeCpuArchitecture(row.Number<ushort>("Architecture")),
            UsagePercent = usage
        };
    }

    private async Task<MemoryInfo> CollectMemoryAsync(CancellationToken cancellationToken)
    {
        var (total, available, _) = SystemMetrics.ReadMemory();
        IReadOnlyList<MemoryModuleInfo> modules = [];

        try
        {
            var rows = await _wmi.QueryAsync(
                "SELECT BankLabel, DeviceLocator, Manufacturer, PartNumber, SerialNumber, Capacity, Speed, FormFactor, MemoryType, SMBIOSMemoryType FROM Win32_PhysicalMemory",
                cancellationToken).ConfigureAwait(false);

            modules = rows.Select(row => new MemoryModuleInfo
            {
                BankLabel = row.Text("BankLabel"),
                DeviceLocator = row.Text("DeviceLocator"),
                Manufacturer = row.Text("Manufacturer"),
                PartNumber = row.Text("PartNumber").Trim(),
                SerialNumber = row.Text("SerialNumber"),
                CapacityBytes = row.Number<long>("Capacity"),
                SpeedMhz = row.Number<uint>("Speed"),
                FormFactor = DescribeFormFactor(row.Number<ushort>("FormFactor")),
                MemoryType = DescribeMemoryType(row.Number<uint>("SMBIOSMemoryType"), row.Number<ushort>("MemoryType"))
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.Warn("Win32_PhysicalMemory query failed; totals still come from GlobalMemoryStatusEx.", ex);
        }

        return new MemoryInfo
        {
            TotalBytes = total,
            AvailableBytes = available,
            Modules = modules
        };
    }

    private async Task<IReadOnlyList<GpuInfo>> CollectGpusAsync(CancellationToken cancellationToken)
    {
        var rows = await _wmi.QueryAsync(
            "SELECT Name, AdapterRAM, DriverVersion, DriverDate, VideoProcessor, CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate, Status, PNPDeviceID FROM Win32_VideoController",
            cancellationToken).ConfigureAwait(false);

        return rows.Select(row =>
        {
            var ram = row.Number<long>("AdapterRAM");
            var width = row.Number<uint>("CurrentHorizontalResolution");
            var height = row.Number<uint>("CurrentVerticalResolution");
            return new GpuInfo
            {
                Name = row.Text("Name"),
                AdapterRam = ram > 0 ? ByteFormatter.FromBytes(ram) : "Unknown",
                DriverVersion = row.Text("DriverVersion"),
                DriverDate = row.Date("DriverDate")?.ToString("yyyy-MM-dd") ?? "Unknown",
                VideoProcessor = row.Text("VideoProcessor"),
                CurrentResolution = width > 0 && height > 0 ? $"{width} x {height}" : "Unknown",
                RefreshRateHz = row.Number<uint>("CurrentRefreshRate"),
                Status = row.Text("Status"),
                PnpDeviceId = row.Text("PNPDeviceID")
            };
        }).ToList();
    }

    private async Task<MotherboardInfo> CollectMotherboardAsync(CancellationToken cancellationToken)
    {
        var rows = await _wmi.QueryAsync(
            "SELECT Manufacturer, Product, Version, SerialNumber FROM Win32_BaseBoard",
            cancellationToken).ConfigureAwait(false);

        var row = rows.FirstOrDefault() ?? throw new InvalidOperationException("Win32_BaseBoard returned no records.");
        return new MotherboardInfo
        {
            Manufacturer = row.Text("Manufacturer"),
            Product = row.Text("Product"),
            Version = row.Text("Version"),
            SerialNumber = row.Text("SerialNumber")
        };
    }

    private async Task<BiosInfo> CollectBiosAsync(CancellationToken cancellationToken)
    {
        var rows = await _wmi.QueryAsync(
            "SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate, SerialNumber, SMBIOSMajorVersion, SMBIOSMinorVersion FROM Win32_BIOS",
            cancellationToken).ConfigureAwait(false);

        var row = rows.FirstOrDefault() ?? throw new InvalidOperationException("Win32_BIOS returned no records.");
        var major = row.Number<ushort>("SMBIOSMajorVersion");
        var minor = row.Number<ushort>("SMBIOSMinorVersion");
        return new BiosInfo
        {
            Manufacturer = row.Text("Manufacturer"),
            Version = row.Text("SMBIOSBIOSVersion"),
            ReleaseDate = row.Date("ReleaseDate")?.ToString("yyyy-MM-dd") ?? "Unknown",
            SerialNumber = row.Text("SerialNumber"),
            SmbiosVersion = major > 0 ? $"{major}.{minor}" : "Unknown"
        };
    }

    private async Task<IReadOnlyList<PhysicalDiskInfo>> CollectDisksAsync(CancellationToken cancellationToken)
    {
        var rows = await _wmi.QueryAsync(
            "SELECT Model, InterfaceType, MediaType, SerialNumber, Size, Partitions, Status, DeviceID, FirmwareRevision FROM Win32_DiskDrive",
            cancellationToken).ConfigureAwait(false);

        return rows.Select(row => new PhysicalDiskInfo
        {
            Model = row.Text("Model"),
            InterfaceType = row.Text("InterfaceType"),
            MediaType = row.Text("MediaType"),
            SerialNumber = row.Text("SerialNumber").Trim(),
            Firmware = row.Text("FirmwareRevision"),
            SizeBytes = row.Number<long>("Size"),
            Partitions = row.Number<uint>("Partitions"),
            Status = row.Text("Status"),
            DeviceId = row.Text("DeviceID")
        }).ToList();
    }

    private IReadOnlyList<LogicalVolumeInfo> CollectVolumes(ICollection<string> warnings)
    {
        try
        {
            return DriveInfo.GetDrives()
                .Select(drive =>
                {
                    try
                    {
                        return new LogicalVolumeInfo
                        {
                            Name = drive.Name,
                            VolumeLabel = drive.IsReady ? drive.VolumeLabel : string.Empty,
                            DriveType = drive.DriveType.ToString(),
                            FileSystem = drive.IsReady ? drive.DriveFormat : "Unknown",
                            TotalBytes = drive.IsReady ? drive.TotalSize : 0,
                            FreeBytes = drive.IsReady ? drive.TotalFreeSpace : 0,
                            IsReady = drive.IsReady
                        };
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"Volume {drive.Name} could not be read: {ex.Message}");
                        _logger.Warn($"Failed to read volume {drive.Name}.", ex);
                        return new LogicalVolumeInfo
                        {
                            Name = drive.Name,
                            DriveType = drive.DriveType.ToString(),
                            IsReady = false
                        };
                    }
                })
                .ToList();
        }
        catch (Exception ex)
        {
            warnings.Add("Logical volumes could not be enumerated.");
            _logger.Error("DriveInfo.GetDrives failed.", ex);
            return [];
        }
    }

    private IReadOnlyList<NetworkAdapterInfo> CollectAdapters(ICollection<string> warnings)
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(adapter => adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(adapter =>
                {
                    var properties = adapter.GetIPProperties();
                    return new NetworkAdapterInfo
                    {
                        Name = adapter.Name,
                        Description = adapter.Description,
                        Status = adapter.OperationalStatus.ToString(),
                        MacAddress = FormatMac(adapter.GetPhysicalAddress()),
                        Speed = adapter.Speed > 0 ? FormatLinkSpeed(adapter.Speed) : "Unknown",
                        UnicastAddresses = properties.UnicastAddresses
                            .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
                            .Select(address => address.Address.ToString())
                            .ToList(),
                        DnsAddresses = properties.DnsAddresses
                            .Select(address => address.ToString())
                            .ToList(),
                        Gateways = properties.GatewayAddresses
                            .Select(address => address.Address.ToString())
                            .ToList(),
                        IsOperational = adapter.OperationalStatus == OperationalStatus.Up,
                        Type = adapter.NetworkInterfaceType.ToString()
                    };
                })
                .ToList();
        }
        catch (Exception ex)
        {
            warnings.Add("Network adapters could not be enumerated.");
            _logger.Error("NetworkInterface.GetAllNetworkInterfaces failed.", ex);
            return [];
        }
    }

    private async Task<T> CollectOrUnavailable<T>(Func<Task<T>> collect, Func<string, T> unavailable, string name, ICollection<string> warnings)
    {
        try
        {
            return await collect().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error($"{name} collection failed.", ex);
            warnings.Add($"{name} could not be collected: {ex.Message}");
            return unavailable(ex.Message);
        }
    }

    private async Task<IReadOnlyList<T>> CollectList<T>(Func<Task<IReadOnlyList<T>>> collect, string name, ICollection<string> warnings)
    {
        try
        {
            return await collect().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error($"{name} collection failed.", ex);
            warnings.Add($"{name} could not be collected: {ex.Message}");
            return [];
        }
    }

    internal static string FormatMac(PhysicalAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 0 ? "Unknown" : string.Join(":", bytes.Select(value => value.ToString("X2")));
    }

    internal static string FormatLinkSpeed(long bitsPerSecond)
    {
        double value = bitsPerSecond;
        string[] units = ["bps", "Kbps", "Mbps", "Gbps"];
        var unit = 0;
        while (value >= 1000 && unit < units.Length - 1)
        {
            value /= 1000;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }

    internal static string DescribeConfigManagerError(uint code) => code switch
    {
        1 => "Device is not configured correctly.",
        10 => "Device cannot start.",
        14 => "Device requires a restart.",
        18 => "Device drivers must be reinstalled.",
        22 => "Device is disabled.",
        24 => "Device is not present, not working, or missing drivers.",
        28 => "Drivers for this device are not installed.",
        31 => "Device is not working properly.",
        43 => "Windows stopped this device because it reported problems.",
        45 => "Device is not currently connected.",
        48 => "Windows cannot verify the digital signature for this device.",
        _ => $"ConfigManagerErrorCode {code}"
    };

    private static string DescribeCpuArchitecture(ushort value) => value switch
    {
        0 => "x86",
        1 => "MIPS",
        2 => "Alpha",
        3 => "PowerPC",
        5 => "ARM",
        6 => "ia64",
        9 => "x64",
        12 => "ARM64",
        _ => RuntimeInformation.OSArchitecture.ToString()
    };

    private static string DescribeFormFactor(ushort value) => value switch
    {
        8 => "DIMM",
        12 => "SODIMM",
        13 => "SRIMM",
        _ => value == 0 ? "Unknown" : $"Form factor {value}"
    };

    private static string DescribeMemoryType(uint smbios, ushort legacy) => smbios switch
    {
        20 => "DDR",
        21 => "DDR2",
        24 => "DDR3",
        26 => "DDR4",
        34 => "DDR5",
        _ => legacy switch
        {
            20 => "DDR",
            21 => "DDR2",
            24 => "DDR3",
            _ => "Unknown"
        }
    };
}
