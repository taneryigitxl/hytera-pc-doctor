using PCDoctor.Core.Enums;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Models.Diagnostics;
using PCDoctor.Core.Models.Network;

namespace PCDoctor.Diagnostics.Scanning.Modules;

internal sealed class NetworkScanModule : ScanModuleBase
{
    private readonly INetworkService _network;

    public NetworkScanModule(INetworkService network, ILocalizationService localization) : base(localization)
    {
        _network = network;
    }

    public override ScanModuleKind Kind => ScanModuleKind.Network;
    public override string DisplayName => Loc["Module.Network"];

    protected override async Task<List<ScanFinding>> CollectAsync(CancellationToken cancellationToken)
    {
        var result = await _network.DiagnoseAsync(cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || result.Value is null)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Network diagnostics are unavailable.");
        }

        var data = result.Value;
        var adapter = data.ActiveAdapter;
        var online = adapter.Status.Equals("Up", StringComparison.OrdinalIgnoreCase);
        var findings = new List<ScanFinding>
        {
            Finding(
                online ? Severity.Ok : Severity.Critical,
                online ? Loc["Scan.Net.AdapterOk"] : Loc["Scan.Net.AdapterFail"],
                Loc.Get("Scan.Net.AdapterSummary", adapter.Name, adapter.Description),
                Loc.Get("Scan.Net.AdapterEvidence", adapter.Status, adapter.LocalIp, adapter.Gateway, adapter.Dns, adapter.MacAddress, adapter.LinkSpeed),
                "NetworkInterface")
        };

        AddTest(findings, data.HttpTest, Severity.Critical, "Scan.Net.HttpOk", "Scan.Net.HttpFail");
        AddTest(findings, data.DnsTest, Severity.Critical, "Scan.Net.DnsOk", "Scan.Net.DnsFail");

        if (data.PingTest.Succeeded && data.PingTest.LatencyMilliseconds >= 200)
        {
            findings.Add(Finding(Severity.Warning, Loc["Scan.Net.PingHigh"], data.PingTest.Detail, Loc.Get("Scan.Net.PingHighEvidence", data.PingTest.LatencyMilliseconds), data.PingTest.Source));
        }
        else
        {
            AddTest(findings, data.PingTest, Severity.Warning, "Scan.Net.PingOk", "Scan.Net.PingFail");
        }

        AddTest(findings, data.GatewayPingTest, Severity.Warning, "Scan.Net.GwOk", "Scan.Net.GwFail");
        return findings;
    }

    private void AddTest(ICollection<ScanFinding> findings, ConnectivityTest test, Severity failure, string okKey, string failKey)
    {
        findings.Add(Finding(
            test.Succeeded ? Severity.Ok : failure,
            Loc[test.Succeeded ? okKey : failKey],
            test.Detail,
            test.Detail,
            test.Source));
    }
}
