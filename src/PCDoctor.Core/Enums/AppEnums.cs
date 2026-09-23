namespace PCDoctor.Core.Enums;

public enum Severity
{
    Ok = 0,
    Warning = 1,
    Critical = 2
}

public enum ScanModuleKind
{
    Cpu,
    Ram,
    Storage,
    Windows,
    Drivers,
    Network,
    EventLogs,
    Startup,
    Security
}

public enum AppTheme
{
    Dark,
    Light
}

public enum AppLanguage
{
    Turkish = 0,
    English = 1
}

public enum AppSection
{
    Dashboard,
    SystemScan,
    Hardware,
    Network,
    Windows,
    EventLogs,
    Startup,
    Security,
    Troubleshooter,
    Reports,
    Settings
}

public enum LogLevel
{
    Debug,
    Information,
    Warning,
    Error
}
