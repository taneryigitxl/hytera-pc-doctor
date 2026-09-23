namespace PCDoctor.Core.Models.Events;

public sealed class WindowsEventRecord
{
    public string LogName { get; init; } = string.Empty;
    public string Source { get; init; } = "Unknown";
    public int EventId { get; init; }
    public string Level { get; init; } = "Unknown";
    public byte? LevelCode { get; init; }
    public DateTimeOffset? TimeCreated { get; init; }
    public string Message { get; init; } = string.Empty;
    public string Provider { get; init; } = "Unknown";
    public string MachineName { get; init; } = Environment.MachineName;
}

public sealed class EventLogQueryOptions
{
    public IReadOnlyList<string> LogNames { get; init; } = ["System", "Application"];
    public IReadOnlyList<string> Levels { get; init; } = ["Critical", "Error"];
    public TimeSpan Lookback { get; init; } = TimeSpan.FromDays(7);
    public int MaxRecords { get; init; } = 250;
    public string? SourceContains { get; init; }
    public string? MessageContains { get; init; }
    public int? EventId { get; init; }
}
