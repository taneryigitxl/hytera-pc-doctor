using PCDoctor.Core.Enums;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Models.Diagnostics;
using PCDoctor.Core.Models.Events;

namespace PCDoctor.Diagnostics.Scanning.Modules;

internal sealed class EventLogScanModule : ScanModuleBase
{
    private readonly IEventLogService _eventLogs;

    public EventLogScanModule(IEventLogService eventLogs, ILocalizationService localization) : base(localization)
    {
        _eventLogs = eventLogs;
    }

    public override ScanModuleKind Kind => ScanModuleKind.EventLogs;
    public override string DisplayName => Loc["Module.EventLogs"];

    protected override async Task<List<ScanFinding>> CollectAsync(CancellationToken cancellationToken)
    {
        var result = await _eventLogs.QueryAsync(new EventLogQueryOptions
        {
            Lookback = TimeSpan.FromHours(24),
            MaxRecords = 200
        }, cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded || result.Value is null)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Event logs are unavailable.");
        }

        var records = result.Value;
        var critical = records.Where(record => record.LevelCode == 1 || ContainsAny(record.Level, "Critical", "Kritik")).ToList();
        var errors = records.Where(record => record.LevelCode == 2 || ContainsAny(record.Level, "Error", "Hata")).ToList();
        var findings = new List<ScanFinding>();

        if (critical.Count > 0)
        {
            var latest = critical[0];
            findings.Add(Finding(
                Severity.Critical,
                Loc.Get("Scan.Events.CriticalTitle", critical.Count),
                Loc["Scan.Events.CriticalSummary"],
                Loc.Get("Scan.Events.CriticalEvidence", latest.TimeCreated, latest.LogName, latest.Source, latest.EventId, Trim(latest.Message)),
                "EventLogReader (System, Application)"));
        }

        if (errors.Count >= 15)
        {
            findings.Add(Finding(
                Severity.Warning,
                Loc.Get("Scan.Events.ManyErrorsTitle", errors.Count),
                Loc["Scan.Events.ManyErrorsSummary"],
                Loc.Get("Scan.Events.ManyErrorsEvidence", errors[0].TimeCreated, errors[0].Source, errors[0].EventId),
                "EventLogReader"));
        }
        else if (errors.Count > 0)
        {
            findings.Add(Finding(
                Severity.Warning,
                Loc.Get("Scan.Events.ErrorsTitle", errors.Count),
                Loc["Scan.Events.ErrorsSummary"],
                Loc.Get("Scan.Events.ErrorsEvidence", errors[0].TimeCreated, errors[0].Source, errors[0].EventId, Trim(errors[0].Message)),
                "EventLogReader"));
        }
        else if (critical.Count == 0)
        {
            findings.Add(Finding(
                Severity.Ok,
                Loc["Scan.Events.OkTitle"],
                Loc["Scan.Events.OkSummary"],
                Loc["Scan.Events.OkEvidence"],
                "EventLogReader"));
        }

        return findings;
    }

    private static bool ContainsAny(string value, params string[] tokens)
        => tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static string Trim(string message)
        => message.Length <= 180 ? message : message[..180] + "...";
}
