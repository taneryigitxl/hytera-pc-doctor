using LibreHardwareMonitor.Hardware;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Logging;
using PCDoctor.Core.Models.Overlay;
using PCDoctor.Diagnostics.Native;

namespace PCDoctor.Diagnostics.Collectors;

public sealed class OverlayMetricsCollector : IOverlayMetricsService, IDisposable
{
    private readonly IAppLogger _logger;
    private readonly WmiClient _wmi;
    private readonly object _gate = new();
    private Computer? _computer;
    private bool _computerFailed;

    public OverlayMetricsCollector(IAppLogger logger, WmiClient wmi)
    {
        _logger = logger;
        _wmi = wmi;
    }

    public async Task<OverlaySnapshot> SampleAsync(CancellationToken cancellationToken = default)
    {
        var cpuTask = SystemMetrics.SampleCpuUsageAsync(350, cancellationToken);
        var ram = SystemMetrics.ReadMemory();
        var disk = ReadSystemDisk();
        EnsureComputer();
        var sensors = ReadSensors();
        var cpuUsage = await cpuTask.ConfigureAwait(false);
        var gpuUsage = sensors.GpuUsage ?? await ReadGpuEngineUsageAsync(cancellationToken).ConfigureAwait(false);
        var cpuTemp = FirstValidTemp(
            sensors.CpuTemp,
            await ReadAcpiTempAsync(cancellationToken).ConfigureAwait(false),
            await ReadThermalZoneAsync(cancellationToken).ConfigureAwait(false),
            await ReadTemperatureProbeAsync(cancellationToken).ConfigureAwait(false));

        return new OverlaySnapshot
        {
            CpuUsage = cpuUsage,
            CpuTempC = cpuTemp,
            GpuUsage = gpuUsage,
            GpuTempC = sensors.GpuTemp,
            GpuName = sensors.GpuName ?? "GPU",
            RamUsage = ram.LoadPercent,
            RamUsedGb = ram.TotalBytes <= 0 ? null : (ram.TotalBytes - ram.AvailableBytes) / 1_073_741_824d,
            DiskUsage = disk.Usage,
            DiskName = disk.Name
        };
    }

    private void EnsureComputer()
    {
        if (_computer is not null || _computerFailed)
        {
            return;
        }

        lock (_gate)
        {
            if (_computer is not null || _computerFailed)
            {
                return;
            }

            try
            {
                var computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsMemoryEnabled = false,
                    IsMotherboardEnabled = true,
                    IsControllerEnabled = false,
                    IsNetworkEnabled = false,
                    IsStorageEnabled = false
                };
                computer.Open();
                _computer = computer;
            }
            catch (Exception ex)
            {
                _computerFailed = true;
                _logger.Warn("Hardware sensors could not be opened. Overlay will use Windows fallbacks.", ex);
            }
        }
    }

    private (double? CpuTemp, double? GpuTemp, double? GpuUsage, string? GpuName) ReadSensors()
    {
        if (_computer is null)
        {
            return default;
        }

        lock (_gate)
        {
            try
            {
                _computer.Accept(new SensorUpdateVisitor());
                double? cpuPackage = null;
                double? cpuAny = null;
                double? boardCpu = null;
                double? gpuTemp = null;
                double? gpuLoad = null;
                string? gpuName = null;

                foreach (var hardware in _computer.Hardware)
                {
                    foreach (var sensor in EnumerateSensors(hardware))
                    {
                        if (sensor.Value is not float value || sensor.Name.Contains("Distance", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (sensor.SensorType == SensorType.Temperature && IsPlausibleTemp(value) && IsCpuNamed(sensor.Name, hardware))
                        {
                            if (hardware.HardwareType == HardwareType.Cpu)
                            {
                                cpuAny = Max(cpuAny, value);
                                if (IsPackageSensor(sensor.Name))
                                {
                                    cpuPackage = value;
                                }
                            }
                            else
                            {
                                boardCpu = Max(boardCpu, value);
                            }
                        }

                        if (IsGpu(hardware.HardwareType) && sensor.SensorType == SensorType.Temperature && IsPlausibleTemp(value))
                        {
                            gpuTemp = Max(gpuTemp, value);
                            gpuName = hardware.Name;
                        }

                        if (IsGpu(hardware.HardwareType) &&
                            sensor.SensorType == SensorType.Load &&
                            (sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
                             sensor.Name.Contains("3D", StringComparison.OrdinalIgnoreCase) ||
                             sensor.Name.Equals("GPU Core", StringComparison.OrdinalIgnoreCase)))
                        {
                            gpuLoad = Max(gpuLoad, value);
                            gpuName = hardware.Name;
                        }
                    }
                }

                return (cpuPackage ?? cpuAny ?? boardCpu, gpuTemp, gpuLoad, gpuName);
            }
            catch (Exception ex)
            {
                _logger.Warn("Hardware sensor sample failed.", ex);
                return default;
            }
        }
    }

    private async Task<double?> ReadAcpiTempAsync(CancellationToken cancellationToken)
    {
        try
        {
            var rows = await _wmi.QueryAsync(
                @"root\wmi",
                "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature",
                cancellationToken).ConfigureAwait(false);
            var temps = rows
                .Select(row => row.Number<uint>("CurrentTemperature"))
                .Select(value => NormalizeThermal(value))
                .OfType<double>()
                .ToList();
            return temps.Count == 0 ? null : temps.Max();
        }
        catch (Exception ex)
        {
            _logger.Warn("ACPI thermal zone query failed.", ex);
            return null;
        }
    }

    private async Task<double?> ReadThermalZoneAsync(CancellationToken cancellationToken)
    {
        try
        {
            var rows = await _wmi.QueryAsync(
                "SELECT Temperature FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation",
                cancellationToken).ConfigureAwait(false);
            var temps = rows
                .Select(row => row.Number<uint>("Temperature"))
                .Select(value => NormalizeThermal(value))
                .OfType<double>()
                .ToList();
            return temps.Count == 0 ? null : temps.Max();
        }
        catch (Exception ex)
        {
            _logger.Warn("Thermal zone performance query failed.", ex);
            return null;
        }
    }

    private async Task<double?> ReadTemperatureProbeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var rows = await _wmi.QueryAsync(
                "SELECT CurrentReading FROM Win32_TemperatureProbe",
                cancellationToken).ConfigureAwait(false);
            var temps = rows
                .Select(row => row.Number<uint>("CurrentReading"))
                .Select(value => NormalizeThermal(value))
                .OfType<double>()
                .ToList();
            return temps.Count == 0 ? null : temps.Max();
        }
        catch (Exception ex)
        {
            _logger.Warn("Temperature probe query failed.", ex);
            return null;
        }
    }

    private async Task<double?> ReadGpuEngineUsageAsync(CancellationToken cancellationToken)
    {
        foreach (var query in new[]
                 {
                     "SELECT Name, UtilizationPercentage FROM Win32_PerfFormattedData_GPUEngine",
                     "SELECT Name, UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine"
                 })
        {
            try
            {
                var rows = await _wmi.QueryAsync(query, cancellationToken).ConfigureAwait(false);
                var values = rows
                    .Where(row => row.Text("Name").Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                    .Select(row => (double)row.Number<ulong>("UtilizationPercentage"))
                    .Where(value => value is >= 0 and <= 100)
                    .ToList();
                if (values.Count > 0)
                {
                    return values.Max();
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"GPU engine query failed: {query}", ex);
            }
        }

        return null;
    }

    private static (string Name, double? Usage) ReadSystemDisk()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory);
            if (string.IsNullOrWhiteSpace(root))
            {
                return ("Disk", null);
            }

            var drive = new DriveInfo(root);
            if (!drive.IsReady || drive.TotalSize <= 0)
            {
                return (drive.Name.TrimEnd('\\'), null);
            }

            var used = drive.TotalSize - drive.AvailableFreeSpace;
            return (drive.Name.TrimEnd('\\'), used * 100d / drive.TotalSize);
        }
        catch
        {
            return ("Disk", null);
        }
    }

    private static IEnumerable<ISensor> EnumerateSensors(IHardware hardware)
    {
        foreach (var sensor in hardware.Sensors)
        {
            yield return sensor;
        }

        foreach (var sub in hardware.SubHardware)
        {
            foreach (var sensor in EnumerateSensors(sub))
            {
                yield return sensor;
            }
        }
    }

    private static bool IsGpu(HardwareType type)
        => type is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;

    private static bool IsPackageSensor(string name)
        => name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
           name.Contains("Tctl", StringComparison.OrdinalIgnoreCase) ||
           name.Contains("Tdie", StringComparison.OrdinalIgnoreCase) ||
           name.Contains("CCD", StringComparison.OrdinalIgnoreCase);

    private static bool IsCpuNamed(string name, IHardware hardware)
        => hardware.HardwareType == HardwareType.Cpu ||
           name.Contains("CPU", StringComparison.OrdinalIgnoreCase) ||
           name.Contains("Processor", StringComparison.OrdinalIgnoreCase) ||
           IsPackageSensor(name);

    private static double? FirstValidTemp(params double?[] values)
        => values.FirstOrDefault(value => value is double temp && IsPlausibleTemp(temp));

    private static bool IsPlausibleTemp(double value) => value is > 5 and < 115;

    private static double? NormalizeThermal(double raw)
    {
        if (raw <= 0)
        {
            return null;
        }

        foreach (var candidate in new[]
                 {
                     (raw / 10d) - 273.15,
                     raw - 273.15,
                     raw / 10d,
                     raw
                 })
        {
            if (IsPlausibleTemp(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static double? Max(double? current, double next)
        => current is null ? next : Math.Max(current.Value, next);

    public void Dispose()
    {
        lock (_gate)
        {
            _computer?.Close();
            _computer = null;
        }
    }

    private sealed class SensorUpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (var sub in hardware.SubHardware)
            {
                sub.Accept(this);
            }
        }

        public void VisitSensor(ISensor sensor)
        {
        }

        public void VisitParameter(IParameter parameter)
        {
        }
    }
}
