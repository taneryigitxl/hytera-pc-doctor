using PCDoctor.Core.Enums;
using PCDoctor.Core.Logging;

namespace PCDoctor.Diagnostics.Logging;

public sealed class FileAppLogger : IAppLogger
{
    private readonly Lock _sync = new();

    public string LogDirectory { get; }
    public string CurrentLogFile { get; }

    public FileAppLogger(string? logDirectory = null)
    {
        LogDirectory = logDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PCDoctor",
            "logs");

        Directory.CreateDirectory(LogDirectory);
        CurrentLogFile = Path.Combine(LogDirectory, $"pcdoctor-{DateTime.Now:yyyyMMdd}.log");
    }

    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        var line = $"{DateTimeOffset.Now:O} | {level,-11} | {message}";
        if (exception is not null)
        {
            line += $"{Environment.NewLine}{exception}";
        }

        lock (_sync)
        {
            File.AppendAllText(CurrentLogFile, line + Environment.NewLine);
        }

        if (level == LogLevel.Error)
        {
            System.Diagnostics.Debug.WriteLine(line);
        }
    }
}
