using PCDoctor.Core.Enums;

namespace PCDoctor.Core.Logging;

public interface IAppLogger
{
    string LogDirectory { get; }
    string CurrentLogFile { get; }
    void Log(LogLevel level, string message, Exception? exception = null);
    void Info(string message) => Log(LogLevel.Information, message);
    void Warn(string message, Exception? exception = null) => Log(LogLevel.Warning, message, exception);
    void Error(string message, Exception? exception = null) => Log(LogLevel.Error, message, exception);
    void Debug(string message, Exception? exception = null) => Log(LogLevel.Debug, message, exception);
}
