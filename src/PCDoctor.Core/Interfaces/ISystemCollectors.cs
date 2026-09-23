using PCDoctor.Core.Models.Events;
using PCDoctor.Core.Models.Hardware;
using PCDoctor.Core.Models.Network;
using PCDoctor.Core.Models.Results;
using PCDoctor.Core.Models.Security;
using PCDoctor.Core.Models.Startup;
using PCDoctor.Core.Models.System;

namespace PCDoctor.Core.Interfaces;

public interface ISystemInfoService
{
    Task<OperationResult<OperatingSystemInfo>> GetOperatingSystemAsync(CancellationToken cancellationToken = default);
    Task<OperationResult<DashboardSnapshot>> GetDashboardAsync(CancellationToken cancellationToken = default);
}

public interface IHardwareService
{
    Task<OperationResult<HardwareInventory>> GetInventoryAsync(CancellationToken cancellationToken = default);
    Task<OperationResult<IReadOnlyList<ProblemDeviceInfo>>> GetProblemDevicesAsync(CancellationToken cancellationToken = default);
}

public interface INetworkService
{
    Task<OperationResult<NetworkDiagnosticsResult>> DiagnoseAsync(CancellationToken cancellationToken = default);
}

public interface IEventLogService
{
    Task<OperationResult<IReadOnlyList<WindowsEventRecord>>> QueryAsync(EventLogQueryOptions options, CancellationToken cancellationToken = default);
}

public interface IStartupService
{
    Task<OperationResult<IReadOnlyList<StartupEntry>>> GetStartupEntriesAsync(CancellationToken cancellationToken = default);
    Task<OperationResult<bool>> SetEnabledAsync(StartupEntry entry, bool enabled, CancellationToken cancellationToken = default);
}

public interface ISecurityService
{
    Task<OperationResult<SecuritySnapshot>> GetSecuritySnapshotAsync(CancellationToken cancellationToken = default);
}
