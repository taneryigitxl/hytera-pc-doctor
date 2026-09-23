using PCDoctor.Core.Enums;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Models.Diagnostics;

namespace PCDoctor.Diagnostics.Scanning.Modules;

internal sealed class StartupScanModule : ScanModuleBase
{
    private readonly IStartupService _startup;

    public StartupScanModule(IStartupService startup, ILocalizationService localization) : base(localization)
    {
        _startup = startup;
    }

    public override ScanModuleKind Kind => ScanModuleKind.Startup;
    public override string DisplayName => Loc["Module.Startup"];

    protected override async Task<List<ScanFinding>> CollectAsync(CancellationToken cancellationToken)
    {
        var result = await _startup.GetStartupEntriesAsync(cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || result.Value is null)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Startup entries are unavailable.");
        }

        var entries = result.Value;
        var preview = string.Join("; ", entries.Take(8).Select(entry => entry.Name));
        var evidence = Loc.Get("Scan.Startup.Evidence", entries.Count, preview);

        if (entries.Count > 20)
        {
            return
            [
                Finding(Severity.Warning, Loc["Scan.Startup.ManyTitle"], Loc["Scan.Startup.ManySummary"], evidence, "Registry Run keys + Startup folders + Win32_StartupCommand")
            ];
        }

        return
        [
            Finding(Severity.Ok, Loc.Get("Scan.Startup.OkTitle", entries.Count), Loc["Scan.Startup.OkSummary"], evidence, "Registry Run keys + Startup folders + Win32_StartupCommand")
        ];
    }
}
