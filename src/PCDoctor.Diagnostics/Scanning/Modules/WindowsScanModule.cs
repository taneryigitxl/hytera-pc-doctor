using PCDoctor.Core.Enums;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Models.Diagnostics;

namespace PCDoctor.Diagnostics.Scanning.Modules;

internal sealed class WindowsScanModule : ScanModuleBase
{
    private readonly ISystemInfoService _systemInfo;

    public WindowsScanModule(ISystemInfoService systemInfo, ILocalizationService localization) : base(localization)
    {
        _systemInfo = systemInfo;
    }

    public override ScanModuleKind Kind => ScanModuleKind.Windows;
    public override string DisplayName => Loc["Module.Windows"];

    protected override async Task<List<ScanFinding>> CollectAsync(CancellationToken cancellationToken)
    {
        var result = await _systemInfo.GetOperatingSystemAsync(cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || result.Value is null)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Windows information is unavailable.");
        }

        var os = result.Value;
        var findings = new List<ScanFinding>
        {
            Finding(
                Severity.Ok,
                Loc["Scan.Windows.OkTitle"],
                Loc.Get("Scan.Windows.OkSummary", os.Caption, os.DisplayVersion, os.Architecture),
                Loc.Get("Scan.Windows.OkEvidence", os.Build, os.InstallDate, os.LastBootTime, FormatUptime(os.Uptime)),
                "Win32_OperatingSystem + HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion")
        };

        if (os.PendingReboot)
        {
            findings.Add(Finding(
                Severity.Warning,
                Loc["Scan.Windows.RebootTitle"],
                Loc["Scan.Windows.RebootSummary"],
                string.Join(" ", os.PendingRebootReasons),
                "CBS RebootPending / WindowsUpdate RebootRequired / PendingFileRenameOperations"));
        }

        if (os.Uptime > TimeSpan.FromDays(14))
        {
            findings.Add(Finding(
                Severity.Warning,
                Loc["Scan.Windows.UptimeTitle"],
                Loc["Scan.Windows.UptimeSummary"],
                Loc.Get("Scan.Windows.UptimeEvidence", FormatUptime(os.Uptime), os.LastBootTime),
                "Win32_OperatingSystem.LastBootUpTime"));
        }

        return findings;
    }

    private static string FormatUptime(TimeSpan uptime)
        => $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
}
