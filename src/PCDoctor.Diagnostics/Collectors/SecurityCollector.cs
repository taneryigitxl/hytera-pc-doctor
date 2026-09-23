using Microsoft.Win32;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Logging;
using PCDoctor.Core.Models.Results;
using PCDoctor.Core.Models.Security;

namespace PCDoctor.Diagnostics.Collectors;

public sealed class SecurityCollector : ISecurityService
{
    private readonly IAppLogger _logger;
    private readonly WmiClient _wmi;

    public SecurityCollector(IAppLogger logger, WmiClient wmi)
    {
        _logger = logger;
        _wmi = wmi;
    }

    public async Task<OperationResult<SecuritySnapshot>> GetSecuritySnapshotAsync(CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        var defender = await CollectDefenderAsync(warnings, cancellationToken).ConfigureAwait(false);
        var firewall = await CollectFirewallAsync(warnings, cancellationToken).ConfigureAwait(false);

        return OperationResult<SecuritySnapshot>.Ok(new SecuritySnapshot
        {
            Defender = defender,
            FirewallProfiles = firewall,
            Warnings = warnings
        }, warnings);
    }

    private async Task<DefenderStatus> CollectDefenderAsync(ICollection<string> warnings, CancellationToken cancellationToken)
    {
        try
        {
            var rows = await _wmi.QueryAsync(
                @"root\Microsoft\Windows\Defender",
                "SELECT AntivirusEnabled, RealTimeProtectionEnabled, AntispywareEnabled, IoavProtectionEnabled, BehaviorMonitorEnabled, NISEnabled, AntivirusSignatureVersion, AntivirusSignatureLastUpdated FROM MSFT_MpComputerStatus",
                cancellationToken).ConfigureAwait(false);

            var row = rows.FirstOrDefault() ?? throw new InvalidOperationException("MSFT_MpComputerStatus returned no records.");
            return new DefenderStatus
            {
                AntivirusEnabled = row.Flag("AntivirusEnabled"),
                RealTimeProtectionEnabled = row.Flag("RealTimeProtectionEnabled"),
                AntispywareEnabled = row.Flag("AntispywareEnabled"),
                IoavProtectionEnabled = row.Flag("IoavProtectionEnabled"),
                BehaviorMonitorEnabled = row.Flag("BehaviorMonitorEnabled"),
                NisEnabled = row.Flag("NISEnabled"),
                AntivirusSignatureVersion = row.Text("AntivirusSignatureVersion"),
                AntivirusSignatureLastUpdated = row.Date("AntivirusSignatureLastUpdated"),
                ProductStatus = row.Flag("AntivirusEnabled") ? "Enabled" : "Disabled"
            };
        }
        catch (Exception ex)
        {
            _logger.Warn("Windows Defender WMI query failed.", ex);
            warnings.Add($"Windows Defender status could not be read from MSFT_MpComputerStatus: {ex.Message}");
            return new DefenderStatus
            {
                IsAvailable = false,
                Error = ex.Message
            };
        }
    }

    private async Task<IReadOnlyList<FirewallProfileStatus>> CollectFirewallAsync(ICollection<string> warnings, CancellationToken cancellationToken)
    {
        try
        {
            var rows = await _wmi.QueryAsync(
                @"root\StandardCimv2",
                "SELECT Name, Enabled FROM MSFT_NetFirewallProfile",
                cancellationToken).ConfigureAwait(false);

            if (rows.Count > 0)
            {
                return rows.Select(row => new FirewallProfileStatus
                {
                    Name = row.Text("Name"),
                    Enabled = row.Flag("Enabled"),
                    Source = @"root\StandardCimv2\MSFT_NetFirewallProfile"
                }).ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.Warn("MSFT_NetFirewallProfile query failed; falling back to firewall policy registry keys.", ex);
            warnings.Add($"Firewall CIM query failed: {ex.Message}");
        }

        return ReadFirewallFromRegistry(warnings);
    }

    private IReadOnlyList<FirewallProfileStatus> ReadFirewallFromRegistry(ICollection<string> warnings)
    {
        var profiles = new (string Name, string Path)[]
        {
            ("Domain", @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\DomainProfile"),
            ("Private", @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile"),
            ("Public", @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\PublicProfile")
        };

        var results = new List<FirewallProfileStatus>();
        foreach (var profile in profiles)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(profile.Path);
                if (key is null)
                {
                    warnings.Add($"Firewall registry key not found: HKLM\\{profile.Path}");
                    continue;
                }

                var enabled = Convert.ToInt32(key.GetValue("EnableFirewall", 0)) == 1;
                results.Add(new FirewallProfileStatus
                {
                    Name = profile.Name,
                    Enabled = enabled,
                    Source = $@"HKLM\{profile.Path}\EnableFirewall"
                });
            }
            catch (Exception ex)
            {
                _logger.Warn($"Could not read firewall profile {profile.Name}.", ex);
                warnings.Add($"Firewall profile {profile.Name} could not be read: {ex.Message}");
            }
        }

        return results;
    }
}
