namespace PCDoctor.Core.Models.Network;

public sealed class ActiveAdapterInfo
{
    public string Name { get; init; } = "None";
    public string Description { get; init; } = "None";
    public string LocalIp { get; init; } = "Unknown";
    public string Gateway { get; init; } = "Unknown";
    public string Dns { get; init; } = "Unknown";
    public string MacAddress { get; init; } = "Unknown";
    public string LinkSpeed { get; init; } = "Unknown";
    public string Status { get; init; } = "Unknown";
    public string Type { get; init; } = "Unknown";
}

public sealed class ConnectivityTest
{
    public string Name { get; init; } = string.Empty;
    public bool Succeeded { get; init; }
    public string Detail { get; init; } = string.Empty;
    public long? LatencyMilliseconds { get; init; }
    public string Source { get; init; } = string.Empty;
}

public sealed class NetworkDiagnosticsResult
{
    public ActiveAdapterInfo ActiveAdapter { get; init; } = new();
    public bool IsNetworkAvailable { get; init; }
    public bool InternetReachable { get; init; }
    public ConnectivityTest PingTest { get; init; } = new();
    public ConnectivityTest GatewayPingTest { get; init; } = new();
    public ConnectivityTest DnsTest { get; init; } = new();
    public ConnectivityTest HttpTest { get; init; } = new();
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public DateTimeOffset CollectedAt { get; init; } = DateTimeOffset.Now;
}
