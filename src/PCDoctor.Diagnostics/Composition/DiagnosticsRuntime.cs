using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Logging;
using PCDoctor.Diagnostics.Collectors;
using PCDoctor.Diagnostics.Logging;
using PCDoctor.Diagnostics.Reporting;
using PCDoctor.Diagnostics.Scanning;
using PCDoctor.Diagnostics.Scanning.Modules;
using PCDoctor.Diagnostics.Services;

namespace PCDoctor.Diagnostics.Composition;

public sealed class DiagnosticsRuntime
{
    public IAppLogger Logger { get; }
    public ISystemInfoService SystemInfo { get; }
    public IHardwareService Hardware { get; }
    public INetworkService Network { get; }
    public IEventLogService EventLogs { get; }
    public IStartupService Startup { get; }
    public ISecurityService Security { get; }
    public IScanEngine ScanEngine { get; }
    public IRepairService Repair { get; }
    public IOverlayMetricsService OverlayMetrics { get; }
    public IPrivilegeService Privileges { get; }
    public IReportStore Reports { get; }

    private DiagnosticsRuntime(
        IAppLogger logger,
        ISystemInfoService systemInfo,
        IHardwareService hardware,
        INetworkService network,
        IEventLogService eventLogs,
        IStartupService startup,
        ISecurityService security,
        IScanEngine scanEngine,
        IRepairService repair,
        IOverlayMetricsService overlayMetrics,
        IPrivilegeService privileges,
        IReportStore reports)
    {
        Logger = logger;
        SystemInfo = systemInfo;
        Hardware = hardware;
        Network = network;
        EventLogs = eventLogs;
        Startup = startup;
        Security = security;
        ScanEngine = scanEngine;
        Repair = repair;
        OverlayMetrics = overlayMetrics;
        Privileges = privileges;
        Reports = reports;
    }

    public static DiagnosticsRuntime Create(ILocalizationService localization, IAppLogger? logger = null)
    {
        logger ??= new FileAppLogger();
        var wmi = new WmiClient(logger);
        var hardware = new HardwareCollector(logger, wmi);
        var systemInfo = new SystemInfoCollector(logger, wmi, hardware);
        var network = new NetworkCollector(logger, localization);
        var eventLogs = new EventLogCollector(logger);
        var startup = new StartupCollector(logger, wmi);
        var security = new SecurityCollector(logger, wmi);
        var privileges = new PrivilegeService();
        var reports = new InMemoryReportStore();

        IScanModule[] modules =
        [
            new CpuScanModule(hardware, localization),
            new RamScanModule(hardware, localization),
            new StorageScanModule(hardware, localization),
            new WindowsScanModule(systemInfo, localization),
            new DriverScanModule(hardware, localization),
            new NetworkScanModule(network, localization),
            new EventLogScanModule(eventLogs, localization),
            new StartupScanModule(startup, localization),
            new SecurityScanModule(security, localization)
        ];

        var engine = new ScanEngine(modules, logger, systemInfo, localization);
        var repair = new RepairService(startup, logger, localization);
        var overlay = new OverlayMetricsCollector(logger, wmi);
        return new DiagnosticsRuntime(logger, systemInfo, hardware, network, eventLogs, startup, security, engine, repair, overlay, privileges, reports);
    }
}
