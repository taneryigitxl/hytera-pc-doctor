using System.Diagnostics;
using PCDoctor.Core.Enums;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Logging;
using PCDoctor.Core.Models.Diagnostics;

namespace PCDoctor.Diagnostics.Services;

public sealed class RepairService : IRepairService
{
    private static readonly string[] ProtectedStartupTokens =
    [
        "microsoft", "windows", "securityhealth", "hytera", "pcdoctor",
        "intel", "nvidia", "amd", "realtek"
    ];

    private readonly IStartupService _startup;
    private readonly IAppLogger _logger;
    private readonly ILocalizationService _loc;

    public RepairService(IStartupService startup, IAppLogger logger, ILocalizationService localization)
    {
        _startup = startup;
        _logger = logger;
        _loc = localization;
    }

    public async Task<RepairReport> RepairAsync(IReadOnlyList<ScanFinding> findings, CancellationToken cancellationToken = default)
    {
        var problems = findings
            .Where(finding => finding.Severity is Severity.Warning or Severity.Critical)
            .ToList();
        var modules = problems.Select(finding => finding.Module).ToHashSet();
        var actions = new List<RepairActionResult>();

        if (modules.Overlaps([ScanModuleKind.Storage, ScanModuleKind.Ram, ScanModuleKind.Cpu]))
        {
            cancellationToken.ThrowIfCancellationRequested();
            actions.Add(await Task.Run(CleanTemporaryFiles, cancellationToken).ConfigureAwait(false));
        }

        if (modules.Contains(ScanModuleKind.Network))
        {
            cancellationToken.ThrowIfCancellationRequested();
            actions.Add(await Task.Run(
                () => RunHidden("ipconfig", "/flushdns", _loc["Repair.Dns"], _loc["Repair.DnsOk"], _loc["Repair.DnsFail"]),
                cancellationToken).ConfigureAwait(false));
        }

        if (modules.Contains(ScanModuleKind.Startup))
        {
            cancellationToken.ThrowIfCancellationRequested();
            actions.Add(await DisableExtraStartupsAsync(cancellationToken).ConfigureAwait(false));
        }

        if (modules.Contains(ScanModuleKind.Security))
        {
            cancellationToken.ThrowIfCancellationRequested();
            actions.Add(await Task.Run(
                () => RunHidden(
                    "netsh",
                    "advfirewall set allprofiles state on",
                    _loc["Repair.Firewall"],
                    _loc["Repair.FirewallOk"],
                    _loc["Repair.FirewallFail"]),
                cancellationToken).ConfigureAwait(false));
            actions.Add(await Task.Run(
                () => RunHidden(
                    "powershell",
                    "-NoProfile -ExecutionPolicy Bypass -Command \"try { Set-MpPreference -DisableRealtimeMonitoring $false; Update-MpSignature } catch { throw }\"",
                    _loc["Repair.Defender"],
                    _loc["Repair.DefenderOk"],
                    _loc["Repair.DefenderFail"]),
                cancellationToken).ConfigureAwait(false));
        }

        if (actions.Count == 0)
        {
            actions.Add(new RepairActionResult
            {
                Title = _loc["Repair.NoAutoFix"],
                Succeeded = true,
                Detail = _loc["Repair.NoAutoFixDetail"]
            });
        }

        return new RepairReport { Actions = actions };
    }

    private RepairActionResult CleanTemporaryFiles()
    {
        var folders = new List<string> { Path.GetTempPath() };
        var windowsTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
        if (!string.Equals(windowsTemp, Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            folders.Add(windowsTemp);
        }

        var deleted = 0;
        long bytes = 0;
        foreach (var folder in folders.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var info = new FileInfo(file);
                    var size = info.Length;
                    info.Delete();
                    deleted++;
                    bytes += size;
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Temp file skipped: {file}", ex);
                }
            }
        }

        return new RepairActionResult
        {
            Title = _loc["Repair.Temp"],
            Succeeded = true,
            Detail = _loc.Get("Repair.TempOk", deleted, FormatBytes(bytes))
        };
    }

    private async Task<RepairActionResult> DisableExtraStartupsAsync(CancellationToken cancellationToken)
    {
        var result = await _startup.GetStartupEntriesAsync(cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || result.Value is null)
        {
            return new RepairActionResult
            {
                Title = _loc["Repair.Startup"],
                Succeeded = false,
                Detail = result.ErrorMessage ?? _loc["Repair.StartupFail"]
            };
        }

        var candidates = result.Value
            .Where(entry => entry is { CanToggle: true, IsEnabled: true } && !IsProtected(entry.Name, entry.Command))
            .ToList();

        var disabled = 0;
        var failed = 0;
        foreach (var entry in candidates)
        {
            var toggle = await _startup.SetEnabledAsync(entry, false, cancellationToken).ConfigureAwait(false);
            if (toggle.Succeeded)
            {
                disabled++;
            }
            else
            {
                failed++;
                _logger.Warn($"Could not disable startup entry '{entry.Name}'.");
            }
        }

        return new RepairActionResult
        {
            Title = _loc["Repair.Startup"],
            Succeeded = failed == 0,
            Detail = _loc.Get("Repair.StartupOk", disabled, failed)
        };
    }

    private RepairActionResult RunHidden(string fileName, string arguments, string title, string okDetail, string failDetail)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (process is null)
            {
                return new RepairActionResult { Title = title, Succeeded = false, Detail = failDetail };
            }

            if (!process.WaitForExit(60_000))
            {
                try { process.Kill(true); } catch { }
                return new RepairActionResult { Title = title, Succeeded = false, Detail = failDetail };
            }

            return new RepairActionResult
            {
                Title = title,
                Succeeded = process.ExitCode == 0,
                Detail = process.ExitCode == 0 ? okDetail : $"{failDetail} ({process.ExitCode})"
            };
        }
        catch (Exception ex)
        {
            _logger.Error($"Repair command failed: {fileName} {arguments}", ex);
            return new RepairActionResult { Title = title, Succeeded = false, Detail = $"{failDetail} {ex.Message}" };
        }
    }

    private static bool IsProtected(string name, string command)
    {
        var text = $"{name} {command}";
        return ProtectedStartupTokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824d:0.0} GB";
        if (bytes >= 1_048_576) return $"{bytes / 1_048_576d:0.0} MB";
        if (bytes >= 1024) return $"{bytes / 1024d:0} KB";
        return $"{bytes} B";
    }
}
