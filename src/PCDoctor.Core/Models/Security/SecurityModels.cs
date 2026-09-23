namespace PCDoctor.Core.Models.Security;

public sealed class DefenderStatus
{
    public bool IsAvailable { get; init; } = true;
    public string? Error { get; init; }
    public bool AntivirusEnabled { get; init; }
    public bool RealTimeProtectionEnabled { get; init; }
    public bool AntispywareEnabled { get; init; }
    public bool IoavProtectionEnabled { get; init; }
    public bool BehaviorMonitorEnabled { get; init; }
    public bool NisEnabled { get; init; }
    public string AntivirusSignatureVersion { get; init; } = "Unknown";
    public DateTimeOffset? AntivirusSignatureLastUpdated { get; init; }
    public string ProductStatus { get; init; } = "Unknown";
}

public sealed class FirewallProfileStatus
{
    public string Name { get; init; } = "Unknown";
    public bool Enabled { get; init; }
    public string Source { get; init; } = "Unknown";
}

public sealed class SecuritySnapshot
{
    public DefenderStatus Defender { get; init; } = new();
    public IReadOnlyList<FirewallProfileStatus> FirewallProfiles { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
