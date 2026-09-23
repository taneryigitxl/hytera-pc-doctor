using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Logging;
using PCDoctor.Core.Models.Hardware;
using PCDoctor.Core.Models.Results;
using PCDoctor.Core.Models.System;
using PCDoctor.Diagnostics.Native;

namespace PCDoctor.Diagnostics.Collectors;

public sealed class SystemInfoCollector : ISystemInfoService
{
    private readonly IAppLogger _logger;
    private readonly WmiClient _wmi;
    private readonly IHardwareService _hardware;

    public SystemInfoCollector(IAppLogger logger, WmiClient wmi, IHardwareService hardware)
    {
        _logger = logger;
        _wmi = wmi;
        _hardware = hardware;
    }

    public async Task<OperationResult<OperatingSystemInfo>> GetOperatingSystemAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var info = await CollectOperatingSystemAsync(cancellationToken).ConfigureAwait(false);
            return OperationResult<OperatingSystemInfo>.Ok(info);
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to collect operating system information.", ex);
            return OperationResult<OperatingSystemInfo>.Fail(
                "Windows information could not be collected from this computer.",
                ex.ToString());
        }
    }

    public async Task<OperationResult<DashboardSnapshot>> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();

        try
        {
            var osTask = GetOperatingSystemAsync(cancellationToken);
            var hardwareTask = _hardware.GetInventoryAsync(cancellationToken);
            var cpuUsageTask = SystemMetrics.SampleCpuUsageAsync(700, cancellationToken);

            await Task.WhenAll(osTask, hardwareTask, cpuUsageTask).ConfigureAwait(false);

            var osResult = osTask.Result;
            var hardwareResult = hardwareTask.Result;
            var cpuUsage = cpuUsageTask.Result;

            if (!osResult.Succeeded || osResult.Value is null)
            {
                warnings.Add(osResult.ErrorMessage ?? "Operating system details are incomplete.");
            }

            if (!hardwareResult.Succeeded || hardwareResult.Value is null)
            {
                return OperationResult<DashboardSnapshot>.Fail(
                    hardwareResult.ErrorMessage ?? "Hardware details could not be collected.",
                    hardwareResult.TechnicalDetails);
            }

            var os = osResult.Value ?? new OperatingSystemInfo();
            var hardware = hardwareResult.Value;
            warnings.AddRange(hardware.CollectionWarnings);

            var volumes = hardware.Volumes.Where(volume => volume.IsReady && volume.DriveType == "Fixed").ToList();
            var primary = volumes.FirstOrDefault(volume => volume.Name.StartsWith("C", StringComparison.OrdinalIgnoreCase))
                          ?? volumes.FirstOrDefault();

            var adapter = hardware.Adapters.FirstOrDefault(item => item.IsOperational)
                          ?? hardware.Adapters.FirstOrDefault();

            var snapshot = new DashboardSnapshot
            {
                DeviceName = os.DeviceName,
                WindowsVersion = FormatWindowsVersion(os),
                CpuModel = hardware.Cpu.IsAvailable ? hardware.Cpu.Name : (hardware.Cpu.Error ?? "Unavailable"),
                CpuUsagePercent = cpuUsage,
                RamTotalBytes = hardware.Memory.TotalBytes,
                RamUsedBytes = hardware.Memory.InUseBytes,
                RamAvailableBytes = hardware.Memory.AvailableBytes,
                RamUsagePercent = hardware.Memory.UsagePercent,
                DiskTotalBytes = primary?.TotalBytes ?? 0,
                DiskUsedBytes = primary?.UsedBytes ?? 0,
                DiskUsagePercent = primary?.UsagePercent ?? 0,
                PrimaryVolumeName = primary?.Name ?? "No fixed volume",
                GpuName = hardware.Gpus.FirstOrDefault()?.Name ?? "No GPU reported",
                IpAddress = adapter?.UnicastAddresses.FirstOrDefault() ?? "No active IPv4 address",
                Uptime = os.Uptime,
                CollectedAt = DateTimeOffset.Now,
                Warnings = warnings
            };

            return OperationResult<DashboardSnapshot>.Ok(snapshot, warnings);
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to build dashboard snapshot.", ex);
            return OperationResult<DashboardSnapshot>.Fail(
                "Dashboard data could not be collected from this computer.",
                ex.ToString());
        }
    }

    private async Task<OperatingSystemInfo> CollectOperatingSystemAsync(CancellationToken cancellationToken)
    {
        var registry = ReadCurrentVersion();
        var pending = ReadPendingReboot();
        IReadOnlyList<WmiRecord> wmiRows = [];

        try
        {
            wmiRows = await _wmi.QueryAsync(
                "SELECT Caption, Version, BuildNumber, OSArchitecture, InstallDate, LastBootUpTime, RegisteredUser, SerialNumber FROM Win32_OperatingSystem",
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Warn("Win32_OperatingSystem query failed; falling back to registry and Environment APIs.", ex);
        }

        var wmi = wmiRows.FirstOrDefault();
        var lastBoot = wmi?.Date("LastBootUpTime") ?? DateTimeOffset.Now - TimeSpan.FromMilliseconds(Environment.TickCount64);
        var installDate = wmi?.Date("InstallDate") ?? registry.InstallDate;

        return new OperatingSystemInfo
        {
            DeviceName = Environment.MachineName,
            Caption = FirstNonEmpty(wmi?.Text("Caption"), registry.ProductName, RuntimeInformation.OSDescription),
            Edition = FirstNonEmpty(registry.Edition, registry.ProductName),
            Version = FirstNonEmpty(wmi?.Text("Version"), registry.CurrentVersion),
            DisplayVersion = FirstNonEmpty(registry.DisplayVersion, registry.ReleaseId),
            Build = registry.Build,
            Architecture = FirstNonEmpty(wmi?.Text("OSArchitecture"), RuntimeInformation.OSArchitecture.ToString()),
            InstallDate = installDate,
            LastBootTime = lastBoot,
            Uptime = DateTimeOffset.Now - lastBoot,
            RegisteredOwner = FirstNonEmpty(wmi?.Text("RegisteredUser"), registry.RegisteredOwner),
            ProductId = FirstNonEmpty(wmi?.Text("SerialNumber"), registry.ProductId),
            PendingReboot = pending.Count > 0,
            PendingRebootReasons = pending
        };
    }

    internal static string FormatWindowsVersion(OperatingSystemInfo os)
    {
        var display = string.IsNullOrWhiteSpace(os.DisplayVersion) || os.DisplayVersion == "Unknown"
            ? os.Version
            : os.DisplayVersion;
        return $"{os.Caption} ({display}, build {os.Build})";
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && value != "Unknown") ?? "Unknown";

    private CurrentVersionInfo ReadCurrentVersion()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key is null)
            {
                return new CurrentVersionInfo();
            }

            var currentBuild = Convert.ToString(key.GetValue("CurrentBuild"), CultureInfo.InvariantCulture) ?? "Unknown";
            var ubr = key.GetValue("UBR");
            var build = ubr is null ? currentBuild : $"{currentBuild}.{ubr}";

            DateTimeOffset? installDate = null;
            if (key.GetValue("InstallDate") is int unix && unix > 0)
            {
                installDate = DateTimeOffset.FromUnixTimeSeconds(unix);
            }

            return new CurrentVersionInfo
            {
                ProductName = Convert.ToString(key.GetValue("ProductName")) ?? "Unknown",
                Edition = Convert.ToString(key.GetValue("EditionID")) ?? "Unknown",
                DisplayVersion = Convert.ToString(key.GetValue("DisplayVersion")) ?? "Unknown",
                ReleaseId = Convert.ToString(key.GetValue("ReleaseId")) ?? "Unknown",
                CurrentVersion = Convert.ToString(key.GetValue("CurrentVersion")) ?? "Unknown",
                Build = build,
                RegisteredOwner = Convert.ToString(key.GetValue("RegisteredOwner")) ?? "Unknown",
                ProductId = Convert.ToString(key.GetValue("ProductId")) ?? "Unknown",
                InstallDate = installDate
            };
        }
        catch (Exception ex)
        {
            _logger.Warn("Could not read HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion.", ex);
            return new CurrentVersionInfo();
        }
    }

    private List<string> ReadPendingReboot()
    {
        var reasons = new List<string>();

        TryAddRebootReason(
            reasons,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending",
            "Component Based Servicing reports RebootPending.");

        TryAddRebootReason(
            reasons,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired",
            "Windows Update Auto Update reports RebootRequired.");

        try
        {
            using var session = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager");
            var pending = session?.GetValue("PendingFileRenameOperations") as string[];
            if (pending is { Length: > 0 })
            {
                reasons.Add($"Session Manager PendingFileRenameOperations contains {pending.Length} entries.");
            }
        }
        catch (Exception ex)
        {
            _logger.Warn("Could not read PendingFileRenameOperations.", ex);
        }

        return reasons;
    }

    private void TryAddRebootReason(ICollection<string> reasons, string path, string message)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(path);
            if (key is not null)
            {
                reasons.Add(message);
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Could not read reboot indicator {path}.", ex);
        }
    }

    private sealed class CurrentVersionInfo
    {
        public string ProductName { get; init; } = "Unknown";
        public string Edition { get; init; } = "Unknown";
        public string DisplayVersion { get; init; } = "Unknown";
        public string ReleaseId { get; init; } = "Unknown";
        public string CurrentVersion { get; init; } = "Unknown";
        public string Build { get; init; } = "Unknown";
        public string RegisteredOwner { get; init; } = "Unknown";
        public string ProductId { get; init; } = "Unknown";
        public DateTimeOffset? InstallDate { get; init; }
    }
}
