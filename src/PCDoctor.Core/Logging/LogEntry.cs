using PCDoctor.Core.Enums;

namespace PCDoctor.Core.Logging;

public sealed class LogEntry
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
    public LogLevel Level { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Exception { get; init; }
}
