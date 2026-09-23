using PCDoctor.Core.Enums;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Models.Diagnostics;

namespace PCDoctor.Diagnostics.Scanning.Modules;

internal sealed class SecurityScanModule : ScanModuleBase
{
    private readonly ISecurityService _security;

    public SecurityScanModule(ISecurityService security, ILocalizationService localization) : base(localization)
    {
        _security = security;
    }

    public override ScanModuleKind Kind => ScanModuleKind.Security;
    public override string DisplayName => Loc["Module.Security"];

    protected override async Task<List<ScanFinding>> CollectAsync(CancellationToken cancellationToken)
    {
        var result = await _security.GetSecuritySnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || result.Value is null)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Security status is unavailable.");
        }

        var snapshot = result.Value;
        var findings = new List<ScanFinding>();
        var defender = snapshot.Defender;

        if (!defender.IsAvailable)
        {
            findings.Add(Finding(
                Severity.Warning,
                Loc["Scan.Sec.DefenderMissing"],
                Loc["Scan.Sec.DefenderMissingSummary"],
                defender.Error ?? "Unknown query failure.",
                @"root\Microsoft\Windows\Defender\MSFT_MpComputerStatus"));
        }
        else
        {
            findings.Add(Finding(
                defender.AntivirusEnabled ? Severity.Ok : Severity.Critical,
                defender.AntivirusEnabled ? Loc["Scan.Sec.AvOn"] : Loc["Scan.Sec.AvOff"],
                defender.AntivirusEnabled ? Loc["Scan.Sec.AvOnSummary"] : Loc["Scan.Sec.AvOffSummary"],
                Loc.Get("Scan.Sec.AvEvidence", defender.AntivirusEnabled, defender.RealTimeProtectionEnabled, defender.AntivirusSignatureVersion, defender.AntivirusSignatureLastUpdated),
                "MSFT_MpComputerStatus"));

            if (defender.AntivirusEnabled && !defender.RealTimeProtectionEnabled)
            {
                findings.Add(Finding(
                    Severity.Critical,
                    Loc["Scan.Sec.RealtimeTitle"],
                    Loc["Scan.Sec.RealtimeSummary"],
                    Loc["Scan.Sec.RealtimeEvidence"],
                    "MSFT_MpComputerStatus.RealTimeProtectionEnabled"));
            }

            if (defender.AntivirusSignatureLastUpdated is { } updated && DateTimeOffset.Now - updated > TimeSpan.FromDays(7))
            {
                findings.Add(Finding(
                    Severity.Warning,
                    Loc["Scan.Sec.SigTitle"],
                    Loc["Scan.Sec.SigSummary"],
                    Loc.Get("Scan.Sec.SigEvidence", updated, defender.AntivirusSignatureVersion),
                    "MSFT_MpComputerStatus.AntivirusSignatureLastUpdated"));
            }
        }

        if (snapshot.FirewallProfiles.Count == 0)
        {
            findings.Add(Finding(
                Severity.Warning,
                Loc["Scan.Sec.FwMissing"],
                Loc["Scan.Sec.FwMissingSummary"],
                string.Join(" ", snapshot.Warnings),
                "MSFT_NetFirewallProfile / FirewallPolicy registry"));
        }
        else
        {
            var disabled = snapshot.FirewallProfiles.Where(profile => !profile.Enabled).ToList();
            var evidence = string.Join("; ", snapshot.FirewallProfiles.Select(profile => $"{profile.Name}={profile.Enabled} ({profile.Source})"));
            if (disabled.Count == snapshot.FirewallProfiles.Count)
            {
                findings.Add(Finding(Severity.Critical, Loc["Scan.Sec.FwAllOff"], Loc["Scan.Sec.FwAllOffSummary"], evidence, disabled[0].Source));
            }
            else if (disabled.Count > 0)
            {
                findings.Add(Finding(Severity.Warning, Loc["Scan.Sec.FwSomeOff"], Loc["Scan.Sec.FwSomeOffSummary"], evidence, disabled[0].Source));
            }
            else
            {
                findings.Add(Finding(Severity.Ok, Loc["Scan.Sec.FwOk"], Loc["Scan.Sec.FwOkSummary"], evidence, snapshot.FirewallProfiles[0].Source));
            }
        }

        return findings;
    }
}
