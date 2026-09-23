using PCDoctor.Core.Enums;
using PCDoctor.Core.Formatting;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Models.Diagnostics;

namespace PCDoctor.Diagnostics.Scanning.Modules;

internal sealed class CpuScanModule : ScanModuleBase
{
    private readonly IHardwareService _hardware;

    public CpuScanModule(IHardwareService hardware, ILocalizationService localization) : base(localization)
    {
        _hardware = hardware;
    }

    public override ScanModuleKind Kind => ScanModuleKind.Cpu;
    public override string DisplayName => Loc["Module.Cpu"];

    protected override async Task<List<ScanFinding>> CollectAsync(CancellationToken cancellationToken)
    {
        var result = await _hardware.GetInventoryAsync(cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || result.Value is null || !result.Value.Cpu.IsAvailable)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? result.Value?.Cpu.Error ?? "CPU data is unavailable.");
        }

        var cpu = result.Value.Cpu;
        var findings = new List<ScanFinding>();
        var evidence = Loc.Get("Scan.Cpu.Evidence", cpu.Name, cpu.CoreCount, cpu.LogicalProcessorCount, ByteFormatter.Percentage(cpu.UsagePercent), cpu.Status);

        if (cpu.UsagePercent >= 95)
        {
            findings.Add(Finding(Severity.Critical, Loc["Scan.Cpu.CriticalTitle"], Loc["Scan.Cpu.CriticalSummary"], evidence, "GetSystemTimes + Win32_Processor"));
        }
        else if (cpu.UsagePercent >= 85)
        {
            findings.Add(Finding(Severity.Warning, Loc["Scan.Cpu.WarningTitle"], Loc["Scan.Cpu.WarningSummary"], evidence, "GetSystemTimes + Win32_Processor"));
        }
        else
        {
            findings.Add(Finding(Severity.Ok, Loc["Scan.Cpu.OkTitle"], Loc["Scan.Cpu.OkSummary"], evidence, "GetSystemTimes + Win32_Processor"));
        }

        if (!string.Equals(cpu.Status, "OK", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(Finding(Severity.Warning, Loc["Scan.Cpu.StatusTitle"], Loc["Scan.Cpu.StatusSummary"], Loc.Get("Scan.Cpu.StatusEvidence", cpu.Status), "Win32_Processor.Status"));
        }

        return findings;
    }
}
