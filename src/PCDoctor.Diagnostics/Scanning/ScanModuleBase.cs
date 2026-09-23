using System.Diagnostics;
using PCDoctor.Core.Enums;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Models.Diagnostics;

namespace PCDoctor.Diagnostics.Scanning;

internal abstract class ScanModuleBase : IScanModule
{
    protected ScanModuleBase(ILocalizationService localization)
    {
        Loc = localization;
    }

    protected ILocalizationService Loc { get; }
    public abstract ScanModuleKind Kind { get; }
    public abstract string DisplayName { get; }

    public async Task<ModuleScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var clock = Stopwatch.StartNew();
        try
        {
            var findings = await CollectAsync(cancellationToken).ConfigureAwait(false);
            if (findings.Count == 0)
            {
                findings.Add(Finding(
                    Severity.Ok,
                    Loc.Get("ScanEngine.EmptyTitle", DisplayName),
                    Loc["ScanEngine.EmptySummary"],
                    Loc["ScanEngine.EmptyEvidence"]));
            }

            return new ModuleScanResult
            {
                Module = Kind,
                DisplayName = DisplayName,
                Succeeded = true,
                Findings = findings,
                HighestSeverity = findings.Max(finding => finding.Severity),
                Duration = clock.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ModuleScanResult
            {
                Module = Kind,
                DisplayName = DisplayName,
                Succeeded = false,
                ErrorMessage = ex.Message,
                HighestSeverity = Severity.Critical,
                Duration = clock.Elapsed,
                Findings =
                [
                    Finding(
                        Severity.Critical,
                        Loc.Get("ScanEngine.CollectFailedTitle", DisplayName),
                        Loc["ScanEngine.CollectFailedSummary"],
                        ex.ToString())
                ]
            };
        }
    }

    protected abstract Task<List<ScanFinding>> CollectAsync(CancellationToken cancellationToken);

    protected ScanFinding Finding(Severity severity, string title, string summary, string evidence, string? source = null)
        => new()
        {
            Module = Kind,
            Severity = severity,
            Title = title,
            Summary = summary,
            Evidence = evidence,
            Source = source ?? DisplayName
        };
}
