namespace PCDoctor.Core.Models.Startup;

public enum StartupEntryKind
{
    Registry,
    Folder,
    Other
}

public sealed class StartupEntry
{
    public string Name { get; init; } = "Unknown";
    public string Command { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string Source { get; init; } = "Unknown";
    public string User { get; init; } = "Unknown";
    public bool IsEnabled { get; init; } = true;
    public bool CanToggle { get; init; }
    public StartupEntryKind Kind { get; init; } = StartupEntryKind.Other;
    public string? Hive { get; init; }
    public string? RegistryPath { get; init; }
    public string? ValueName { get; init; }
    public string? FilePath { get; init; }
}
