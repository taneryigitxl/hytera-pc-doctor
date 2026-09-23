using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Logging;
using PCDoctor.Core.Models.Network;
using PCDoctor.Core.Models.Results;

namespace PCDoctor.Diagnostics.Collectors;

public sealed class NetworkCollector : INetworkService
{
    private static readonly HttpClient Http = CreateClient();
    private readonly IAppLogger _logger;
    private readonly ILocalizationService _loc;

    public NetworkCollector(IAppLogger logger, ILocalizationService localization)
    {
        _logger = logger;
        _loc = localization;
    }

    public async Task<OperationResult<NetworkDiagnosticsResult>> DiagnoseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var adapter = ResolveActiveAdapter();
            var networkAvailable = NetworkInterface.GetIsNetworkAvailable();

            var gatewayPing = string.IsNullOrWhiteSpace(adapter.Gateway) || adapter.Gateway == "Unknown"
                ? new ConnectivityTest
                {
                    Name = _loc["Net.GatewayName"],
                    Succeeded = false,
                    Detail = _loc["Net.NoGateway"],
                    Source = "IPInterfaceProperties.GatewayAddresses"
                }
                : await PingAsync(_loc["Net.GatewayName"], adapter.Gateway, cancellationToken).ConfigureAwait(false);

            var internetPing = await PingAsync(_loc["Net.PingName"], "1.1.1.1", cancellationToken).ConfigureAwait(false);
            if (!internetPing.Succeeded)
            {
                internetPing = await PingAsync(_loc["Net.PingName"], "8.8.8.8", cancellationToken).ConfigureAwait(false);
            }

            var dns = await ResolveDnsAsync(cancellationToken).ConfigureAwait(false);
            var http = await HttpConnectAsync(cancellationToken).ConfigureAwait(false);

            var warnings = new List<string>();
            if (!networkAvailable)
            {
                warnings.Add("NetworkInterface.GetIsNetworkAvailable() returned false.");
            }

            var result = new NetworkDiagnosticsResult
            {
                ActiveAdapter = adapter,
                IsNetworkAvailable = networkAvailable,
                InternetReachable = http.Succeeded,
                PingTest = internetPing,
                GatewayPingTest = gatewayPing,
                DnsTest = dns,
                HttpTest = http,
                Warnings = warnings,
                CollectedAt = DateTimeOffset.Now
            };

            return OperationResult<NetworkDiagnosticsResult>.Ok(result, warnings);
        }
        catch (Exception ex)
        {
            _logger.Error("Network diagnostics failed.", ex);
            return OperationResult<NetworkDiagnosticsResult>.Fail(
                "Network diagnostics could not be completed.",
                ex.ToString());
        }
    }

    private static ActiveAdapterInfo ResolveActiveAdapter()
    {
        var candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter => adapter.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
            .Select(adapter => new { Adapter = adapter, Properties = adapter.GetIPProperties() })
            .ToList();

        var selected = candidates.FirstOrDefault(item =>
                           item.Adapter.OperationalStatus == OperationalStatus.Up &&
                           item.Properties.GatewayAddresses.Count > 0 &&
                           item.Properties.UnicastAddresses.Any(address => address.Address.AddressFamily == AddressFamily.InterNetwork))
                       ?? candidates.FirstOrDefault(item => item.Adapter.OperationalStatus == OperationalStatus.Up)
                       ?? candidates.FirstOrDefault();

        if (selected is null)
        {
            return new ActiveAdapterInfo();
        }

        var ipv4 = selected.Properties.UnicastAddresses
            .FirstOrDefault(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
            ?.Address.ToString() ?? "Unknown";

        return new ActiveAdapterInfo
        {
            Name = selected.Adapter.Name,
            Description = selected.Adapter.Description,
            LocalIp = ipv4,
            Gateway = selected.Properties.GatewayAddresses.FirstOrDefault()?.Address.ToString() ?? "Unknown",
            Dns = selected.Properties.DnsAddresses.Count == 0
                ? "Unknown"
                : string.Join(", ", selected.Properties.DnsAddresses.Select(address => address.ToString())),
            MacAddress = HardwareCollector.FormatMac(selected.Adapter.GetPhysicalAddress()),
            LinkSpeed = selected.Adapter.Speed > 0 ? HardwareCollector.FormatLinkSpeed(selected.Adapter.Speed) : "Unknown",
            Status = selected.Adapter.OperationalStatus.ToString(),
            Type = selected.Adapter.NetworkInterfaceType.ToString()
        };
    }

    private async Task<ConnectivityTest> PingAsync(string name, string host, CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, 3000).WaitAsync(cancellationToken).ConfigureAwait(false);
            return new ConnectivityTest
            {
                Name = name,
                Succeeded = reply.Status == IPStatus.Success,
                Detail = reply.Status == IPStatus.Success
                    ? _loc.Get("Net.PingOk", host, reply.RoundtripTime)
                    : _loc.Get("Net.PingFail", host, reply.Status),
                LatencyMilliseconds = reply.Status == IPStatus.Success ? reply.RoundtripTime : null,
                Source = $"System.Net.NetworkInformation.Ping -> {host}"
            };
        }
        catch (Exception ex)
        {
            _logger.Warn($"Ping to {host} failed.", ex);
            return new ConnectivityTest
            {
                Name = name,
                Succeeded = false,
                Detail = _loc.Get("Net.PingThrow", host, ex.GetType().Name, ex.Message),
                Source = $"System.Net.NetworkInformation.Ping -> {host}"
            };
        }
    }

    private async Task<ConnectivityTest> ResolveDnsAsync(CancellationToken cancellationToken)
    {
        const string host = "www.microsoft.com";
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
            var visible = string.Join(", ", addresses.Take(4).Select(address => address.ToString()));
            return new ConnectivityTest
            {
                Name = _loc["Net.DnsName"],
                Succeeded = addresses.Length > 0,
                Detail = addresses.Length > 0
                    ? _loc.Get("Net.DnsOk", host, visible)
                    : _loc.Get("Net.DnsEmpty", host),
                Source = $"System.Net.Dns.GetHostAddressesAsync({host})"
            };
        }
        catch (Exception ex)
        {
            _logger.Warn($"DNS resolution for {host} failed.", ex);
            return new ConnectivityTest
            {
                Name = _loc["Net.DnsName"],
                Succeeded = false,
                Detail = _loc.Get("Net.DnsFail", host, ex.Message),
                Source = $"System.Net.Dns.GetHostAddressesAsync({host})"
            };
        }
    }

    private async Task<ConnectivityTest> HttpConnectAsync(CancellationToken cancellationToken)
    {
        const string url = "http://www.msftconnecttest.com/connecttest.txt";
        try
        {
            using var response = await Http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var expected = body.Contains("Microsoft Connect Test", StringComparison.OrdinalIgnoreCase);
            return new ConnectivityTest
            {
                Name = _loc["Net.HttpName"],
                Succeeded = response.IsSuccessStatusCode && expected,
                Detail = response.IsSuccessStatusCode
                    ? _loc.Get("Net.HttpOk", (int)response.StatusCode, url, expected)
                    : _loc.Get("Net.HttpStatus", (int)response.StatusCode, url),
                Source = url
            };
        }
        catch (Exception ex)
        {
            _logger.Warn("HTTP connectivity test failed.", ex);
            return new ConnectivityTest
            {
                Name = _loc["Net.HttpName"],
                Succeeded = false,
                Detail = _loc.Get("Net.HttpFail", url, ex.Message),
                Source = url
            };
        }
    }

    private static HttpClient CreateClient()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8),
            DefaultRequestHeaders = { { "User-Agent", "PCDoctor/1.0" } }
        };
    }
}
