using System.Collections.ObjectModel;
using PCDoctor.App.Mvvm;
using PCDoctor.Core.Formatting;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Logging;
using PCDoctor.Core.Models.Hardware;

namespace PCDoctor.App.ViewModels;

public sealed class HardwareViewModel : LoadableViewModel
{
    private readonly IHardwareService _hardware;
    private HardwareInventory? _lastInventory;

    public HardwareViewModel(IHardwareService hardware, IAppLogger logger, ILocalizationService localization)
        : base(logger, localization)
    {
        _hardware = hardware;
    }

    public ObservableCollection<DetailCardModel> Cards { get; } = [];
    public ObservableCollection<string> Warnings { get; } = [];

    protected override void OnLanguageChanged()
    {
        if (_lastInventory is not null)
        {
            Apply(_lastInventory);
        }
    }

    protected override async Task LoadCoreAsync()
    {
        var result = await _hardware.GetInventoryAsync().ConfigureAwait(true);
        if (!result.Succeeded || result.Value is null)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? Loc["Hardware.Unavailable"]);
        }

        Apply(result.Value);
    }

    private void Apply(HardwareInventory inventory)
    {
        _lastInventory = inventory;
        var unknown = Loc["Common.Unknown"];
        Cards.Clear();
        Cards.Add(CpuCard(inventory.Cpu, unknown));
        Cards.Add(MemoryCard(inventory.Memory, unknown));

        if (inventory.Gpus.Count == 0)
        {
            Cards.Add(new DetailCardModel
            {
                Title = Loc["Hardware.Gpu"],
                Caption = Loc["Hardware.NoGpu"],
                Rows = [new DetailRow(Loc["Hardware.Status"], Loc["Common.Unavailable"], unknown)]
            });
        }
        else
        {
            foreach (var gpu in inventory.Gpus)
            {
                Cards.Add(new DetailCardModel
                {
                    Title = Loc["Hardware.Gpu"],
                    Caption = gpu.Name,
                    Rows =
                    [
                        new(Loc["Hardware.Name"], gpu.Name, unknown),
                        new(Loc["Hardware.AdapterRam"], gpu.AdapterRam, unknown),
                        new(Loc["Hardware.VideoProcessor"], gpu.VideoProcessor, unknown),
                        new(Loc["Hardware.Driver"], $"{gpu.DriverVersion} ({gpu.DriverDate})", unknown),
                        new(Loc["Hardware.Resolution"], gpu.CurrentResolution, unknown),
                        new(Loc["Hardware.RefreshRate"], gpu.RefreshRateHz > 0 ? $"{gpu.RefreshRateHz} Hz" : unknown, unknown),
                        new(Loc["Hardware.Status"], gpu.Status, unknown),
                        new(Loc["Hardware.PnpId"], gpu.PnpDeviceId, unknown)
                    ]
                });
            }
        }

        Cards.Add(new DetailCardModel
        {
            Title = Loc["Hardware.Motherboard"],
            Caption = inventory.Motherboard.IsAvailable ? null : inventory.Motherboard.Error,
            Rows =
            [
                new(Loc["Hardware.Manufacturer"], inventory.Motherboard.Manufacturer, unknown),
                new(Loc["Hardware.Product"], inventory.Motherboard.Product, unknown),
                new(Loc["Hardware.Version"], inventory.Motherboard.Version, unknown),
                new(Loc["Hardware.Serial"], inventory.Motherboard.SerialNumber, unknown)
            ]
        });

        Cards.Add(new DetailCardModel
        {
            Title = Loc["Hardware.Bios"],
            Caption = inventory.Bios.IsAvailable ? null : inventory.Bios.Error,
            Rows =
            [
                new(Loc["Hardware.Manufacturer"], inventory.Bios.Manufacturer, unknown),
                new(Loc["Hardware.Version"], inventory.Bios.Version, unknown),
                new(Loc["Hardware.ReleaseDate"], inventory.Bios.ReleaseDate, unknown),
                new(Loc["Hardware.Smbios"], inventory.Bios.SmbiosVersion, unknown),
                new(Loc["Hardware.Serial"], inventory.Bios.SerialNumber, unknown)
            ]
        });

        foreach (var disk in inventory.Disks)
        {
            Cards.Add(new DetailCardModel
            {
                Title = Loc["Hardware.Disk"],
                Caption = disk.Model,
                Rows =
                [
                    new(Loc["Hardware.Model"], disk.Model, unknown),
                    new(Loc["Hardware.Interface"], disk.InterfaceType, unknown),
                    new(Loc["Hardware.Media"], disk.MediaType, unknown),
                    new(Loc["Hardware.Size"], ByteFormatter.FromBytes(disk.SizeBytes), unknown),
                    new(Loc["Hardware.Partitions"], disk.Partitions.ToString(), unknown),
                    new(Loc["Hardware.Firmware"], disk.Firmware, unknown),
                    new(Loc["Hardware.Serial"], disk.SerialNumber, unknown),
                    new(Loc["Hardware.Status"], disk.Status, unknown),
                    new(Loc["Hardware.DeviceId"], disk.DeviceId, unknown)
                ]
            });
        }

        foreach (var volume in inventory.Volumes)
        {
            Cards.Add(new DetailCardModel
            {
                Title = Loc["Hardware.Volume"],
                Caption = volume.Name,
                Rows =
                [
                    new(Loc["Hardware.Name"], volume.Name, unknown),
                    new(Loc["Hardware.Label"], string.IsNullOrWhiteSpace(volume.VolumeLabel) ? Loc["Hardware.EmptyLabel"] : volume.VolumeLabel, unknown),
                    new(Loc["Hardware.Type"], volume.DriveType, unknown),
                    new(Loc["Hardware.FileSystem"], volume.FileSystem, unknown),
                    new(Loc["Hardware.Capacity"], ByteFormatter.FromBytes(volume.TotalBytes), unknown),
                    new(Loc["Hardware.Used"], $"{ByteFormatter.FromBytes(volume.UsedBytes)} ({ByteFormatter.Percentage(volume.UsagePercent)})", unknown),
                    new(Loc["Hardware.Free"], ByteFormatter.FromBytes(volume.FreeBytes), unknown),
                    new(Loc["Hardware.Ready"], volume.IsReady ? Loc["Common.Yes"] : Loc["Common.No"], unknown)
                ]
            });
        }

        foreach (var adapter in inventory.Adapters)
        {
            Cards.Add(new DetailCardModel
            {
                Title = Loc["Hardware.Adapter"],
                Caption = adapter.Name,
                Rows =
                [
                    new(Loc["Hardware.Name"], adapter.Name, unknown),
                    new(Loc["Hardware.Description"], adapter.Description, unknown),
                    new(Loc["Hardware.Type"], adapter.Type, unknown),
                    new(Loc["Hardware.Status"], adapter.Status, unknown),
                    new(Loc["Hardware.Mac"], adapter.MacAddress, unknown),
                    new(Loc["Hardware.Speed"], adapter.Speed, unknown),
                    new(Loc["Hardware.Ipv4"], adapter.UnicastAddresses.Count == 0 ? Loc["Common.None"] : string.Join(", ", adapter.UnicastAddresses), unknown),
                    new(Loc["Hardware.Gateway"], adapter.Gateways.Count == 0 ? Loc["Common.None"] : string.Join(", ", adapter.Gateways), unknown),
                    new(Loc["Hardware.Dns"], adapter.DnsAddresses.Count == 0 ? Loc["Common.None"] : string.Join(", ", adapter.DnsAddresses), unknown)
                ]
            });
        }

        Warnings.Clear();
        foreach (var warning in inventory.CollectionWarnings)
        {
            Warnings.Add(warning);
        }
    }

    private DetailCardModel CpuCard(CpuInfo cpu, string unknown) => new()
    {
        Title = Loc["Hardware.Cpu"],
        Caption = cpu.IsAvailable ? cpu.Name : cpu.Error,
        Rows =
        [
            new(Loc["Hardware.Name"], cpu.Name, unknown),
            new(Loc["Hardware.Manufacturer"], cpu.Manufacturer, unknown),
            new(Loc["Hardware.Architecture"], cpu.Architecture, unknown),
            new(Loc["Hardware.CoresLogical"], $"{cpu.CoreCount} / {cpu.LogicalProcessorCount}", unknown),
            new(Loc["Hardware.Clock"], Loc.Get("Hardware.ClockValue", cpu.CurrentClockMhz, cpu.MaxClockMhz), unknown),
            new(Loc["Hardware.Cache"], $"{cpu.L2CacheKb} KB / {cpu.L3CacheKb} KB", unknown),
            new(Loc["Hardware.Socket"], cpu.Socket, unknown),
            new(Loc["Hardware.Status"], cpu.Status, unknown),
            new(Loc["Hardware.Usage"], ByteFormatter.Percentage(cpu.UsagePercent), unknown)
        ]
    };

    private DetailCardModel MemoryCard(MemoryInfo memory, string unknown)
    {
        var rows = new List<DetailRow>
        {
            new(Loc["Hardware.Total"], ByteFormatter.FromBytes(memory.TotalBytes), unknown),
            new(Loc["Hardware.InUse"], ByteFormatter.FromBytes(memory.InUseBytes), unknown),
            new(Loc["Hardware.Available"], ByteFormatter.FromBytes(memory.AvailableBytes), unknown),
            new(Loc["Hardware.Load"], ByteFormatter.Percentage(memory.UsagePercent), unknown),
            new(Loc["Hardware.Modules"], memory.Modules.Count.ToString(), unknown)
        };

        foreach (var module in memory.Modules)
        {
            rows.Add(new DetailRow(
                module.DeviceLocator,
                $"{ByteFormatter.FromBytes(module.CapacityBytes)} {module.MemoryType} {module.SpeedMhz} MHz · {module.Manufacturer} {module.PartNumber}",
                unknown));
        }

        return new DetailCardModel
        {
            Title = Loc["Hardware.Memory"],
            Caption = memory.IsAvailable ? "GlobalMemoryStatusEx + Win32_PhysicalMemory" : memory.Error,
            Rows = rows
        };
    }
}
