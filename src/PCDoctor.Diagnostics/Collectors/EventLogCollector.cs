using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Logging;
using PCDoctor.Core.Models.Events;
using PCDoctor.Core.Models.Results;

namespace PCDoctor.Diagnostics.Collectors;

public sealed class EventLogCollector : IEventLogService
{
    private readonly IAppLogger _logger;

    public EventLogCollector(IAppLogger logger)
    {
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<WindowsEventRecord>>> QueryAsync(
        EventLogQueryOptions options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var records = await Task.Run(() => Read(options, cancellationToken), cancellationToken).ConfigureAwait(false);
            return OperationResult<IReadOnlyList<WindowsEventRecord>>.Ok(records);
        }
        catch (Exception ex)
        {
            _logger.Error("Event log query failed.", ex);
            return OperationResult<IReadOnlyList<WindowsEventRecord>>.Fail(
                "Windows Event Log records could not be read. Some channels require elevated permissions.",
                ex.ToString());
        }
    }

    private IReadOnlyList<WindowsEventRecord> Read(EventLogQueryOptions options, CancellationToken cancellationToken)
    {
        var collected = new List<WindowsEventRecord>();
        var warnings = new List<string>();

        foreach (var logName in options.LogNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var before = collected.Count;
            try
            {
                ReadWithEventReader(logName, options, collected, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Warn($"EventLogReader failed for '{logName}', falling back to classic EventLog.", ex);
            }

            if (collected.Count == before)
            {
                try
                {
                    ReadWithClassicLog(logName, options, collected, cancellationToken);
                }
                catch (Exception fallbackEx)
                {
                    warnings.Add($"{logName}: {fallbackEx.Message}");
                    _logger.Warn($"Classic EventLog also failed for '{logName}'.", fallbackEx);
                }
            }

            if (collected.Count >= options.MaxRecords)
            {
                break;
            }
        }

        if (collected.Count == 0 && warnings.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", warnings));
        }

        return collected
            .OrderByDescending(record => record.TimeCreated)
            .Take(options.MaxRecords)
            .ToList();
    }

    private static void ReadWithEventReader(
        string logName,
        EventLogQueryOptions options,
        List<WindowsEventRecord> collected,
        CancellationToken cancellationToken)
    {
        var lookbackMs = (long)Math.Min(options.Lookback.TotalMilliseconds, int.MaxValue);
        var xpath = $"*[System[(Level=1 or Level=2) and TimeCreated[timediff(@SystemTime) <= {lookbackMs}]]]";
        var query = new EventLogQuery(logName, PathType.LogName, xpath);

        try
        {
            query.ReverseDirection = true;
        }
        catch (Exception)
        {
            query.ReverseDirection = false;
        }

        using var reader = new EventLogReader(query);
        EventRecord? record;
        while ((record = reader.ReadEvent()) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (record)
            {
                if (collected.Count >= options.MaxRecords)
                {
                    return;
                }

                var mapped = Map(logName, record);
                if (Matches(mapped, options))
                {
                    collected.Add(mapped);
                }
            }
        }
    }

    private static void ReadWithClassicLog(
        string logName,
        EventLogQueryOptions options,
        List<WindowsEventRecord> collected,
        CancellationToken cancellationToken)
    {
        using var log = new EventLog(logName);
        var cutoff = DateTime.Now - options.Lookback;

        for (var index = log.Entries.Count - 1; index >= 0; index--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (collected.Count >= options.MaxRecords)
            {
                return;
            }

            var entry = log.Entries[index];
            if (entry.TimeGenerated < cutoff)
            {
                continue;
            }

            if (entry.EntryType is not EventLogEntryType.Error and not EventLogEntryType.FailureAudit)
            {
                continue;
            }

            string message;
            try
            {
                message = string.IsNullOrWhiteSpace(entry.Message)
                    ? "(No message text was provided for this event.)"
                    : entry.Message;
            }
            catch (Exception)
            {
                message = "(Event description could not be formatted.)";
            }

            var mapped = new WindowsEventRecord
            {
                LogName = logName,
                Source = entry.Source,
                Provider = entry.Source,
                EventId = (int)(entry.InstanceId & 0xFFFF),
                Level = entry.EntryType == EventLogEntryType.Error ? "Error" : entry.EntryType.ToString(),
                LevelCode = 2,
                TimeCreated = new DateTimeOffset(DateTime.SpecifyKind(entry.TimeGenerated, DateTimeKind.Local)),
                Message = message,
                MachineName = entry.MachineName
            };

            if (Matches(mapped, options))
            {
                collected.Add(mapped);
            }
        }
    }

    private static WindowsEventRecord Map(string logName, EventRecord record)
    {
        string message;
        try
        {
            message = record.FormatDescription() ?? "(No message text was provided for this event.)";
        }
        catch (Exception)
        {
            message = "(Event description could not be formatted.)";
        }

        return new WindowsEventRecord
        {
            LogName = logName,
            Source = record.ProviderName ?? "Unknown",
            Provider = record.ProviderName ?? "Unknown",
            EventId = record.Id,
            Level = record.LevelDisplayName ?? LevelName(record.Level),
            LevelCode = record.Level,
            TimeCreated = record.TimeCreated.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(record.TimeCreated.Value, DateTimeKind.Local))
                : null,
            Message = message,
            MachineName = record.MachineName ?? Environment.MachineName
        };
    }

    private static bool Matches(WindowsEventRecord record, EventLogQueryOptions options)
    {
        if (options.EventId is int eventId && record.EventId != eventId)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(options.SourceContains) &&
            record.Source.Contains(options.SourceContains, StringComparison.OrdinalIgnoreCase) is false)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(options.MessageContains) &&
            record.Message.Contains(options.MessageContains, StringComparison.OrdinalIgnoreCase) is false)
        {
            return false;
        }

        if (options.Levels.Count == 0)
        {
            return true;
        }

        if (record.LevelCode is 1 or 2)
        {
            return true;
        }

        return options.Levels.Any(level =>
            record.Level.Contains(level, StringComparison.OrdinalIgnoreCase) ||
            record.Level.Contains("Kritik", StringComparison.OrdinalIgnoreCase) ||
            record.Level.Contains("Hata", StringComparison.OrdinalIgnoreCase) ||
            record.Level.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
            record.Level.Contains("Critical", StringComparison.OrdinalIgnoreCase));
    }

    private static string LevelName(byte? level) => level switch
    {
        1 => "Critical",
        2 => "Error",
        3 => "Warning",
        4 => "Information",
        _ => "Unknown"
    };
}
