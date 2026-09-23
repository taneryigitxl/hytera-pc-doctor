using PCDoctor.Core.Enums;
using PCDoctor.Core.Formatting;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Models.Diagnostics;

namespace PCDoctor.Diagnostics.Scanning.Modules;

internal sealed class RamScanModule : ScanModuleBase
{
    private readonly IHardwareService _hardware;

    public RamScanModule(IHardwareService hardware, ILocalizationService localization) : base(localization)
    {
        _hardware = hardware;
    }

    public override ScanModuleKind Kind => ScanModuleKind.Ram;
    public override string DisplayName => Loc["Module.Ram"];

    protected override async Task<List<ScanFinding>> CollectAsync(CancellationToken cancellationToken)
    {
        var result = await _hardware.GetInventoryAsync(cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || result.Value is null || !result.Value.Memory.IsAvailable)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? result.Value?.Memory.Error ?? "Memory data is unavailable.");
        }

        var memory = result.Value.Memory;
        var evidence = Loc.Get(
            "Scan.Ram.Evidence",
            ByteFormatter.FromBytes(memory.TotalBytes),
            ByteFormatter.FromBytes(memory.InUseBytes),
            ByteFormatter.FromBytes(memory.AvailableBytes),
            ByteFormatter.Percentage(memory.UsagePercent));

        if (memory.UsagePercent >= 95 || memory.AvailableBytes < 256L * 1024 * 1024)
        {
            return [Finding(Severity.Critical, Loc["Scan.Ram.CriticalTitle"], Loc["Scan.Ram.CriticalSummary"], evidence, "GlobalMemoryStatusEx")];
        }

        if (memory.UsagePercent >= 85)
        {
            return [Finding(Severity.Warning, Loc["Scan.Ram.WarningTitle"], Loc["Scan.Ram.WarningSummary"], evidence, "GlobalMemoryStatusEx")];
        }

        return [Finding(Severity.Ok, Loc["Scan.Ram.OkTitle"], Loc["Scan.Ram.OkSummary"], evidence, "GlobalMemoryStatusEx")];
    }
}
