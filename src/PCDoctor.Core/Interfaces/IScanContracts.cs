using PCDoctor.Core.Enums;
using PCDoctor.Core.Models.Diagnostics;

namespace PCDoctor.Core.Interfaces;

public interface IScanModule
{
    ScanModuleKind Kind { get; }
    string DisplayName { get; }
    Task<ModuleScanResult> ScanAsync(CancellationToken cancellationToken = default);
}

public interface IScanEngine
{
    IReadOnlyList<IScanModule> Modules { get; }
    Task<DiagnosticReport> RunFullScanAsync(IProgress<ScanProgress>? progress, CancellationToken cancellationToken = default);
}

public interface IRepairService
{
    Task<RepairReport> RepairAsync(IReadOnlyList<ScanFinding> findings, CancellationToken cancellationToken = default);
}
